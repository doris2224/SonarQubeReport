using System.Text.Json;
using System.Text.Json.Serialization;
using SonarQubeReport.Models;

namespace SonarQubeReport.Models;

/// <summary>
/// JSON 序列化上下文（用於 System.Text.Json 的 Source Generator）
/// 需要針對所有用到的 DTO 類型註冊
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IssuesSearchResponse))]
[JsonSerializable(typeof(HotspotsSearchResponse))]
[JsonSerializable(typeof(RuleShowResponse))]
[JsonSerializable(typeof(HotspotShowResponse))]
[JsonSerializable(typeof(CETaskResponse))]
[JsonSerializable(typeof(CETaskStatus))]
[JsonSerializable(typeof(QualityGate))]
[JsonSerializable(typeof(QualityGateCondition))]
public partial class AppJsonContext : JsonSerializerContext
{
}
