using System.Text.Json.Serialization;

namespace SonarQubeReport.Models;

/// <summary>
/// CE Task 相關的數據模型
/// 對應 SonarQube API: /api/ce/task
/// </summary>

/// <summary>
/// CE Task 狀態回應
/// </summary>
public class CETaskResponse
{
    [JsonPropertyName("task")]
    public CETaskStatus? Task { get; set; }
}

/// <summary>
/// CE Task 狀態信息
/// </summary>
public class CETaskStatus
{
    /// <summary>
    /// Task ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Task 狀態：
    /// - PENDING：等待執行
    /// - IN_PROGRESS：執行中
    /// - SUCCESS：成功
    /// - FAILED：失敗
    /// - CANCELED：已取消
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Task 類型
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// 完成時間戳記（毫秒）
    /// </summary>
    [JsonPropertyName("executedAt")]
    public string? ExecutedAt { get; set; }

    /// <summary>
    /// 執行時間（毫秒）
    /// </summary>
    [JsonPropertyName("executionTimeMs")]
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// 錯誤信息（當狀態為 FAILED 時）
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Task 的執行進度
    /// </summary>
    [JsonPropertyName("progress")]
    public string? Progress { get; set; }

    /// <summary>
    /// 是否還在執行中
    /// </summary>
    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; }

    /// <summary>
    /// 分析相關的組件 ID（Component）
    /// </summary>
    [JsonPropertyName("componentId")]
    public string? ComponentId { get; set; }

    /// <summary>
    /// 分析相關的組件 Key（Project Key）
    /// </summary>
    [JsonPropertyName("componentKey")]
    public string? ComponentKey { get; set; }

    /// <summary>
    /// 開始時間戳記（毫秒）
    /// </summary>
    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    /// <summary>
    /// 警告信息
    /// </summary>
    [JsonPropertyName("warningCount")]
    public int? WarningCount { get; set; }

    /// <summary>
    /// 分析結果的質量 Gate 狀態
    /// </summary>
    [JsonPropertyName("qualityGate")]
    public QualityGate? QualityGate { get; set; }
}

/// <summary>
/// 質量 Gate 信息
/// </summary>
public class QualityGate
{
    /// <summary>
    /// 質量 Gate 狀態：OK、WARN、ERROR
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// 條件詳情
    /// </summary>
    [JsonPropertyName("conditions")]
    public List<QualityGateCondition>? Conditions { get; set; }
}

/// <summary>
/// 質量 Gate 條件
/// </summary>
public class QualityGateCondition
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("metricKey")]
    public string? MetricKey { get; set; }

    [JsonPropertyName("metricName")]
    public string? MetricName { get; set; }

    [JsonPropertyName("comparator")]
    public string? Comparator { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("errorThreshold")]
    public string? ErrorThreshold { get; set; }

    [JsonPropertyName("warningThreshold")]
    public string? WarningThreshold { get; set; }

    [JsonPropertyName("periodIndex")]
    public int? PeriodIndex { get; set; }
}
