// AI/PointTagParser.cs
// Parses [POINT:x,y:label:screenN] tags that Claude embeds in responses
// to indicate which UI element to point at.
//
// Tag format: [POINT:x,y:label:screenN]
//   x, y   — coordinates as fractions of the display (0.0–1.0)
//   label  — human-readable element description
//   screenN — "screen0", "screen1", etc. (0 = cursor screen)
//
// Port of the POINT tag parsing in CompanionManager.swift.

using System.Text.RegularExpressions;

namespace ClickyWindows;

internal record PointTag(
    double X,          // 0.0–1.0 fraction of display width
    double Y,          // 0.0–1.0 fraction of display height
    string Label,
    int ScreenIndex    // 0 = cursor screen
);

internal static class PointTagParser
{
    // Matches: [POINT:0.45,0.62:Start button:screen0]
    private static readonly Regex TagPattern = new Regex(
        @"\[POINT:(?<x>[\d.]+),(?<y>[\d.]+):(?<label>[^:]+):screen(?<screen>\d+)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Fallback: [POINT:0.45,0.62:Start button] (no screen index)
    private static readonly Regex TagPatternNoScreen = new Regex(
        @"\[POINT:(?<x>[\d.]+),(?<y>[\d.]+):(?<label>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts the first POINT tag from Claude's response text.
    /// Returns null if no tag is found.
    /// </summary>
    public static PointTag? ExtractFirstTag(string responseText)
    {
        var match = TagPattern.Match(responseText);
        if (match.Success)
        {
            return new PointTag(
                X: double.Parse(match.Groups["x"].Value),
                Y: double.Parse(match.Groups["y"].Value),
                Label: match.Groups["label"].Value.Trim(),
                ScreenIndex: int.Parse(match.Groups["screen"].Value)
            );
        }

        // Try fallback format
        match = TagPatternNoScreen.Match(responseText);
        if (match.Success)
        {
            return new PointTag(
                X: double.Parse(match.Groups["x"].Value),
                Y: double.Parse(match.Groups["y"].Value),
                Label: match.Groups["label"].Value.Trim(),
                ScreenIndex: 0
            );
        }

        return null;
    }

    /// <summary>
    /// Returns the response text with all POINT tags stripped out.
    /// Used for display and TTS (the cursor handles pointing visually).
    /// </summary>
    public static string StripAllTags(string responseText)
    {
        var stripped = TagPattern.Replace(responseText, "");
        stripped = TagPatternNoScreen.Replace(stripped, "");
        return stripped.Trim();
    }
}
