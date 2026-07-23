using System.Text.Json;
using ClosedXML.Excel;
using SonarQubeReport.Models;

namespace SonarQubeReport.Services;

/// <summary>
/// 依照 sonar-cnes-report-5.0.4.jar 的頁籤配置，把抓回來的資料寫成 .xlsx：
/// All (完整原始欄位，resolved=false) / Issues (resolved=false) / Unconfirmed (resolved=true) / 
/// Security Hotspots / Metrics。
/// 欄位與分頁邏輯已對齊 sonar-cnes-report-5.0.4.jar 的實際輸出。
/// </summary>
public static class ExcelReportBuilder
{
    // 對應 sonar-cnes-report「All」頁籤欄位順序（SonarQube v26.7 起共 32 欄，
    // 比原本的 29 欄多了 internalTags / fromSonarQubeUpdate / linkedTicketStatus）
    private static readonly string[] AllHeaders =
    {
        "updateDate", "line", "rule", "project", "effort", "type", "cleanCodeAttribute", "internalTags",
        "issueStatus", "flows", "scope", "externalRuleEngine", "key", "severity", "comments", "author",
        "fromSonarQubeUpdate", "cleanCodeAttributeCategory", "linkedTicketStatus", "messageFormattings",
        "impacts", "message", "creationDate", "quickFixAvailable", "tags", "codeVariants", "component",
        "prioritizedRule", "textRange", "debt", "hash", "status"
    };

    // 對應範例檔「Issues」/「Unconfirmed」頁籤欄位順序
    private static readonly string[] IssueHeaders =
        { "Rule", "Message", "Type", "Severity", "Language", "File", "Line", "Effort", "Status", "Comments" };

    // 對應範例檔「Security Hotspots」頁籤欄位順序
    private static readonly string[] HotspotHeaders =
        { "Rule", "Message", "Category", "Priority", "Severity", "Language", "File", "Line", "Status", "Resolution", "Comments" };

    // Security Hotspot 的 securityCategory 是一個內部代碼（如 "auth"），
    // sonar-cnes-report 的 StringManager 會轉成人看得懂的顯示名稱再輸出，這裡對齊同一份對照表。
    private static readonly Dictionary<string, string> SecurityCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["buffer-overflow"] = "Buffer Overflow",
        ["sql-injection"] = "SQL Injection",
        ["rce"] = "Code Injection (RCE)",
        ["object-injection"] = "Object Injection",
        ["command-injection"] = "Command Injection",
        ["path-traversal-injection"] = "Path Traversal Injection",
        ["ldap-injection"] = "LDAP Injection",
        ["xpath-injection"] = "XPath Injection",
        ["log-injection"] = "Log Injection",
        ["xxe"] = "XML External Entity (XXE)",
        ["xss"] = "Cross-Site Scripting (XSS)",
        ["dos"] = "Denial of Service (DoS)",
        ["ssrf"] = "Server-Side Request Forgery (SSRF)",
        ["csrf"] = "Cross-Site Request Forgery (CSRF)",
        ["http-response-splitting"] = "HTTP Response Splitting",
        ["open-redirect"] = "Open Redirect",
        ["weak-cryptography"] = "Weak Cryptography",
        ["auth"] = "Authentication",
        ["insecure-conf"] = "Insecure Configuration",
        ["file-manipulation"] = "File Manipulation",
        ["others"] = "Others",
        ["permission"] = "Permission",
        ["encrypt-data"] = "Encryption of Sensitive Data",
        ["traceability"] = "Traceability",
    };

    public static void Build(
        string outputPath,
        List<SonarIssue> issues,
        List<SonarIssue> unconfirmedIssues,
        Dictionary<string, string> ruleLanguages,
        List<HotspotRow> hotspots,
        List<Measure>? measures = null)
    {
        using var workbook = new XLWorkbook();

        // 頁籤順序依照需求：All、Issues、Unconfirmed、Security Hotspots、Metrics
        // 「All」跟「Issues」用同一份資料（resolved=false），只是欄位不同——
        // 這對齊 sonar-cnes-report 的行為：它的「All」頁籤也是 resolved=false 查詢的原始 JSON，
        // 不是 Issues + Unconfirmed 的聯集。
        BuildAllSheet(workbook.Worksheets.Add("All"), issues);
        BuildIssueSheet(workbook.Worksheets.Add("Issues"), issues, ruleLanguages);
        BuildIssueSheet(workbook.Worksheets.Add("Unconfirmed"), unconfirmedIssues, ruleLanguages);
        BuildHotspotSheet(workbook.Worksheets.Add("Security Hotspots"), hotspots);
        
        // 新增 Metrics 頁籤
        if (measures != null && measures.Count > 0)
        {
            BuildMetricsSheet(workbook.Worksheets.Add("Metrics"), measures);
        }

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        workbook.SaveAs(outputPath);
    }

    private static void BuildAllSheet(IXLWorksheet ws, List<SonarIssue> issues)
    {
        WriteHeader(ws, AllHeaders);

        int row = 2;
        foreach (var i in issues)
        {
            int col = 1;
            SetCell(ws, row, col++, i.UpdateDate ?? "");
            SetCell(ws, row, col++, i.Line?.ToString() ?? "");
            SetCell(ws, row, col++, i.Rule ?? "");
            SetCell(ws, row, col++, i.Project ?? "");
            SetCell(ws, row, col++, i.Effort ?? "");
            SetCell(ws, row, col++, i.Type ?? "");
            SetCell(ws, row, col++, i.CleanCodeAttribute ?? "");
            SetCell(ws, row, col++, ToJson(i.InternalTags));
            SetCell(ws, row, col++, i.IssueStatus ?? "");
            SetCell(ws, row, col++, ToJson(i.Flows));
            SetCell(ws, row, col++, i.Scope ?? "");
            SetCell(ws, row, col++, i.ExternalRuleEngine ?? "");
            SetCell(ws, row, col++, i.Key ?? "");
            SetCell(ws, row, col++, i.Severity ?? "");
            SetCell(ws, row, col++, ToJson(i.Comments));
            SetCell(ws, row, col++, i.Author ?? "");
            SetCell(ws, row, col++, i.FromSonarQubeUpdate?.ToString() ?? "");
            SetCell(ws, row, col++, i.CleanCodeAttributeCategory ?? "");
            SetCell(ws, row, col++, i.LinkedTicketStatus ?? "");
            SetCell(ws, row, col++, ToJson(i.MessageFormattings));
            SetCell(ws, row, col++, ToJson(i.Impacts));
            SetCell(ws, row, col++, i.Message ?? "");
            SetCell(ws, row, col++, i.CreationDate ?? "");
            SetCell(ws, row, col++, i.QuickFixAvailable?.ToString() ?? "");
            SetCell(ws, row, col++, ToJson(i.Tags));
            SetCell(ws, row, col++, ToJson(i.CodeVariants));
            SetCell(ws, row, col++, i.Component ?? "");
            SetCell(ws, row, col++, i.PrioritizedRule?.ToString() ?? "");
            SetCell(ws, row, col++, ToJson(i.TextRange));
            SetCell(ws, row, col++, i.Debt ?? "");
            SetCell(ws, row, col++, i.Hash ?? "");
            SetCell(ws, row, col++, i.Status ?? "");
            row++;
        }

        FinalizeSheet(ws, AllHeaders.Length);
    }

    private static void BuildIssueSheet(IXLWorksheet ws, List<SonarIssue> issues, Dictionary<string, string> ruleLanguages)
    {
        WriteHeader(ws, IssueHeaders);

        int row = 2;
        foreach (var i in issues)
        {
            // Language 是用 issue 的 rule 去對應 rules[].langName（來自 /api/issues/search 回應本身），
            // 不是用 component 的語言——SonarQube 的 issue 物件不保證帶語言，rule 才有穩定的語言歸屬。
            var language = i.Rule != null && ruleLanguages.TryGetValue(i.Rule, out var lang)
                ? lang
                : "";

            int col = 1;
            SetCell(ws, row, col++, i.Rule ?? "");
            SetCell(ws, row, col++, i.Message ?? "");
            SetCell(ws, row, col++, i.Type ?? "");
            SetCell(ws, row, col++, i.Severity ?? "");
            SetCell(ws, row, col++, language);
            SetCell(ws, row, col++, i.Component ?? "");
            SetCell(ws, row, col++, i.Line?.ToString() ?? "");
            SetCell(ws, row, col++, i.Effort ?? "");
            SetCell(ws, row, col++, i.Status ?? "");
            SetCell(ws, row, col++, JoinComments(i.Comments));
            row++;
        }

        FinalizeSheet(ws, IssueHeaders.Length);
    }

    private static void BuildHotspotSheet(IXLWorksheet ws, List<HotspotRow> hotspots)
    {
        WriteHeader(ws, HotspotHeaders);

        int row = 2;
        foreach (var hr in hotspots)
        {
            var h = hr.Hotspot;
            var categoryName = h.SecurityCategory != null && SecurityCategoryNames.TryGetValue(h.SecurityCategory, out var name)
                ? name
                : h.SecurityCategory ?? "";

            int col = 1;
            SetCell(ws, row, col++, h.RuleKey ?? "");
            SetCell(ws, row, col++, h.Message ?? "");
            SetCell(ws, row, col++, categoryName);
            SetCell(ws, row, col++, h.VulnerabilityProbability ?? "");
            SetCell(ws, row, col++, hr.Rule?.Severity ?? "");
            SetCell(ws, row, col++, hr.Rule?.LangName ?? hr.Rule?.Lang ?? "");
            SetCell(ws, row, col++, h.Component ?? "");
            SetCell(ws, row, col++, h.Line?.ToString() ?? "");
            SetCell(ws, row, col++, h.Status ?? "");
            SetCell(ws, row, col++, h.Resolution ?? "");
            // 留言透過 /api/hotspots/show 逐筆查詢取得（見 SonarQubeClient.GetHotspotCommentsAsync）
            SetCell(ws, row, col++, hr.Comments);
            row++;
        }

        FinalizeSheet(ws, HotspotHeaders.Length);
    }

    private static void BuildMetricsSheet(IXLWorksheet ws, List<Measure> measures)
    {
        // Metrics 頁籤頭：Metric Name 和 Value
        string[] headers = { "Metric Name", "Value" };
        WriteHeader(ws, headers);

        int row = 2;
        foreach (var measure in measures)
        {
            if (string.IsNullOrEmpty(measure.Metric))
                continue;

            int col = 1;
            // 將指標 Key 轉換為人類可讀的名稱
            SetCell(ws, row, col++, GetMetricDisplayName(measure.Metric));
            // 指標值
            SetCell(ws, row, col++, measure.Value ?? "");

            row++;
        }

        // 設置表格格式
        var table = ws.Range(1, 1, row - 1, 2).CreateTable("metrics");
        table.ShowAutoFilter = true;
        table.Theme = XLTableTheme.TableStyleMedium2;

        // 調整列寬
        ws.Column(1).Width = 35;
        ws.Column(2).Width = 20;
    }

    /// <summary>
    /// 將 SonarQube 指標 Key 轉換為人類可讀的顯示名稱
    /// 對齊 sonar-cnes-report 的 StringManager 邏輯
    /// </summary>
    private static string GetMetricDisplayName(string metricKey)
    {
        return metricKey switch
        {
            // 代碼行數相關
            "ncloc" => "Lines of Code",
            "lines" => "Lines",
            "comment_lines" => "Comment Lines",
            "comment_lines_density" => "Comment Lines Density",

            // 複雜度相關
            "complexity" => "Complexity",
            "cognitive_complexity" => "Cognitive Complexity",
            "file_complexity" => "File Complexity",
            "function_complexity" => "Function Complexity",
            "class_complexity" => "Class Complexity",

            // 覆蓋率相關
            "coverage" => "Coverage",
            "line_coverage" => "Line Coverage",
            "branch_coverage" => "Branch Coverage",
            "conditions_coverage" => "Conditions Coverage",

            // 重複代碼
            "duplicated_lines" => "Duplicated Lines",
            "duplicated_lines_density" => "Duplicated Lines Density",
            "duplicated_blocks" => "Duplicated Blocks",
            "duplicated_files" => "Duplicated Files",

            // 問題/缺陷相關
            "bugs" => "Bugs",
            "vulnerabilities" => "Vulnerabilities",
            "code_smells" => "Code Smells",
            "security_hotspots" => "Security Hotspots",
            "violations" => "Violations",
            "blocker_violations" => "Blocker Violations",
            "critical_violations" => "Critical Violations",
            "major_violations" => "Major Violations",
            "minor_violations" => "Minor Violations",
            "info_violations" => "Info Violations",

            // 測試相關
            "tests" => "Tests",
            "test_success_density" => "Test Success Density",
            "test_execution_time" => "Test Execution Time",
            "test_failures" => "Test Failures",
            "test_errors" => "Test Errors",
            "test_skipped" => "Test Skipped",

            // 技術債務
            "sqale_index" => "Technical Debt",
            "sqale_rating" => "Maintainability Rating",
            "sqale_debt_ratio" => "Technical Debt Ratio",

            // 品質指標
            "alert_status" => "Quality Gate Status",
            "quality_gate_details" => "Quality Gate Details",

            // 安全相關
            "security_rating" => "Security Rating",
            "reliability_rating" => "Reliability Rating",
            "maintainability_rating" => "Maintainability Rating",

            // 新代碼相關
            "new_lines" => "New Lines",
            "new_code_smells" => "New Code Smells",
            "new_bugs" => "New Bugs",
            "new_vulnerabilities" => "New Vulnerabilities",
            "new_coverage" => "New Coverage",
            "new_line_coverage" => "New Line Coverage",
            "new_branch_coverage" => "New Branch Coverage",
            "new_duplicated_lines" => "New Duplicated Lines",
            "new_violations" => "New Violations",

            // 預設值：將 snake_case 轉換為標題格式
            _ => CamelCaseToTitle(metricKey)
        };
    }

    /// <summary>
    /// 將 snake_case 轉換為標題格式
    /// 例如: "test_metric" => "Test Metric"
    /// </summary>
    private static string CamelCaseToTitle(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        var words = input.Split('_', '-');
        return string.Join(" ", words.Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "")));
    }

    private static void WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];

        var headerRow = ws.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD9, 0xE1, 0xF2);
        ws.SheetView.FreezeRows(1);
    }

    private static void FinalizeSheet(IXLWorksheet ws, int columnCount)
    {
        ws.Style.Font.FontName = "Arial";

        var usedRange = ws.RangeUsed();
        if (usedRange != null)
        {
            usedRange.SetAutoFilter();
            ws.Columns(1, columnCount).AdjustToContents();
        }
    }

    // Excel 單一儲存格最多只能容納 32,767 個字元，超過就會在寫入時丟例外。
    private const int ExcelMaxCellLength = 32767;
    private const string TruncationSuffix = " …[內容過長，已截斷；完整內容請至 SonarQube 網頁查看]";

    private static string Clamp(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        if (value.Length <= ExcelMaxCellLength)
            return value;

        int keep = Math.Max(0, ExcelMaxCellLength - TruncationSuffix.Length);
        return value.Substring(0, keep) + TruncationSuffix;
    }

    private static void SetCell(IXLWorksheet ws, int row, int col, string? value)
        => ws.Cell(row, col).Value = Clamp(value);

    private static string ToJson(JsonElement? el)
    {
        if (el is null || el.Value.ValueKind == JsonValueKind.Undefined)
            return "";
        return el.Value.GetRawText();
    }

    private static string JoinComments(JsonElement? comments)
    {
        if (comments is null || comments.Value.ValueKind != JsonValueKind.Array)
            return "";

        var parts = new List<string>();
        foreach (var c in comments.Value.EnumerateArray())
        {
            if (c.TryGetProperty("markdown", out var md) && md.ValueKind == JsonValueKind.String)
                parts.Add(md.GetString() ?? "");
            else if (c.TryGetProperty("htmlText", out var ht) && ht.ValueKind == JsonValueKind.String)
                parts.Add(ht.GetString() ?? "");
        }

        return string.Join(" | ", parts);
    }
}
