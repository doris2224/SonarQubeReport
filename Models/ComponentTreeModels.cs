using System.Text.Json;
using System.Text.Json.Serialization;

namespace SonarQubeReport.Models;

/// <summary>
/// 组件树（Component Tree）相关数据模型
/// 对应 SonarQube API: /api/measures/component_tree
/// 用于生成 Metrics 工作表（组件矩阵）
/// </summary>

public class ComponentTreeResponse
{
    [JsonPropertyName("paging")]
    public PagingInfo? Paging { get; set; }

    [JsonPropertyName("components")]
    public List<TreeComponent>? Components { get; set; }
}

public class PagingInfo
{
    [JsonPropertyName("pageIndex")]
    public int PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class TreeComponent
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("qualifier")]
    public string? Qualifier { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("measures")]
    public List<TreeMeasure>? Measures { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    /// <summary>
    /// 转换为字典格式（Excel 导出）
    /// 对齐 sonar-cnes-report 的 Component.toMap() 逻辑
    /// </summary>
    public Dictionary<string, string?> ToMap()
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            { "ID", Id ?? "" },
            { "Name", Name ?? "" },
            { "Path", Path ?? "" }
        };

        if (Measures != null)
        {
            foreach (var measure in Measures)
            {
                if (!string.IsNullOrEmpty(measure.Metric))
                {
                    map[measure.Metric] = measure.Value;
                }
            }
        }

        return map;
    }
}

public class TreeMeasure
{
    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("bestValue")]
    public bool? BestValue { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
