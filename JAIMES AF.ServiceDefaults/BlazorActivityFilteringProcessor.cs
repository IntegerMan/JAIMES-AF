using System.Diagnostics;
using OpenTelemetry;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Custom processor that filters out Blazor ComponentHub activities and non-relevant UI events.
/// Uses OnEnd to filter activities before export by not calling base.OnEnd().
/// Keeps routing events and events that trigger POST requests (onkeydown, onclick).
/// </summary>
public sealed class BlazorActivityFilteringProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        // Get activity properties
        string? sourceName = activity.Source.Name;
        string? operationName = activity.OperationName;
        string? displayName = activity.DisplayName;

        // Always keep routing events (they're important business logic)
        // These appear as "Route /games -> MattEland.Jaimes.Web.Components.Pages.Games"
        if ((!string.IsNullOrEmpty(operationName) && operationName.StartsWith("Route ", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrEmpty(displayName) && displayName.StartsWith("Route ", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnEnd(activity);
            return;
        }

        // Filter ComponentHub activities - ONLY filter if source name explicitly contains ComponentHub
        // This is the most reliable check - ComponentHub activities come from "Microsoft.AspNetCore.Components.Server.ComponentHub"
        if (!string.IsNullOrEmpty(sourceName) && sourceName.Contains("ComponentHub", StringComparison.OrdinalIgnoreCase))
        {
            // Don't call base.OnEnd() - this filters out the activity
            return;
        }

        // Check display name for ComponentHub patterns (backup check)
        // ComponentHub activities appear as "Microsoft.AspNetCore.Components.Server.ComponentHub/OnRenderCompleted"
        // Only filter if we see the full ComponentHub path in the display name
        if (!string.IsNullOrEmpty(displayName) && displayName.Contains("ComponentHub/", StringComparison.OrdinalIgnoreCase))
        {
            // Don't call base.OnEnd() - this filters out the activity
            return;
        }

        // Filter non-relevant UI events (keep onkeydown and onclick as they trigger POSTs)
        // These appear as "Event onpointerleave -> ...", "Event onkeyup -> ...", "Event onchange -> ..."
        // Only filter if we see "Event " followed by a non-relevant event name
        string? nameToCheck = displayName ?? operationName;
        if (!string.IsNullOrEmpty(nameToCheck) && nameToCheck.Contains("Event ", StringComparison.OrdinalIgnoreCase))
        {
            // Keep onkeydown and onclick events (they trigger POST requests)
            // Filter out all other UI events: onpointerleave, onkeyup, onchange, onmouseover, etc.
            if (!nameToCheck.Contains("onkeydown", StringComparison.OrdinalIgnoreCase) &&
                !nameToCheck.Contains("onclick", StringComparison.OrdinalIgnoreCase))
            {
                // Don't call base.OnEnd() - this filters out the activity
                return;
            }
        }

        // Allow all other activities (business logic, API calls, worker operations, etc.)
        base.OnEnd(activity);
    }
}

