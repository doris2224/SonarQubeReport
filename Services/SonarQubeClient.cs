using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SonarQubeReport.Models;

namespace SonarQubeReport.Services;

/// <summary>
/// 呼叫 SonarQube Web API 的簡易 client。
/// 認證方式採用 HTTP Basic Auth，帳號放 User Token、密碼留空，
/// 這是新舊版 SonarQube / SonarQube Server 都相容的作法。
/// （較新版本亦支援 "Authorization: Bearer &lt;token&gt;"，若貴公司環境已停用 Basic Auth，
/// 可改用下方註解的 Bearer 寫法。）
/// </summary>
public class SonarQubeClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly int _pageSize;
    private readonly Dictionary<string, RuleDetail?> _ruleCache = new();

    public SonarQubeClient(string baseUrl, string token, int pageSize)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        // Basic Auth: username = token, password = 空字串
        var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{token}:"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

        // 若改用 Bearer Token 認證，改成：
        // _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _pageSize = pageSize <= 0 ? 500 : Math.Min(pageSize, 500); // SonarQube 單頁上限為 500

        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // SonarQube /api/issues/search 的死限制：不管怎麼分頁，p*ps 最多只能到 10000（超過就回 400）。
    private const int MaxSearchWindow = 10000;

    private static readonly string[] IssueTypesForSplit = { "BUG", "VULNERABILITY", "CODE_SMELL" };
    private static readonly string[] SeveritiesForSplit = { "BLOCKER", "CRITICAL", "MAJOR", "MINOR", "INFO" };

    // 遞迴切分時使用的日期範圍上下限（早於任何專案建立時間 / 晚於現在皆可，只是安全的邊界值）。
    private static readonly DateTimeOffset EarliestPossibleDate = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset LatestPossibleDate => DateTimeOffset.UtcNow.AddDays(2);

    /// <summary>
    /// 檢查 CE Task 的執行狀態
    /// 對應 SonarQube API: /api/ce/task
    /// </summary>
    public async Task<CETaskStatus?> GetCETaskStatusAsync(string ceTaskId)
    {
        string sbody = "";
        try
        {
            var url = $"api/ce/task?id={Uri.EscapeDataString(ceTaskId)}";
            var body = await GetStringAsync(url);
            sbody = body;
            var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.CETaskResponse);
            return result?.Task;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"取得 CE Task 狀態失敗: {ex.Message}");
            Console.Error.WriteLine($"body: {sbody}");
            return null;
        }
    }

    /// <summary>
    /// 抓取指定專案(可多個)、指定 resolved 狀態的 issue。
    ///
    /// 對應 sonar-cnes-report (AbstractIssuesProvider) 的作法：
    /// Issues 頁籤／All 頁籤都是 resolved=false 的查詢結果；Unconfirmed 頁籤是 resolved=true
    /// 的查詢結果——這是在 SonarQube 伺服器端用 /api/issues/search 的 resolved 參數篩選，
    /// 不是抓一次全部再用本地端的 status 欄位去分類，兩者在有大量歷史已解決 issue 時
    /// 結果會不一致（本地端用 status 分類抓到的是「目前狀態」，伺服器端的 resolved
    /// 篩的是「是否已被標記解決」，兩個欄位語意不同）。
    ///
    /// 回傳值同時附上 rule -&gt; 語言顯示名稱 (langName) 對照表，來源是回應中的 "rules" 陣列
    /// （因為請求帶了 additionalFields=_all）。sonar-cnes-report 就是拿這個表、用
    /// issue.rule 去對應 rules[].key 取得語言，用來填 Issues/Unconfirmed 頁籤的 Language 欄位。
    ///
    /// 因為 SonarQube 的 /api/issues/search 規定「p * ps」最多只能到 10000（10000 之後一律回 400），
    /// 當單一次查詢的 issue 總數超過一萬筆時，改成依「issue 類型 -&gt; 嚴重度 -&gt; 建立日期區間」遞迴切分，
    /// 每一段切分後的查詢結果數都會落在 10000 以內，最後再把所有切分查詢的結果合併起來。
    /// </summary>
    public async Task<(List<SonarIssue> Issues, Dictionary<string, string> RuleLanguages)> SearchIssuesAsync(
        IEnumerable<string> projectKeys, bool resolved)
    {
        var componentKeys = string.Join(",", projectKeys);
        var ruleLanguages = new Dictionary<string, string>();

        var issues = await FetchIssuesWithSplitAsync(
            componentKeys, resolved, type: null, severity: null,
            createdAfter: null, createdBefore: null,
            ruleLanguages);

        return (issues, ruleLanguages);
    }

    private async Task<List<SonarIssue>> FetchIssuesWithSplitAsync(
        string componentKeys, bool resolved, string? type, string? severity,
        DateTimeOffset? createdAfter, DateTimeOffset? createdBefore,
        Dictionary<string, string> ruleLanguages)
    {
        int total = await GetIssuesTotalAsync(componentKeys, resolved, type, severity, createdAfter, createdBefore);
        if (total == 0)
            return new List<SonarIssue>();

        if (total <= MaxSearchWindow)
        {
            return await FetchIssuesPagesAsync(
                componentKeys, resolved, type, severity, createdAfter, createdBefore, total, ruleLanguages);
        }

        // 超過一萬筆，需要再切分。優先順序：type -> severity -> 建立日期區間（二分）
        if (type is null)
        {
            var result = new List<SonarIssue>();
            foreach (var t in IssueTypesForSplit)
                result.AddRange(await FetchIssuesWithSplitAsync(componentKeys, resolved, t, severity, createdAfter, createdBefore, ruleLanguages));
            return result;
        }

        if (severity is null)
        {
            var result = new List<SonarIssue>();
            foreach (var s in SeveritiesForSplit)
                result.AddRange(await FetchIssuesWithSplitAsync(componentKeys, resolved, type, s, createdAfter, createdBefore, ruleLanguages));
            return result;
        }

        // 同一個 type + severity 底下仍然超過一萬筆，改用建立日期二分切分
        var start = createdAfter ?? EarliestPossibleDate;
        var end = createdBefore ?? LatestPossibleDate;

        if (end - start < TimeSpan.FromSeconds(2))
        {
            // 已經切到秒級還是超過一萬筆（極端狀況），只能盡力而為，先抓前 10000 筆並提出警告。
            Console.Error.WriteLine(
                $"警告：{componentKeys} 在 resolved={resolved}, type={type}, severity={severity} 且時間區間已無法再切分的情況下，" +
                $"仍有 {total} 筆 issue，僅能取得前 {MaxSearchWindow} 筆，可能有資料遺漏。");
            return await FetchIssuesPagesAsync(
                componentKeys, resolved, type, severity, start, end, MaxSearchWindow, ruleLanguages);
        }

        var mid = start + TimeSpan.FromTicks((end - start).Ticks / 2);

        var left = await FetchIssuesWithSplitAsync(componentKeys, resolved, type, severity, start, mid, ruleLanguages);
        var right = await FetchIssuesWithSplitAsync(componentKeys, resolved, type, severity, mid, end, ruleLanguages);
        left.AddRange(right);
        return left;
    }

    private async Task<int> GetIssuesTotalAsync(
        string componentKeys, bool resolved, string? type, string? severity,
        DateTimeOffset? createdAfter, DateTimeOffset? createdBefore)
    {
        var url = BuildIssuesSearchUrl(componentKeys, resolved, type, severity, createdAfter, createdBefore, page: 1, pageSize: 1);
        var body = await GetStringAsync(url);
        var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.IssuesSearchResponse);
        return result?.Total ?? 0;
    }

    private async Task<List<SonarIssue>> FetchIssuesPagesAsync(
        string componentKeys, bool resolved, string? type, string? severity,
        DateTimeOffset? createdAfter, DateTimeOffset? createdBefore,
        int total, Dictionary<string, string> ruleLanguages)
    {
        var issues = new List<SonarIssue>();
        int page = 1;

        while (true)
        {
            var url = BuildIssuesSearchUrl(componentKeys, resolved, type, severity, createdAfter, createdBefore, page, _pageSize);
            var body = await GetStringAsync(url);

            var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.IssuesSearchResponse)
                         ?? new IssuesSearchResponse();

            issues.AddRange(result.Issues);

            // 用 rules[] 建立 rule -> 語言顯示名稱 對照表（跟 sonar-cnes-report 的 setIssuesLanguage 邏輯相同）。
            foreach (var r in result.Rules)
            {
                if (!string.IsNullOrEmpty(r.Key) && !string.IsNullOrEmpty(r.LangName))
                    ruleLanguages[r.Key] = r.LangName!;
            }

            if (result.Issues.Count == 0 || result.Issues.Count < _pageSize || page * _pageSize >= total)
                break;

            page++;
        }

        return issues;
    }

    private static string BuildIssuesSearchUrl(
        string componentKeys, bool resolved, string? type, string? severity,
        DateTimeOffset? createdAfter, DateTimeOffset? createdBefore,
        int page, int pageSize)
    {
        var url = $"api/issues/search?componentKeys={Uri.EscapeDataString(componentKeys)}" +
                   $"&resolved={(resolved ? "true" : "false")}&ps={pageSize}&p={page}&additionalFields=_all";

        if (!string.IsNullOrEmpty(type))
            url += $"&types={Uri.EscapeDataString(type)}";

        if (!string.IsNullOrEmpty(severity))
            url += $"&severities={Uri.EscapeDataString(severity)}";

        if (createdAfter.HasValue)
            url += $"&createdAfter={Uri.EscapeDataString(FormatSonarDate(createdAfter.Value))}";

        if (createdBefore.HasValue)
            url += $"&createdBefore={Uri.EscapeDataString(FormatSonarDate(createdBefore.Value))}";

        return url;
    }

    // SonarQube 接受的日期時間格式，例如 2017-10-19T13:00:00+0200（時區偏移不能有冒號）。
    private static string FormatSonarDate(DateTimeOffset dt)
    {
        var utc = dt.ToUniversalTime();
        return utc.ToString("yyyy-MM-ddTHH:mm:ss") + "+0000";
    }

    /// <summary>抓取指定「單一」專案的 Security Hotspots。</summary>
    public async Task<List<SonarHotspot>> SearchHotspotsAsync(string projectKey, string status)
    {
        var hotspots = new List<SonarHotspot>();
        int page = 1;

        while (true)
        {
            var statusParam = string.IsNullOrWhiteSpace(status) || status.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"&status={Uri.EscapeDataString(status)}";

            var url = $"api/hotspots/search?projectKey={Uri.EscapeDataString(projectKey)}&ps={_pageSize}&p={page}{statusParam}";
            var body = await GetStringAsync(url);

            var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.HotspotsSearchResponse)
                         ?? new HotspotsSearchResponse();

            hotspots.AddRange(result.Hotspots);

            int total = result.Paging?.Total ?? result.Hotspots.Count;
            if (result.Hotspots.Count == 0 || result.Hotspots.Count < _pageSize || page * _pageSize >= total)
                break;

            page++;
        }

        return hotspots;
    }

    /// <summary>
    /// 查詢規則詳細資料，用來補齊 Hotspot 的 Severity 與 Language（Hotspot 本身的 API 不含這兩個欄位）。
    /// 有做記憶體快取，同一個 ruleKey 只會呼叫一次 API。
    /// </summary>
    public async Task<RuleDetail?> GetRuleDetailAsync(string? ruleKey)
    {
        if (string.IsNullOrEmpty(ruleKey)) return null;
        if (_ruleCache.TryGetValue(ruleKey, out var cached)) return cached;

        try
        {
            var url = $"api/rules/show?key={Uri.EscapeDataString(ruleKey)}";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _ruleCache[ruleKey] = null;
                return null;
            }

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.RuleShowResponse);
            _ruleCache[ruleKey] = result?.Rule;
            return result?.Rule;
        }
        catch
        {
            _ruleCache[ruleKey] = null;
            return null;
        }
    }

    /// <summary>
    /// 取得單一 Security Hotspot 的留言串，串接成一段文字。
    /// /api/hotspots/search 不會回傳留言，要靠這支逐筆查詢的 /api/hotspots/show 才拿得到
    /// （對應 sonar-cnes-report 的 AbstractSecurityHotspotsProvider 作法）。
    /// 逐筆查詢會增加 API 呼叫次數，hotspot 數量大時執行時間會拉長。
    /// </summary>
    public async Task<string> GetHotspotCommentsAsync(string hotspotKey)
    {
        try
        {
            var url = $"api/hotspots/show?hotspot={Uri.EscapeDataString(hotspotKey)}";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return string.Empty;

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize(body, AppJsonContext.Default.HotspotShowResponse);
            if (result is null || result.Comment.Count == 0)
                return string.Empty;

            var parts = result.Comment
                .Select(c => !string.IsNullOrEmpty(c.Markdown) ? c.Markdown : c.HtmlText)
                .Where(t => !string.IsNullOrEmpty(t));

            return string.Join(" | ", parts);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task<string> GetStringAsync(string relativeUrl)
    {
        var response = await _http.GetAsync(relativeUrl);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"呼叫 SonarQube API 失敗 ({(int)response.StatusCode} {response.StatusCode}): {relativeUrl}\n{errorBody}");
        }
        return await response.Content.ReadAsStringAsync();
    }

    public void Dispose() => _http.Dispose();
}
