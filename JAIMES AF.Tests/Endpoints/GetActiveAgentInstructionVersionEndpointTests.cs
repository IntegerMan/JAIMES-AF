using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Tests.Endpoints;

public class GetActiveAgentInstructionVersionEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task GetActiveAgentInstructionVersionEndpoint_WithValidAgent_ReturnsActiveVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // First create an agent (with required Instructions field)
        var createAgentRequest = new { Name = "Test Agent", Role = "GameMaster", Instructions = "Initial instructions" };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/agents", createAgentRequest, ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AgentResponse? createdAgent = await createResponse.Content.ReadFromJsonAsync<AgentResponse>(ct);
        createdAgent.ShouldNotBeNull();

        // Create an additional instruction version for the agent (agent already has v1.0 from creation)
        var createVersionRequest = new { VersionNumber = "1.0.0", Instructions = "Test instructions" };
        HttpResponseMessage versionResponse = await Client.PostAsJsonAsync($"/agents/{createdAgent.Id}/instruction-versions", createVersionRequest, ct);
        versionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now get the active instruction version (should be the newly created 1.0.0, not the initial v1.0)
        HttpResponseMessage getResponse = await Client.GetAsync($"/agents/{createdAgent.Id}/instruction-versions/active", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AgentInstructionVersionResponse? version = await getResponse.Content.ReadFromJsonAsync<AgentInstructionVersionResponse>(ct);
        version.ShouldNotBeNull();
        version.AgentId.ShouldBe(createdAgent.Id);
        version.VersionNumber.ShouldBe("1.0.0");
        version.Instructions.ShouldBe("Test instructions");
        version.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetActiveAgentInstructionVersionEndpoint_WithAgentWithoutVersions_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Note: Agents now always have a v1.0 version created automatically, so this test
        // verifies that the endpoint returns the active version (v1.0) that was created with the agent
        var createAgentRequest = new { Name = "Test Agent", Role = "GameMaster", Instructions = "Initial instructions" };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/agents", createAgentRequest, ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AgentResponse? createdAgent = await createResponse.Content.ReadFromJsonAsync<AgentResponse>(ct);
        createdAgent.ShouldNotBeNull();

        // Get active instruction version (should return v1.0 that was created automatically)
        HttpResponseMessage getResponse = await Client.GetAsync($"/agents/{createdAgent.Id}/instruction-versions/active", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AgentInstructionVersionResponse? version = await getResponse.Content.ReadFromJsonAsync<AgentInstructionVersionResponse>(ct);
        version.ShouldNotBeNull();
        version.AgentId.ShouldBe(createdAgent.Id);
        version.VersionNumber.ShouldBe("v1.0");
        version.Instructions.ShouldBe("Initial instructions");
        version.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetActiveAgentInstructionVersionEndpoint_WithInvalidAgent_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Try to get active instruction version for non-existent agent
        HttpResponseMessage getResponse = await Client.GetAsync("/agents/invalid-agent-id/instruction-versions/active", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

