using MattEland.Jaimes.ServiceDefinitions.Responses;

namespace MattEland.Jaimes.Tests.Endpoints;

public class GetActiveAgentInstructionVersionEndpointTests : EndpointTestBase
{
    [Fact]
    public async Task GetActiveAgentInstructionVersionEndpoint_WithValidAgent_ReturnsActiveVersion()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // First create an agent
        var createAgentRequest = new { Name = "Test Agent", Role = "GameMaster" };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/agents", createAgentRequest, ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AgentResponse? createdAgent = await createResponse.Content.ReadFromJsonAsync<AgentResponse>(ct);
        createdAgent.ShouldNotBeNull();

        // Create an instruction version for the agent
        var createVersionRequest = new { VersionNumber = "1.0.0", Instructions = "Test instructions" };
        HttpResponseMessage versionResponse = await Client.PostAsJsonAsync($"/agents/{createdAgent.Id}/instruction-versions", createVersionRequest, ct);
        versionResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now get the active instruction version
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

        // First create an agent
        var createAgentRequest = new { Name = "Test Agent", Role = "GameMaster" };
        HttpResponseMessage createResponse = await Client.PostAsJsonAsync("/agents", createAgentRequest, ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        AgentResponse? createdAgent = await createResponse.Content.ReadFromJsonAsync<AgentResponse>(ct);
        createdAgent.ShouldNotBeNull();

        // Try to get active instruction version when none exist
        HttpResponseMessage getResponse = await Client.GetAsync($"/agents/{createdAgent.Id}/instruction-versions/active", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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