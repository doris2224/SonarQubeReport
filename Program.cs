using SonarQubeReport;
using SonarQubeReport.Models;
using SonarQubeReport.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length < 3)
{
    Console.WriteLine("用法：SonarQubeReport.exe \"<sonarQubeToken>\" \"<sonarQubeProjects>\" \"<reportPath>\"");
    Console.WriteLine();
    Console.WriteLine("  sonarQubeToken   : SonarQube 使用者權杖 (User Token)");
    Console.WriteLine("  sonarQubeProjects: 專案 Key，多專案以逗號分隔，例如 Test 或 Test1,Test2");
    Console.WriteLine("  reportPath       : 報表輸出路徑，可為完整檔名(.xlsx)或資料夾（資料夾時會自動命名）");
    Console.WriteLine();
    Console.WriteLine("範例：SonarQubeReport.exe \"squ_xxxxxxxx\" \"Test\" \"C:\\Reports\\Test.xlsx\"");
    return 1;
}

string token = args[0];
string projectsArg = args[1];
string reportPathArg = args[2];

var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var config = AppConfig.Load(configPath);

var projects = projectsArg
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

if (projects.Length == 0)
{
    Console.Error.WriteLine("錯誤：sonarQubeProjects 不可為空。");
    return 1;
}

string outputPath = ResolveOutputPath(reportPathArg, projects);

try
{
    using var client = new SonarQubeClient(config.BaseUrl, token, config.PageSize);

    Console.WriteLine($"連線 SonarQube  : {config.BaseUrl}");
    Console.WriteLine($"目標專案        : {string.Join(", ", projects)}");
    foreach (var p in projects)
        Console.WriteLine($"  儀表板連結     : {config.BaseUrl.TrimEnd('/')}/dashboard?id={p}");
    Console.WriteLine();

    // Issues / All 頁籤：resolved=false（尚未被標記解決的 issue）
    Console.WriteLine("下載 Issues 資料中 (resolved=false)...");
    var (issues, ruleLanguages) = await client.SearchIssuesAsync(projects, resolved: false);
    Console.WriteLine($"  取得 {issues.Count} 筆 issue");

    // Unconfirmed 頁籤：resolved=true（已被標記解決，如 wontfix / false-positive / fixed）
    Console.WriteLine("下載 Unconfirmed 資料中 (resolved=true)...");
    var (unconfirmed, unconfirmedRuleLanguages) = await client.SearchIssuesAsync(projects, resolved: true);
    Console.WriteLine($"  取得 {unconfirmed.Count} 筆 issue");

    foreach (var kv in unconfirmedRuleLanguages)
        ruleLanguages[kv.Key] = kv.Value;

    Console.WriteLine("下載 Security Hotspots 資料中...");
    var hotspotRows = new List<HotspotRow>();
    foreach (var project in projects)
    {
        var hotspots = await client.SearchHotspotsAsync(project, config.HotspotStatus);
        foreach (var h in hotspots)
        {
            var ruleDetail = await client.GetRuleDetailAsync(h.RuleKey);
            var comments = string.IsNullOrEmpty(h.Key) ? "" : await client.GetHotspotCommentsAsync(h.Key);
            hotspotRows.Add(new HotspotRow(h, ruleDetail, comments));
        }
    }
    Console.WriteLine($"  取得 {hotspotRows.Count} 筆 security hotspot");

    // 【新增】獲取組件樹數據（用於 Metrics 頁籤）
    Console.WriteLine("下載 Metrics 資料中...");
    var allComponents = new List<TreeComponent>();
    foreach (var project in projects)
    {
        var components = await client.GetComponentTreeAsync(project);
        allComponents.AddRange(components);
    }
    Console.WriteLine($"  取得 {allComponents.Count} 個組件");

    Console.WriteLine();
    Console.WriteLine($"產出報表: {outputPath}");
    ExcelReportBuilder.Build(outputPath, issues, unconfirmed, ruleLanguages, hotspotRows, allComponents);

    Console.WriteLine("完成。");
    return 0;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"呼叫 SonarQube API 時發生錯誤：{ex.Message}");
    return 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"執行失敗：{ex}");
    return 3;
}

static string ResolveOutputPath(string reportPathArg, string[] projects)
{
    if (reportPathArg.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
    {
        var dir = Path.GetDirectoryName(reportPathArg);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        return reportPathArg;
    }

    Directory.CreateDirectory(reportPathArg);
    string fileName = $"{string.Join("_", projects)}_SonarQubeReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
    return Path.Combine(reportPathArg, fileName);
}
