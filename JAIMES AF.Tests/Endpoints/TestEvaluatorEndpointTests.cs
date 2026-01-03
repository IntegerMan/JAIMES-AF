using System.Net;
using System.Net.Http.Json;
using MattEland.Jaimes.ServiceDefinitions.Requests;
using MattEland.Jaimes.ServiceDefinitions.Responses;
using MattEland.Jaimes.Tests.Endpoints;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Shouldly;

namespace MattEland.Jaimes.Tests.Endpoints;

public class TestEvaluatorEndpointTests : EndpointTestBase
{
    private TestEvaluationState _evaluationState = null!;

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll(typeof(IEvaluator));
        services.AddSingleton<TestEvaluationState>();
        services.AddSingleton<IEvaluator, StubEvaluator>();

        services.RemoveAll(typeof(IChatClient));
        Mock<IChatClient> mockChatClient = new();
        services.AddSingleton(mockChatClient.Object);
    }

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        _evaluationState = scope.ServiceProvider.GetRequiredService<TestEvaluationState>();
    }

    [Fact]
    public async Task MissingInterpretation_WithPassingScore_ShouldBePass()
    {
        _evaluationState.Configure("TestMetric", () =>
        {
            NumericMetric metric = new("TestMetric")
            {
                Value = 4.0
            };
            return metric;
        });

        TestEvaluatorResponse response = await ExecuteRequestAsync();

        TestEvaluatorMetricResult metric = response.Metrics.ShouldHaveSingleItem();
        bool? passed = metric.Passed;
        passed.ShouldNotBeNull();
        passed.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingInterpretation_WithFailingScore_ShouldBeFail()
    {
        _evaluationState.Configure("TestMetric", () =>
        {
            NumericMetric metric = new("TestMetric")
            {
                Value = 3.5
            };
            return metric;
        });

        TestEvaluatorResponse response = await ExecuteRequestAsync();

        TestEvaluatorMetricResult metric = response.Metrics.ShouldHaveSingleItem();
        bool? passed = metric.Passed;
        passed.ShouldNotBeNull();
        passed.Value.ShouldBeFalse();
    }

    private async Task<TestEvaluatorResponse> ExecuteRequestAsync()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        TestEvaluatorRequest request = new()
        {
            InstructionVersionId = 1,
            AssistantResponse = "Test assistant response",
            ConversationContext = []
        };

        HttpResponseMessage httpResponse = await Client.PostAsJsonAsync("/admin/evaluators/test", request, ct);
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        TestEvaluatorResponse? response = await httpResponse.Content.ReadFromJsonAsync<TestEvaluatorResponse>(cancellationToken: ct);
        response.ShouldNotBeNull();
        response.Errors.ShouldBeEmpty();

        return response;
    }

    private sealed class StubEvaluator(TestEvaluationState state) : IEvaluator
    {
        public IReadOnlyCollection<string> EvaluationMetricNames => state.MetricNames;

        public ValueTask<EvaluationResult> EvaluateAsync(
            IEnumerable<ChatMessage> messages,
            ChatResponse modelResponse,
            ChatConfiguration? chatConfiguration = null,
            IEnumerable<EvaluationContext>? evaluationContext = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(state.BuildResult());
        }
    }

    private sealed class TestEvaluationState
    {
        private Func<EvaluationMetric> _metricFactory = () => new NumericMetric("TestMetric") { Value = 5.0 };
        private string[] _metricNames = ["TestMetric"];

        public IReadOnlyCollection<string> MetricNames => _metricNames;

        public void Configure(string metricName, Func<EvaluationMetric> metricFactory)
        {
            _metricNames = [metricName];
            _metricFactory = metricFactory;
        }

        public EvaluationResult BuildResult()
        {
            return new EvaluationResult(_metricFactory());
        }
    }
}
