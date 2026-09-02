using System.Text.Json.Serialization;

namespace ClaudeQuotaTracker;

/// <summary>
/// Mirrors the JSON the PowerShell bridge script writes to
/// %AppData%\ClaudeQuotaTracker\snapshot.json. Field names use camelCase to
/// match the bridge script's ConvertTo-Json output.
/// </summary>
public sealed class SnapshotData
{
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("sourceSessionId")]
    public string? SourceSessionId { get; set; }

    [JsonPropertyName("fiveHour")]
    public RateWindow? FiveHour { get; set; }

    [JsonPropertyName("sevenDay")]
    public RateWindow? SevenDay { get; set; }
}

public sealed class RateWindow
{
    [JsonPropertyName("usedPercentage")]
    public double UsedPercentage { get; set; }

    [JsonPropertyName("resetsAt")]
    public DateTimeOffset ResetsAt { get; set; }
}
