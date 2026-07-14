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

/// <summary>Hotspot 加上其規則詳細資料（Severity / Language 來源），供 Excel 產表使用。</summary>
public record HotspotRow(SonarHotspot Hotspot, RuleDetail? Rule);
