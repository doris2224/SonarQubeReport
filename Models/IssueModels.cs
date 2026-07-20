using System.Text.Json;
using System.Text.Json.Serialization;

namespace SonarQubeReport.Models;

/// <summary>對應 GET /api/issues/search 回傳的 JSON 結構。</summary>
public class IssuesSearchResponse
{
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("p")] public int P { get; set; }
    [JsonPropertyName("ps")] public int Ps { get; set; }
    [JsonPropertyName("issues")] public List<SonarIssue> Issues { get; set; } = new();
    [JsonPropertyName("components")] public List<SonarComponent> Components { get; set; } = new();
    [JsonPropertyName("rules")] public List<SonarRule> Rules { get; set; } = new();
}

/// <summary>issues/search 回應中的 "components" 陣列，用來取得每個檔案(component)的語言。</summary>
public class SonarComponent
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("qualifier")] public string? Qualifier { get; set; }
}

/// <summary>
/// issues/search 回應中的 "rules" 陣列（因為請求帶了 additionalFields=_all 才會回傳）。
/// sonar-cnes-report 就是用這裡的 langName、以 issue.rule 對應 rules[].key 取得語言，
/// 而不是用 component 的語言 —— 這才是 Issues/Unconfirmed 頁籤 Language 欄位正確的資料來源。
/// </summary>
public class SonarRule
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("lang")] public string? Lang { get; set; }
    [JsonPropertyName("langName")] public string? LangName { get; set; }
}

/// <summary>
/// 單一 issue 的完整欄位。欄位命名/順序對應範例檔案「All」頁籤的 29 個欄位。
/// 巢狀物件（flows、impacts、textRange...）保留為 JsonElement，寫入 Excel 時再轉成 JSON 字串，
/// 詳見 ExcelReportBuilder。
/// </summary>
public class SonarIssue
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("rule")] public string? Rule { get; set; }
    [JsonPropertyName("severity")] public string? Severity { get; set; }
    [JsonPropertyName("component")] public string? Component { get; set; }
    [JsonPropertyName("project")] public string? Project { get; set; }
    [JsonPropertyName("line")] public int? Line { get; set; }
    [JsonPropertyName("hash")] public string? Hash { get; set; }
    [JsonPropertyName("textRange")] public JsonElement? TextRange { get; set; }
    [JsonPropertyName("flows")] public JsonElement? Flows { get; set; }
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("messageFormattings")] public JsonElement? MessageFormattings { get; set; }
    [JsonPropertyName("effort")] public string? Effort { get; set; }
    [JsonPropertyName("debt")] public string? Debt { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    [JsonPropertyName("tags")] public JsonElement? Tags { get; set; }
    [JsonPropertyName("codeVariants")] public JsonElement? CodeVariants { get; set; }
    [JsonPropertyName("creationDate")] public string? CreationDate { get; set; }
    [JsonPropertyName("updateDate")] public string? UpdateDate { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("comments")] public JsonElement? Comments { get; set; }
    [JsonPropertyName("cleanCodeAttribute")] public string? CleanCodeAttribute { get; set; }
    [JsonPropertyName("cleanCodeAttributeCategory")] public string? CleanCodeAttributeCategory { get; set; }
    [JsonPropertyName("impacts")] public JsonElement? Impacts { get; set; }
    [JsonPropertyName("issueStatus")] public string? IssueStatus { get; set; }
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("externalRuleEngine")] public string? ExternalRuleEngine { get; set; }
    [JsonPropertyName("quickFixAvailable")] public bool? QuickFixAvailable { get; set; }
    [JsonPropertyName("prioritizedRule")] public bool? PrioritizedRule { get; set; }

    // 以下 3 欄是 SonarQube v26.7 之後 /api/issues/search 新增的欄位。
    // sonar-cnes-report 是把整包原始 JSON 攤平寫出，新欄位會自動出現；
    // 本工具用固定 model，所以升級 SonarQube 後要手動補欄位，才能保持兩邊「All」頁籤一致。
    [JsonPropertyName("internalTags")] public JsonElement? InternalTags { get; set; }
    [JsonPropertyName("fromSonarQubeUpdate")] public bool? FromSonarQubeUpdate { get; set; }
    [JsonPropertyName("linkedTicketStatus")] public string? LinkedTicketStatus { get; set; }
}
