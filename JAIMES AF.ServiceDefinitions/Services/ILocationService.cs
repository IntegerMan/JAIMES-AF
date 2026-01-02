using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.ServiceDefinitions.Services;

/// <summary>
/// Service interface for location management operations.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets a location by name within a game (case-insensitive).
    /// </summary>
    Task<LocationResponse?> GetLocationByNameAsync(Guid gameId, string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all locations for a game.
    /// </summary>
    Task<LocationListResponse> GetLocationsAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a location by ID.
    /// </summary>
    Task<LocationResponse?> GetLocationByIdAsync(int locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new location.
    /// </summary>
    Task<LocationResponse> CreateLocationAsync(Guid gameId, string name, string description, string? storytellerNotes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing location.
    /// </summary>
    Task<LocationResponse?> UpdateLocationAsync(int locationId, string? description, string? storytellerNotes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a location.
    /// </summary>
    Task<bool> DeleteLocationAsync(int locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events for a location.
    /// </summary>
    Task<LocationEventResponse[]?>
        GetLocationEventsAsync(int locationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an event to a location.
    /// </summary>
    Task<LocationEventResponse> AddLocationEventAsync(int locationId, string eventName, string eventDescription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets nearby locations for a location.
    /// </summary>
    Task<NearbyLocationResponse[]?> GetNearbyLocationsAsync(int locationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a nearby location relationship.
    /// </summary>
    Task<NearbyLocationResponse?> AddNearbyLocationAsync(int sourceLocationId, int targetLocationId, string? distance,
        string? travelNotes, string? storytellerNotes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a location name exists in a game.
    /// </summary>
    Task<bool> LocationExistsAsync(Guid gameId, string name, CancellationToken cancellationToken = default);
}
