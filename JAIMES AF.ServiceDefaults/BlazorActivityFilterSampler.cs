using OpenTelemetry.Trace;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Custom sampler that filters out Blazor ComponentHub activities and non-relevant UI events.
/// Keeps only events that trigger POST requests (onkeydown, onclick) and business logic traces.
/// </summary>
public sealed class BlazorActivityFilterSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Get activity name to check
        string? activityName = samplingParameters.Name;

        if (string.IsNullOrEmpty(activityName))
        {
            // If no name, allow it (shouldn't happen, but be safe)
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        // Always keep routing events (they're important business logic)
        // These appear as "Route /games -> MattEland.Jaimes.Web.Components.Pages.Games"
        if (activityName.StartsWith("Route ", StringComparison.OrdinalIgnoreCase))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        // Only filter ComponentHub activities if we're absolutely certain
        // The full path format is: "Microsoft.AspNetCore.Components.Server.ComponentHub/MethodName"
        // Be very specific to avoid false positives
        if (activityName.Contains("Microsoft.AspNetCore.Components.Server.ComponentHub/", StringComparison.OrdinalIgnoreCase))
        {
            // Check if this is a known ComponentHub method
            string[] componentHubMethods = 
            {
                "OnRenderCompleted",
                "BeginInvokeDotNetFromJS",
                "EndInvokeJSFromDotNet",
                "OnCircuitOpened",
                "OnCircuitClosed",
                "StartCircuit",
                "EndCircuit"
            };

            foreach (string method in componentHubMethods)
            {
                if (activityName.Contains($"/{method}", StringComparison.OrdinalIgnoreCase) ||
                    activityName.EndsWith($"/{method}", StringComparison.OrdinalIgnoreCase))
                {
                    return new SamplingResult(SamplingDecision.Drop);
                }
            }
        }

        // Filter non-relevant UI events (keep onkeydown and onclick as they trigger POSTs)
        // These appear as "Event onkeyup -> MudBlazor.MudInput 1" or similar
        if (activityName.Contains("Event ", StringComparison.OrdinalIgnoreCase))
        {
            // Keep onkeydown and onclick events (they trigger POST requests)
            // Filter out onkeyup, onchange, and other UI events that don't trigger POSTs
            if (!activityName.Contains("onkeydown", StringComparison.OrdinalIgnoreCase) &&
                !activityName.Contains("onclick", StringComparison.OrdinalIgnoreCase))
            {
                return new SamplingResult(SamplingDecision.Drop);
            }
        }

        // Allow all other activities (business logic, API calls, worker operations, etc.)
        // Default to allowing everything - let the processor handle more specific filtering
        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}

