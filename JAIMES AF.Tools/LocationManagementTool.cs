using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MattEland.Jaimes.Tools;

/// <summary>
/// Tool that provides location management functionality for the AI storyteller.
/// </summary>
public class LocationManagementTool(GameDto game, IServiceProvider serviceProvider)
{
    private readonly GameDto _game = game ?? throw new ArgumentNullException(nameof(game));

    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private const int MaxNameLength = 200;

    /// <summary>
    /// Creates a new location or updates an existing one.
    /// </summary>
    /// <param name="name">The unique name of the location within this game.</param>
    /// <param name="description">The player-facing description / appearance of the location (required).</param>
    /// <param name="storytellerNotes">Private notes for you (the AI) about this location. Hidden from the player.</param>
    /// <returns>Confirmation of the operation or an error message.</returns>
    [Description(
        "Creates a new location or updates an existing one. Use this tool to establish new places in the game world as they become relevant to the story. Every location must have a name and description / appearance. You can also add private storyteller notes that are hidden from the player to help you plan story elements.")]
    public async Task<string> CreateOrUpdateLocationAsync(string name, string description,
        string? storytellerNotes = null)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Error: Location name is required. Please provide a name for the location.";
        }

        if (name.Length > MaxNameLength)
        {
            return
                $"Error: Location name must be {MaxNameLength} characters or less. The provided name has {name.Length} characters.";
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return "Error: Location description is required. Please provide a description for the location.";
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        ILocationService? locationService = scope.ServiceProvider.GetService<ILocationService>();
        if (locationService == null)
        {
            return "Location service is not available.";
        }

        // Check if location already exists
        LocationResponse? existing = await locationService.GetLocationByNameAsync(_game.GameId, name);

        if (existing != null)
        {
            // Update existing location
            LocationResponse? updated =
                await locationService.UpdateLocationAsync(existing.Id, description, storytellerNotes);
            return updated != null
                ? $"Location '{name}' has been updated."
                : $"Error: Failed to update location '{name}'.";
        }

        // Create new location
        LocationResponse newLocation =
            await locationService.CreateLocationAsync(_game.GameId, name, description, storytellerNotes);
        return $"Location '{newLocation.Name}' has been created successfully.";
    }

    /// <summary>
    /// Adds a significant event to a location's history.
    /// </summary>
    /// <param name="locationName">The name of the location where the event occurred.</param>
    /// <param name="eventName">A short name/title for the event.</param>
    /// <param name="eventDescription">A description of what happened (required).</param>
    /// <returns>Confirmation of the operation or an error message.</returns>
    [Description(
        "Adds a significant event to a location's history. Use this tool to record important happenings at locations - battles, discoveries, meetings, or any event worth remembering. This helps maintain narrative consistency throughout the game.")]
    public async Task<string> AddLocationEventAsync(string locationName, string eventName, string eventDescription)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return "Error: Location name is required. Please specify which location the event occurred at.";
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            return "Error: Event name is required. Please provide a short name for the event.";
        }

        if (eventName.Length > MaxNameLength)
        {
            return $"Error: Event name must be {MaxNameLength} characters or less.";
        }

        if (string.IsNullOrWhiteSpace(eventDescription))
        {
            return "Error: Event description is required. Please describe what happened at this location.";
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        ILocationService? locationService = scope.ServiceProvider.GetService<ILocationService>();
        if (locationService == null)
        {
            return "Location service is not available.";
        }

        // Find the location
        LocationResponse? location = await locationService.GetLocationByNameAsync(_game.GameId, locationName);
        if (location == null)
        {
            return
                $"Error: Location '{locationName}' was not found. Please create the location first using CreateOrUpdateLocation, or check the spelling.";
        }

        LocationEventResponse evt =
            await locationService.AddLocationEventAsync(location.Id, eventName, eventDescription);
        return $"Event '{evt.EventName}' has been added to {location.Name}.";
    }

    /// <summary>
    /// Links two locations as being nearby to each other.
    /// </summary>
    /// <param name="locationName">The name of the first location.</param>
    /// <param name="nearbyLocationName">The name of the second location that is nearby.</param>
    /// <param name="distance">Optional distance description (e.g., "2 miles", "a day's journey").</param>
    /// <param name="travelNotes">Optional travel notes visible to the player (e.g., "through the dark forest").</param>
    /// <param name="storytellerNotes">Optional private notes for the AI about this route (e.g., hidden dangers).</param>
    /// <returns>Confirmation of the operation or an error message.</returns>
    [Description(
        "Links two locations as being nearby to each other. Use this tool to establish geographic relationships between places. You can include travel information and private storyteller notes about dangers or secrets along the route that are hidden from the player.")]
    public async Task<string> AddNearbyLocationAsync(string locationName, string nearbyLocationName,
        string? distance = null, string? travelNotes = null, string? storytellerNotes = null)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return "Error: First location name is required.";
        }

        if (string.IsNullOrWhiteSpace(nearbyLocationName))
        {
            return "Error: Second (nearby) location name is required.";
        }

        if (string.Equals(locationName, nearbyLocationName, StringComparison.OrdinalIgnoreCase))
        {
            return "Error: Cannot link a location to itself.";
        }

        using IServiceScope scope = _serviceProvider.CreateScope();
        ILocationService? locationService = scope.ServiceProvider.GetService<ILocationService>();
        if (locationService == null)
        {
            return "Location service is not available.";
        }

        // Find both locations
        LocationResponse? source = await locationService.GetLocationByNameAsync(_game.GameId, locationName);
        if (source == null)
        {
            return $"Error: Location '{locationName}' was not found. Please create it first or check the spelling.";
        }

        LocationResponse? target = await locationService.GetLocationByNameAsync(_game.GameId, nearbyLocationName);
        if (target == null)
        {
            return
                $"Error: Location '{nearbyLocationName}' was not found. Please create it first or check the spelling.";
        }

        NearbyLocationResponse? result =
            await locationService.AddNearbyLocationAsync(source.Id, target.Id, distance, travelNotes, storytellerNotes);

        if (result == null)
        {
            return "Error: Failed to link locations.";
        }

        string message = $"{source.Name} and {target.Name} are now linked as nearby locations.";
        if (!string.IsNullOrWhiteSpace(distance))
        {
            message += $" Distance: {distance}.";
        }

        return message;
    }
}
