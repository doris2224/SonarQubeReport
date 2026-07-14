using System.Text.Json;

namespace SonarQubeReport;

/// <summary>
/// 對應 appsettings.json 內的 "SonarQube" 區塊。
/// 刻意不使用 Microsoft.Extensions.Configuration，改用 System.Text.Json 手動解析，
/// 以減少發佈為單一 exe 檔時需要一併打包的相依套件。
/// </summary>
public class AppConfig
{
    public string BaseUrl { get; set; } = "http://localhost:9000";
    public int PageSize { get; set; } = 500;
    public List<string> UnconfirmedStatuses { get; set; } = new() { "CLOSED", "RESOLVED" };
    public string HotspotStatus { get; set; } = "TO_REVIEW";

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"警告：找不到設定檔 {path}，將使用預設值執行。");
            return new AppConfig();
        }

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("SonarQube", out var root))
        {
            Console.WriteLine("警告：appsettings.json 內找不到 \"SonarQube\" 區塊，將使用預設值。");
            return new AppConfig();
        }

        var cfg = new AppConfig();

        if (root.TryGetProperty("BaseUrl", out var baseUrl) && baseUrl.ValueKind == JsonValueKind.String)
            cfg.BaseUrl = baseUrl.GetString() ?? cfg.BaseUrl;

        if (root.TryGetProperty("PageSize", out var pageSize) && pageSize.ValueKind == JsonValueKind.Number)
            cfg.PageSize = pageSize.GetInt32();

        if (root.TryGetProperty("HotspotStatus", out var hotspotStatus) && hotspotStatus.ValueKind == JsonValueKind.String)
            cfg.HotspotStatus = hotspotStatus.GetString() ?? cfg.HotspotStatus;

        if (root.TryGetProperty("UnconfirmedStatuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
        {
            var list = statuses.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => s.Length > 0)
                .ToList();

            if (list.Count > 0)
                cfg.UnconfirmedStatuses = list;
        }

        return cfg;
    }
}
