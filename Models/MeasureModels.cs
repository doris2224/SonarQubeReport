using System.Text.Json.Serialization;

namespace SonarQubeReport.Models;

/// <summary>
/// Measure 相關的數據模型
/// 對應 SonarQube API: /api/measures/component
/// </summary>

/// <summary>
/// Measure 回應
/// </summary>
public class MeasuresResponse
{
    [JsonPropertyName("component")]
    public MeasureComponent? Component { get; set; }
}

/// <summary>
/// Measure 組件信息
/// </summary>
public class MeasureComponent
{
    /// <summary>
    /// 組件 Key（通常是項目 Key）
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    /// <summary>
    /// 組件 ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// 組件名稱
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// 組件限定符（TRK=項目, DIR=目錄, FIL=文件 等）
    /// </summary>
    [JsonPropertyName("qualifier")]
    public string? Qualifier { get; set; }

    /// <summary>
    /// 該組件的所有 Measure（指標測量值）
    /// </summary>
    [JsonPropertyName("measures")]
    public List<Measure>? Measures { get; set; }
}

/// <summary>
/// 單一指標測量值
/// </summary>
public class Measure
{
    /// <summary>
    /// 指標的 Key（如 "ncloc", "complexity", "coverage" 等）
    /// </summary>
    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    /// <summary>
    /// 指標的值（通常是字符串形式的數字）
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// 分時期的測量值（用於趨勢分析）
    /// </summary>
    [JsonPropertyName("periods")]
    public List<MeasurePeriod>? Periods { get; set; }

    /// <summary>
    /// 是否為最佳實踐指標（某些版本）
    /// </summary>
    [JsonPropertyName("bestValue")]
    public bool? BestValue { get; set; }
}

/// <summary>
/// Measure 的時期信息（用於變更分析）
/// </summary>
public class MeasurePeriod
{
    /// <summary>
    /// 時期索引
    /// </summary>
    [JsonPropertyName("index")]
    public int? Index { get; set; }

    /// <summary>
    /// 該時期的測量值
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// 時期的模式（如 "previous_version", "last_period" 等）
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>
    /// 時期的日期
    /// </summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }
}

/// <summary>
/// Metrics 工作表的行數據模型（用於 Excel 導出）
/// 將 Measure 列表轉換成平坦的行結構，便於在 Excel 中展示
/// </summary>
public class MetricsRow
{
    /// <summary>
    /// 指標名稱（顯示用的友好名稱）
    /// </summary>
    public string MetricName { get; set; } = "";

    /// <summary>
    /// 指標值
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// 建構子：從 Measure 創建
    /// </summary>
    public MetricsRow() { }

    /// <summary>
    /// 建構子：從 Measure 創建，自動轉換指標 Key 為友好名稱
    /// </summary>
    public MetricsRow(string metricKey, string? value = null)
    {
        MetricName = GetMetricDisplayName(metricKey);
        Value = value ?? "";
    }

    /// <summary>
    /// 將 SonarQube 指標 Key 轉換為人類可讀的顯示名稱
    /// 對齊 sonar-cnes-report 的 StringManager 邏輯
    /// </summary>
    private static string GetMetricDisplayName(string metricKey)
    {
        // 常見的 SonarQube 指標對照表
        // 對齊 sonar-cnes-report 的 messages*.properties 中的定義
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

            // 預設值：使用原始 Key，首字母大寫
            _ => CapitalizeWords(metricKey)
        };
    }

    /// <summary>
    /// 將 snake_case 或 camelCase 轉換為標題格式
    /// 例如: "test_metric" => "Test Metric", "testMetric" => "Test Metric"
    /// </summary>
    private static string CapitalizeWords(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // 先把下劃線替換為空格，再按駝峰式分隔
        var words = new System.Collections.Generic.List<string>();
        var current = new System.Text.StringBuilder();

        foreach (char c in input)
        {
            if (c == '_')
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
            }
            else if (char.IsUpper(c) && current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
                current.Append(c);
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return string.Join(" ", words.Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..] : "")));
    }
}
