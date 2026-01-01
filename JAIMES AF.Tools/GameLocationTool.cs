using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides location lookup functionality for the AI storyteller.
/// </summary>
public class GameLocationTool(GameDto game, IServiceProvider serviceProvider)
{
    private readonly GameDto _game = game ?? throw new ArgumentNullException(nameof(game));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    /// <summary>
    /// Retrieves detailed information about a location by name.
    /// This tool uses case-insensitive matching to find locations within the current game.
    /// </summary>
    /// <param name="locationName">The name of the location to look up (case-insensitive).</param>
    /// <returns>A string containing the location's description, storyteller notes, appearance, significant events, and nearby locations.</returns>
    [Description(
        "Retrieves detailed information about a location by name, including its description, appearance, significant events that have occurred there, and nearby locations. Use this tool whenever you need to describe a location the player visits or references, or when you need to recall what has happened at a specific place. The search is case-insensitive.")]
    public async Task<string> GetLocationByNameAsync(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return "Please provide a location name to look up.";
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        ILocationService? locationService = scope.ServiceProvider.GetService<ILocationService>();
        if (locationService == null)
        {
            return "Location service is not available.";
        }

        LocationResponse? location = await locationService.GetLocationByNameAsync(_game.GameId, locationName);

        if (location == null)
        {
            // Get list of valid locations to help the AI
            LocationListResponse allLocations = await locationService.GetLocationsAsync(_game.GameId);
            if (allLocations.TotalCount == 0)
            {
                return
                    $"Location '{locationName}' was not found. No locations exist in this game yet. Use the location management tool to create it first.";
            }

            string validNames = string.Join("', '", allLocations.Locations.Select(l => l.Name));
            return $"Location '{locationName}' was not found. Valid locations are: '{validNames}'.";
        }

        return await FormatLocationDetailsAsync(location, locationService);
    }

    /// <summary>
    /// Gets a list of all known locations in the current game.
    /// </summary>
    /// <returns>A string containing the names and brief descriptions of all locations.</returns>
    [Description(
        "Gets a list of all known locations in the current game with their names and brief descriptions. Use this tool when you need to see what locations have been established in the game world, or when the player asks about places they can go.")]
    public async Task<string> GetAllLocationsAsync()
    {
        using IServiceScope scope = _serviceProvider.CreateScope();
        ILocationService? locationService = scope.ServiceProvider.GetService<ILocationService>();
        if (locationService == null)
        {
            return "Location service is not available.";
        }

        LocationListResponse response = await locationService.GetLocationsAsync(_game.GameId);

        if (response.TotalCount == 0)
        {
            return
                "No locations have been established in this game yet. Use the location management tool to create new locations as the story unfolds.";
        }

        List<string> locationStrings = [];
        foreach (LocationResponse loc in response.Locations)
        {
            string locInfo = $"**{loc.Name}**: {loc.Description}";
            if (loc.EventCount > 0)
            {
                locInfo += $" ({loc.EventCount} event(s) recorded)";
            }

            locationStrings.Add(locInfo);
        }

        return $"Known locations ({response.TotalCount}):\n\n" + string.Join("\n\n", locationStrings);
    }

    private static async Task<string> FormatLocationDetailsAsync(LocationResponse location,
        ILocationService locationService)
    {
        List<string> parts =
        [
            $"**{location.Name}**",
            $"Description: {location.Description}"
        ];

        if (!string.IsNullOrWhiteSpace(location.Appearance))
        {
            parts.Add($"Appearance: {location.Appearance}");
        }

        if (!string.IsNullOrWhiteSpace(location.StorytellerNotes))
        {
            parts.Add($"[Storyteller Notes - Hidden from player]: {location.StorytellerNotes}");
        }

        // Get events
        LocationEventResponse[] events = await locationService.GetLocationEventsAsync(location.Id);
        if (events.Length > 0)
        {
            parts.Add("\nSignificant Events:");
            foreach (LocationEventResponse evt in events)
            {
                parts.Add($"  - {evt.EventName}: {evt.EventDescription}");
            }
        }

        // Get nearby locations
        NearbyLocationResponse[] nearbyLocations = await locationService.GetNearbyLocationsAsync(location.Id);
        if (nearbyLocations.Length > 0)
        {
            parts.Add("\nNearby Locations:");
            foreach (NearbyLocationResponse nearby in nearbyLocations)
            {
                string nearbyName = nearby.SourceLocationId == location.Id
                    ? nearby.TargetLocationName
                    : nearby.SourceLocationName;

                string nearbyInfo = $"  - {nearbyName}";
                if (!string.IsNullOrWhiteSpace(nearby.Distance))
                {
                    nearbyInfo += $" ({nearby.Distance})";
                }

                if (!string.IsNullOrWhiteSpace(nearby.TravelNotes))
                {
                    nearbyInfo += $" - {nearby.TravelNotes}";
                }

                if (!string.IsNullOrWhiteSpace(nearby.StorytellerNotes))
                {
                    nearbyInfo += $" [Hidden: {nearby.StorytellerNotes}]";
                }

                parts.Add(nearbyInfo);
            }
        }

        return string.Join("\n", parts);
    }
}
