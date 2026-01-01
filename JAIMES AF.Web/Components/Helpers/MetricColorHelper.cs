using MudBlazor;

namespace MattEland.Jaimes.Web.Components.Helpers;

public static class MetricColorHelper
{
    public static Color GetScoreColor(double score)
    {
        return score switch
        {
            >= 5.0 => Color.Success,
            >= 4.0 => Color.Info,
            >= 3.0 => Color.Warning,
            >= 2.0 => Color.Secondary,
            _ => Color.Error
        };
    }
}
