using System.Text.Json.Serialization;

namespace SonarQubeReport.Models;

/// <summary>
/// System.Text.Json 的來源產生器 (source generator) context。
/// 發佈為自我包含 (self-contained) / 單一檔案 (PublishSingleFile) / 裁剪 (trimmed) 的 exe 時，
/// .NET 會自動關閉「反射式」JSON 序列化（System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault = false），
/// 直接用 JsonSerializer.Deserialize&lt;T&gt;(json, options) 會丟出：
///   InvalidOperationException: Reflection-based serialization has been disabled for this application.
/// 改用這個來源產生器 context 就不需要依賴反射，任何發佈設定都能正常運作。
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IssuesSearchResponse))]
[JsonSerializable(typeof(HotspotsSearchResponse))]
[JsonSerializable(typeof(RuleShowResponse))]
[JsonSerializable(typeof(HotspotShowResponse))]
[JsonSerializable(typeof(ComponentTreeResponse))]
[JsonSerializable(typeof(TreeComponent))]
[JsonSerializable(typeof(TreeMeasure))]
[JsonSerializable(typeof(PagingInfo))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
