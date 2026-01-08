using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Helpers;

public static class MetricColorHelper
{
    /// <summary>
    /// Get MudBlazor Color for a score on 1-5 scale.
    /// 1=Red, 2=Orange-ish, 3=Yellow, 4=Info/Teal, 5=Green
    /// </summary>
    public static Color GetScoreColor(double score)
    {
        return score switch
        {
            >= 4.5 => Color.Success, // 5: Green
            >= 3.5 => Color.Info, // 4: Teal/Info (no yellowgreen in MudBlazor)
            >= 2.5 => Color.Warning, // 3: Yellow
            >= 1.5 => Color.Tertiary, // 2: Orange-ish (Tertiary is typically orange/salmon)
            _ => Color.Error // 1: Red
        };
    }

    /// <summary>
    /// Get CSS hex/rgba color string for a score on 1-5 scale for heatmap backgrounds.
    /// Returns both the background color and appropriate text color.
    /// </summary>
    public static (string bgColor, string textColor) GetScoreColorCss(double score)
    {
        return score switch
        {
            >= 4.5 => ("rgba(6, 214, 160, 0.5)", "#000"), // 5: Green
            >= 3.5 => ("rgba(76, 201, 240, 0.5)", "#000"), // 4: Teal/Cyan
            >= 2.5 => ("rgba(255, 214, 10, 0.5)", "#000"), // 3: Yellow
            >= 1.5 => ("rgba(255, 152, 0, 0.5)", "#000"), // 2: Orange
            _ => ("rgba(239, 71, 111, 0.5)", "#000") // 1: Red
        };
    }

    /// <summary>
    /// Get CSS rgba color string for EvaluationProgressRing wedges.
    /// </summary>
    public static string GetScoreColorRgba(double score)
    {
        return score switch
        {
            >= 4.5 => "#06D6A0", // 5: Green
            >= 3.5 => "#4CC9F0", // 4: Cyan/Teal
            >= 2.5 => "#FFD60A", // 3: Yellow
            >= 1.5 => "#FF9800", // 2: Orange
            _ => "#EF476F" // 1: Red
        };
    }
}
