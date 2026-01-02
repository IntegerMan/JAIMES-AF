using System.Net;
using System.Net.Http.Json;
using MattEland.Jaimes.ApiService.Endpoints.Locations;
using MattEland.Jaimes.Repositories.Entities;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class AddLocationEventEndpointTests : EndpointTestBase
{
    private int _locationId;

    protected override async Task SeedTestDataAsync(JaimesDbContext context, CancellationToken cancellationToken)
    {
        await base.SeedTestDataAsync(context, cancellationToken);

        // Add a location to the test database
        Location location = new()
        {
            GameId = Guid.Parse("00000000-0000-0000-0000-000000000001"), // Matching base Guid if needed or just any
            Name = "Test Location",
            Description = "A place for testing",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Locations.Add(location);
        await context.SaveChangesAsync(cancellationToken);
        _locationId = location.Id;
    }

    [Fact]
    public async Task AddLocationEvent_ReturnsCreated_WhenValid()
    {
        // Arrange
        var request = new AddLocationEventRequest
        {
            EventName = "Valid Event",
            EventDescription = "Something happened here."
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/locations/{_locationId}/events", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        LocationEventResponse? result = await response.Content.ReadFromJsonAsync<LocationEventResponse>(ct);
        result.ShouldNotBeNull();
        result.EventName.ShouldBe(request.EventName);
    }

    [Fact]
    public async Task AddLocationEvent_ReturnsBadRequest_WhenEventNameIsEmpty()
    {
        // Arrange
        var request = new AddLocationEventRequest
        {
            EventName = "",
            EventDescription = "Something happened here."
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/locations/{_locationId}/events", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddLocationEvent_ReturnsBadRequest_WhenEventNameTooLong()
    {
        // Arrange
        var request = new AddLocationEventRequest
        {
            EventName = new string('A', 201),
            EventDescription = "Something happened here."
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync($"/locations/{_locationId}/events", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddLocationEvent_ReturnsNotFound_WhenLocationDoesNotExist()
    {
        // Arrange
        var request = new AddLocationEventRequest
        {
            EventName = "Valid Event",
            EventDescription = "Something happened here."
        };
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await Client.PostAsJsonAsync("/locations/9999/events", request, ct);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
