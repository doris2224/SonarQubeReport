using System.Text.Json.Serialization;

namespace SonarQubeReport.Models;

/// <summary>對應 GET /api/hotspots/search 回傳的 JSON 結構。</summary>
public class HotspotsSearchResponse
{
    [JsonPropertyName("paging")] public Paging? Paging { get; set; }
    [JsonPropertyName("hotspots")] public List<SonarHotspot> Hotspots { get; set; } = new();
}

public class Paging
{
    [JsonPropertyName("pageIndex")] public int PageIndex { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}

public class SonarHotspot
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("component")] public string? Component { get; set; }
    [JsonPropertyName("project")] public string? Project { get; set; }
    [JsonPropertyName("securityCategory")] public string? SecurityCategory { get; set; }
    [JsonPropertyName("vulnerabilityProbability")] public string? VulnerabilityProbability { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("resolution")] public string? Resolution { get; set; }
    [JsonPropertyName("line")] public int? Line { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("ruleKey")] public string? RuleKey { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("creationDate")] public string? CreationDate { get; set; }
    [JsonPropertyName("updateDate")] public string? UpdateDate { get; set; }
}

/// <summary>對應 GET /api/rules/show?key=... 回傳的 JSON 結構，用來補齊 Hotspot 的 Severity / Language。</summary>
public class RuleShowResponse
{
    [JsonPropertyName("rule")] public RuleDetail? Rule { get; set; }
}

public class RuleDetail
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
    [JsonPropertyName("lang")] public string? Lang { get; set; }
    [JsonPropertyName("langName")] public string? LangName { get; set; }
}

/// <summary>Hotspot 加上其規則詳細資料（Severity / Language 來源）與留言，供 Excel 產表使用。</summary>
public record HotspotRow(SonarHotspot Hotspot, RuleDetail? Rule, string Comments);

/// <summary>
/// 對應 GET /api/hotspots/show?hotspot=&lt;key&gt; 回傳的 JSON 結構。
/// /api/hotspots/search 本身不含留言串，要靠這支逐筆查詢的 API 才拿得到 "comment" 陣列
/// （sonar-cnes-report 的 AbstractSecurityHotspotsProvider 就是這樣做的）。
/// </summary>
public class HotspotShowResponse
{
    [JsonPropertyName("comment")] public List<HotspotComment> Comment { get; set; } = new();
}

public class HotspotComment
{
    [JsonPropertyName("login")] public string? Login { get; set; }
    [JsonPropertyName("htmlText")] public string? HtmlText { get; set; }
    [JsonPropertyName("markdown")] public string? Markdown { get; set; }
    [JsonPropertyName("createdAt")] public string? CreatedAt { get; set; }
}
