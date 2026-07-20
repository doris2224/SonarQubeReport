using System.Text.Json;
using ClosedXML.Excel;
using SonarQubeReport.Models;

namespace SonarQubeReport.Services;

/// <summary>
/// 依照 SonarQubeReportExample.xlsx 的頁籤配置，把抓回來的資料寫成 .xlsx：
/// All (完整原始欄位，resolved=false) / Issues (resolved=false) / Unconfirmed (resolved=true) / Security Hotspots。
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
        List<HotspotRow> hotspots)
    {
        using var workbook = new XLWorkbook();

        // 頁籤順序依照需求：All、Issues、Unconfirmed、Security Hotspots
        // 「All」跟「Issues」用同一份資料（resolved=false），只是欄位不同——
        // 這對齊 sonar-cnes-report 的行為：它的「All」頁籤也是 resolved=false 查詢的原始 JSON，
        // 不是 Issues + Unconfirmed 的聯集。
        BuildAllSheet(workbook.Worksheets.Add("All"), issues);
        BuildIssueSheet(workbook.Worksheets.Add("Issues"), issues, ruleLanguages);
        BuildIssueSheet(workbook.Worksheets.Add("Unconfirmed"), unconfirmedIssues, ruleLanguages);
        BuildHotspotSheet(workbook.Worksheets.Add("Security Hotspots"), hotspots);

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
