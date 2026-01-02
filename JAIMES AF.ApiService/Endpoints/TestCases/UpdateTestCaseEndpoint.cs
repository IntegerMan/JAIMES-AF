using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.ServiceDefinitions.Services;

namespace MattEland.Jaimes.ApiService.Endpoints.TestCases;

public class UpdateTestCaseEndpoint : Ep.Req<UpdateTestCaseRequest>.Res<TestCaseResponse>
{
    public required ITestCaseService TestCaseService { get; set; }

    public override void Configure()
    {
        Put("/test-cases/{id:int}");
        AllowAnonymous();
        Description(b => b
            .Produces<TestCaseResponse>()
            .Produces(400)
            .Produces(404)
            .WithTags("Test Cases"));
    }

    public override async Task HandleAsync(UpdateTestCaseRequest req, CancellationToken ct)
    {
        int id = Route<int>("id");

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            ThrowError("Name is required.");
            return;
        }

        TestCaseResponse? result = await TestCaseService.UpdateTestCaseAsync(id, req.Name, req.Description, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
