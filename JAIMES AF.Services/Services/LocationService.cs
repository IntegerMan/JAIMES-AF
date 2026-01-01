using MattEland.Jaimes.Repositories;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;
using Microsoft.EntityFrameworkCore;

namespace MattEland.Jaimes.ServiceLayer.Services;

/// <summary>
/// Service implementation for location management operations.
/// </summary>
public class LocationService(IDbContextFactory<JaimesDbContext> contextFactory) : ILocationService
{
    /// <inheritdoc />
    public async Task<LocationResponse?> GetLocationByNameAsync(Guid gameId, string name,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? location = await context.Locations
            .Include(l => l.Events)
            .Include(l => l.NearbyLocationsAsSource)
            .Include(l => l.NearbyLocationsAsTarget)
            .FirstOrDefaultAsync(l => l.GameId == gameId && l.Name.ToLower() == name.ToLower(), cancellationToken);

        return location == null ? null : MapToResponse(location);
    }

    /// <inheritdoc />
    public async Task<LocationListResponse> GetLocationsAsync(Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        List<Location> locations = await context.Locations
            .Include(l => l.Events)
            .Include(l => l.NearbyLocationsAsSource)
            .Include(l => l.NearbyLocationsAsTarget)
            .Where(l => l.GameId == gameId)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);

        return new LocationListResponse
        {
            Locations = locations.Select(MapToResponse).ToArray(),
            TotalCount = locations.Count
        };
    }

    /// <inheritdoc />
    public async Task<LocationResponse?> GetLocationByIdAsync(int locationId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? location = await context.Locations
            .Include(l => l.Events)
            .Include(l => l.NearbyLocationsAsSource)
            .Include(l => l.NearbyLocationsAsTarget)
            .FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);

        return location == null ? null : MapToResponse(location);
    }

    /// <inheritdoc />
    public async Task<LocationResponse> CreateLocationAsync(Guid gameId, string name, string description,
        string? storytellerNotes, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location location = new()
        {
            GameId = gameId,
            Name = name,
            Description = description,
            StorytellerNotes = storytellerNotes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(location);
    }

    /// <inheritdoc />
    public async Task<LocationResponse?> UpdateLocationAsync(int locationId, string? description,
        string? storytellerNotes, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? location = await context.Locations
            .Include(l => l.Events)
            .Include(l => l.NearbyLocationsAsSource)
            .Include(l => l.NearbyLocationsAsTarget)
            .FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);

        if (location == null)
        {
            return null;
        }

        if (description != null)
        {
            location.Description = description;
        }

        if (storytellerNotes != null)
        {
            location.StorytellerNotes = storytellerNotes;
        }

        location.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return MapToResponse(location);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteLocationAsync(int locationId, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? location = await context.Locations.FindAsync([locationId], cancellationToken);
        if (location == null)
        {
            return false;
        }

        context.Locations.Remove(location);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<LocationEventResponse[]> GetLocationEventsAsync(int locationId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? location = await context.Locations
            .Include(l => l.Events)
            .FirstOrDefaultAsync(l => l.Id == locationId, cancellationToken);

        if (location == null)
        {
            return [];
        }

        return location.Events
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new LocationEventResponse
            {
                Id = e.Id,
                LocationId = e.LocationId,
                LocationName = location.Name,
                EventName = e.EventName,
                EventDescription = e.EventDescription,
                CreatedAt = e.CreatedAt
            })
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<LocationEventResponse> AddLocationEventAsync(int locationId, string eventName,
        string eventDescription, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? location = await context.Locations.FindAsync([locationId], cancellationToken);
        if (location == null)
        {
            throw new InvalidOperationException($"Location with ID {locationId} not found.");
        }

        LocationEvent evt = new()
        {
            LocationId = locationId,
            EventName = eventName,
            EventDescription = eventDescription,
            CreatedAt = DateTime.UtcNow
        };

        context.LocationEvents.Add(evt);
        await context.SaveChangesAsync(cancellationToken);

        return new LocationEventResponse
        {
            Id = evt.Id,
            LocationId = evt.LocationId,
            LocationName = location.Name,
            EventName = evt.EventName,
            EventDescription = evt.EventDescription,
            CreatedAt = evt.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<NearbyLocationResponse[]> GetNearbyLocationsAsync(int locationId,
        CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        List<NearbyLocation> nearbyLocations = await context.NearbyLocations
            .Include(nl => nl.SourceLocation)
            .Include(nl => nl.TargetLocation)
            .Where(nl => nl.SourceLocationId == locationId || nl.TargetLocationId == locationId)
            .ToListAsync(cancellationToken);

        return nearbyLocations.Select(nl => new NearbyLocationResponse
        {
            SourceLocationId = nl.SourceLocationId,
            SourceLocationName = nl.SourceLocation.Name,
            TargetLocationId = nl.TargetLocationId,
            TargetLocationName = nl.TargetLocation.Name,
            Distance = nl.Distance,
            TravelNotes = nl.TravelNotes,
            StorytellerNotes = nl.StorytellerNotes
        }).ToArray();
    }

    /// <inheritdoc />
    public async Task<NearbyLocationResponse?> AddNearbyLocationAsync(int sourceLocationId, int targetLocationId,
        string? distance, string? travelNotes, string? storytellerNotes, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        Location? source = await context.Locations.FindAsync([sourceLocationId], cancellationToken);
        Location? target = await context.Locations.FindAsync([targetLocationId], cancellationToken);

        if (source == null || target == null)
        {
            return null;
        }

        // Check if relationship already exists (in either direction)
        bool exists = await context.NearbyLocations
            .AnyAsync(nl => (nl.SourceLocationId == sourceLocationId && nl.TargetLocationId == targetLocationId) ||
                            (nl.SourceLocationId == targetLocationId && nl.TargetLocationId == sourceLocationId),
                cancellationToken);

        if (exists)
        {
            // Update existing relationship (find in either direction)
            NearbyLocation? existing = await context.NearbyLocations
                .FirstAsync(nl =>
                        (nl.SourceLocationId == sourceLocationId && nl.TargetLocationId == targetLocationId) ||
                        (nl.SourceLocationId == targetLocationId && nl.TargetLocationId == sourceLocationId),
                    cancellationToken);

            existing.Distance = distance ?? existing.Distance;
            existing.TravelNotes = travelNotes ?? existing.TravelNotes;
            existing.StorytellerNotes = storytellerNotes ?? existing.StorytellerNotes;
            await context.SaveChangesAsync(cancellationToken);

            return new NearbyLocationResponse
            {
                SourceLocationId = existing.SourceLocationId,
                SourceLocationName = existing.SourceLocationId == sourceLocationId ? source.Name : target.Name,
                TargetLocationId = existing.TargetLocationId,
                TargetLocationName = existing.TargetLocationId == targetLocationId ? target.Name : source.Name,
                Distance = existing.Distance,
                TravelNotes = existing.TravelNotes,
                StorytellerNotes = existing.StorytellerNotes
            };
        }

        NearbyLocation nearby = new()
        {
            SourceLocationId = sourceLocationId,
            TargetLocationId = targetLocationId,
            Distance = distance,
            TravelNotes = travelNotes,
            StorytellerNotes = storytellerNotes
        };

        context.NearbyLocations.Add(nearby);
        await context.SaveChangesAsync(cancellationToken);

        return new NearbyLocationResponse
        {
            SourceLocationId = nearby.SourceLocationId,
            SourceLocationName = source.Name,
            TargetLocationId = nearby.TargetLocationId,
            TargetLocationName = target.Name,
            Distance = nearby.Distance,
            TravelNotes = nearby.TravelNotes,
            StorytellerNotes = nearby.StorytellerNotes
        };
    }

    /// <inheritdoc />
    public async Task<bool> LocationExistsAsync(Guid gameId, string name, CancellationToken cancellationToken = default)
    {
        await using JaimesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Locations.AnyAsync(l => l.GameId == gameId && l.Name.ToLower() == name.ToLower(),
            cancellationToken);
    }

    private static LocationResponse MapToResponse(Location location)
    {
        return new LocationResponse
        {
            Id = location.Id,
            GameId = location.GameId,
            Name = location.Name,
            Description = location.Description,
            StorytellerNotes = location.StorytellerNotes,
            CreatedAt = location.CreatedAt,
            UpdatedAt = location.UpdatedAt,
            EventCount = location.Events.Count,
            NearbyLocationCount = location.NearbyLocationsAsSource.Count + location.NearbyLocationsAsTarget.Count
        };
    }
}
