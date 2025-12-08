using System;

namespace SubtitleReader.Models;

/// <summary>
/// Запись в истории распознанного текста
/// </summary>
public sealed class TextHistoryEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string RegionName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public bool WasRead { get; init; }

    public string FormattedTime => Timestamp.ToString("HH:mm:ss");
}
