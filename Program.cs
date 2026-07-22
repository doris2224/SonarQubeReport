using SonarQubeReport;
using SonarQubeReport.Models;
using SonarQubeReport.Services;
using System.Text;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length < 3)
{
    Console.WriteLine("用法：SonarQubeReport.exe \"<sonarQubeToken>\" \"<sonarQubeProjects>\" \"<reportPath>\" [<ceTaskID>]");
    Console.WriteLine();
    Console.WriteLine("  sonarQubeToken   : SonarQube 使用者權杖 (User Token)");
    Console.WriteLine("  sonarQubeProjects: 專案 Key，多專案以逗號分隔，例如 Test 或 Test1,Test2");
    Console.WriteLine("  reportPath       : 報表輸出路徑，可為完整檔名(.xlsx)或資料夾（資料夾時會自動命名）");
    Console.WriteLine("  ceTaskID         : (選擇性) CE Task ID，用於驗證分析是否完成");
    Console.WriteLine();
    Console.WriteLine("範例（不使用 CE Task ID）：");
    Console.WriteLine("  SonarQubeReport.exe \"squ_xxxxxxxx\" \"Test\" \"C:\\Reports\\Test.xlsx\"");
    Console.WriteLine();
    Console.WriteLine("範例（使用 CE Task ID）：");
    Console.WriteLine("  SonarQubeReport.exe \"squ_xxxxxxxx\" \"Test\" \"C:\\Reports\\Test.xlsx\" \"AYp-i5Y1e3hxfZfWVgZY\"");
    return 1;
}

string token = args[0];
string projectsArg = args[1];
string reportPathArg = args[2];
string? ceTaskId = args.Length > 3 ? args[3] : null;

//string logpath = args.Length > 4 ? args[4] : null;

//FileInfo fileinfo = new FileInfo(logpath);

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

    Console.WriteLine($"SonarQube Server  : {config.BaseUrl}");
    Console.WriteLine($"SonarQube Job        : {string.Join(", ", projects)}");
    foreach (var p in projects)
        Console.WriteLine($"  SonarQube Url     : {config.BaseUrl.TrimEnd('/')}/dashboard?id={p}");
    Console.WriteLine();

    // 如果提供了 CE Task ID，先進行驗證
    if (!string.IsNullOrEmpty(ceTaskId))
    {
        Console.WriteLine($"驗證 CE Task ID: {ceTaskId}");
        bool taskCompleted = await ValidateCETaskAsync(client, ceTaskId);
        
        if (!taskCompleted)
        {
            Console.Error.WriteLine("錯誤：CE Task 未成功完成，中止報表產出。");
            return 4;
        }
        
        Console.WriteLine("✓ CE Task 已成功完成，繼續產出報表。");
        Console.WriteLine();
    }

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

    Console.WriteLine();
    Console.WriteLine($"產出報表: {outputPath}");
    ExcelReportBuilder.Build(outputPath, issues, unconfirmed, ruleLanguages, hotspotRows);

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

/// <summary>
/// 驗證 CE Task 是否成功完成
/// 重試最多 5 次，每次等待 30 秒
/// </summary>
static async Task<bool> ValidateCETaskAsync(SonarQubeClient client, string ceTaskId)
{
    const int maxRetries = 5;
    const int delaySeconds = 30;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            Console.WriteLine($"  檢查 CE Task 狀態... (嘗試 {attempt}/{maxRetries})");
            
            var taskStatus = await client.GetCETaskStatusAsync(ceTaskId);
            
            if (taskStatus == null)
            {
                Console.Error.WriteLine($"  ✗ 無法取得 CE Task {ceTaskId} 的狀態");
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"  等待 {delaySeconds} 秒後重試...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
                continue;
            }

            Console.WriteLine($"  CE Task 狀態: {taskStatus.Status}");
            
            if (taskStatus.Status == "SUCCESS")
            {
                return true;
            }
            else if (taskStatus.Status == "FAILED" || taskStatus.Status == "CANCELED")
            {
                Console.Error.WriteLine($"  ✗ CE Task 狀態異常: {taskStatus.Status}");
                if (!string.IsNullOrEmpty(taskStatus.ErrorMessage))
                {
                    Console.Error.WriteLine($"  錯誤信息: {taskStatus.ErrorMessage}");
                }
                return false;
            }
            else if (taskStatus.Status == "IN_PROGRESS" || taskStatus.Status == "PENDING")
            {
                Console.WriteLine($"  CE Task 仍在執行中...");
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"  等待 {delaySeconds} 秒後重試...");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  ✗ 檢查 CE Task 時發生錯誤: {ex.Message}");
            if (attempt < maxRetries)
            {
                Console.WriteLine($"  等待 {delaySeconds} 秒後重試...");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    Console.Error.WriteLine($"  ✗ 在 {maxRetries} 次重試後，CE Task 仍未成功完成");
    return false;
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

/// <summary>
/// 產生Log檔
/// </summary>
/// <param name="msg">檔案內容</param>
/// <param name="fileinfo"></param>
//static void WriteInLog(string msg, FileInfo fileinfo)
//{
//    if (!Directory.Exists(fileinfo.DirectoryName))
//    {
//        //Log檔案目錄不存在時，創建路徑
//        Directory.CreateDirectory(fileinfo.DirectoryName);
//    }
//    using (FileStream fs = fileinfo.OpenWrite())
//    {
//        StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
//        sw.BaseStream.Seek(0, SeekOrigin.End);
//        sw.Write(msg);
//        sw.Flush();
//        sw.Close();
//    }
//}