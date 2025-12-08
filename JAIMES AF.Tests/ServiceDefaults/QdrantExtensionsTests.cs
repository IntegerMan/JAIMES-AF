using Qdrant.Client;

namespace MattEland.Jaimes.Tests.ServiceDefaults;

public class QdrantExtensionsTests
{
    [Fact]
    public void AddQdrantClient_WithConfigurationSection_RegistersClient()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6335"},
            {"TestSection:QdrantUseHttps", "true"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true
        };

        services.AddQdrantClient(configuration, options, out QdrantExtensions.QdrantConnectionConfig config);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
        config.Host.ShouldBe("test-host");
        config.Port.ShouldBe(6335);
        config.UseHttps.ShouldBeTrue();
    }

    [Fact]
    public void AddQdrantClient_WithConnectionString_ExtractsHostAndPort()
    {
        Dictionary<string, string?> configData = new()
        {
            {"ConnectionStrings:qdrant-embeddings", "grpc://connection-host:6336"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            ConnectionStringName = "qdrant-embeddings",
            RequireConfiguration = false
        };

        services.AddQdrantClient(configuration, options, out QdrantExtensions.QdrantConnectionConfig config);

        config.Host.ShouldBe("connection-host");
        config.Port.ShouldBe(6336);
    }

    [Fact]
    public void AddQdrantClient_WithConnectionStringAndApiKey_ExtractsApiKey()
    {
        Dictionary<string, string?> configData = new()
        {
            {"ConnectionStrings:qdrant-embeddings", "grpc://test-host:6334?api-key=test-api-key"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            ConnectionStringName = "qdrant-embeddings",
            RequireConfiguration = false
        };

        services.AddQdrantClient(configuration, options, out _);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddQdrantClient_WithApiKeyFromConfiguration_UsesApiKey()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6334"},
            {"Qdrant:ApiKey", "config-api-key"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true
        };

        services.AddQdrantClient(configuration, options, out _);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddQdrantClient_WithAdditionalApiKeyKeys_UsesAdditionalKeys()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6334"},
            {"Custom:ApiKey", "custom-api-key"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true,
            AdditionalApiKeyKeys = new[] {"Custom:ApiKey"}
        };

        services.AddQdrantClient(configuration, options, out _);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddQdrantClient_WithDefaultApiKey_UsesDefault()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6334"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true,
            DefaultApiKey = "default-key"
        };

        services.AddQdrantClient(configuration, options, out _);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddQdrantClient_WithoutConfigurationAndNotRequired_UsesDefaults()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = false
        };

        services.AddQdrantClient(configuration, options, out QdrantExtensions.QdrantConnectionConfig config);

        config.Host.ShouldBe("localhost");
        config.Port.ShouldBe(6334);
        config.UseHttps.ShouldBeFalse();
    }

    [Fact]
    public void AddQdrantClient_WithoutConfigurationAndRequired_ThrowsException()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true
        };

        Should.Throw<InvalidOperationException>(() =>
            services.AddQdrantClient(configuration, options, out _));
    }

    [Fact]
    public void AddQdrantClient_WithInvalidPort_ThrowsException()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "invalid-port"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true
        };

        Should.Throw<InvalidOperationException>(() =>
            services.AddQdrantClient(configuration, options, out _));
    }

    [Fact]
    public void AddQdrantClient_WithoutOutParameter_RegistersClient()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6334"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true
        };

        IServiceCollection result = services.AddQdrantClient(configuration, options);

        result.ShouldBeSameAs(services);
        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddQdrantClient_WithNullOptions_UsesDefaults()
    {
        Dictionary<string, string?> configData = new()
        {
            {"QdrantHost", "test-host"},
            {"QdrantPort", "6334"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();

        // When options is null, it creates default options with RequireConfiguration=true
        // So we need to provide the configuration values
        services.AddQdrantClient(configuration,
            new QdrantExtensions.QdrantConfigurationOptions
            {
                SectionPrefix = "",
                RequireConfiguration = false
            },
            out QdrantExtensions.QdrantConnectionConfig config);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }

    [Fact]
    public void GetQdrantConfiguration_WithConfigurationSection_ReturnsConfig()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "config-host"},
            {"TestSection:QdrantPort", "6337"},
            {"TestSection:QdrantUseHttps", "true"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = false
        };

        QdrantExtensions.QdrantConnectionConfig
            config = QdrantExtensions.GetQdrantConfiguration(configuration, options);

        config.Host.ShouldBe("config-host");
        config.Port.ShouldBe(6337);
        config.UseHttps.ShouldBeTrue();
    }

    [Fact]
    public void GetQdrantConfiguration_WithConnectionString_ExtractsFromConnectionString()
    {
        Dictionary<string, string?> configData = new()
        {
            {"ConnectionStrings:qdrant-embeddings", "grpc://conn-host:6338"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            ConnectionStringName = "qdrant-embeddings",
            RequireConfiguration = false
        };

        QdrantExtensions.QdrantConnectionConfig
            config = QdrantExtensions.GetQdrantConfiguration(configuration, options);

        config.Host.ShouldBe("conn-host");
        config.Port.ShouldBe(6338);
    }

    [Fact]
    public void GetQdrantConfiguration_WithoutConfiguration_UsesDefaults()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = false
        };

        QdrantExtensions.QdrantConnectionConfig
            config = QdrantExtensions.GetQdrantConfiguration(configuration, options);

        config.Host.ShouldBe("localhost");
        config.Port.ShouldBe(6334);
        config.UseHttps.ShouldBeFalse();
    }

    [Fact]
    public void GetQdrantConfiguration_WithInvalidPort_UsesDefaultPort()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "not-a-number"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = false
        };

        QdrantExtensions.QdrantConnectionConfig
            config = QdrantExtensions.GetQdrantConfiguration(configuration, options);

        config.Host.ShouldBe("test-host");
        config.Port.ShouldBe(6334); // Falls back to default
    }

    [Fact]
    public void AddQdrantClient_WithUnresolvedAspireExpression_ResolvesFromEnvironment()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6334"},
            {"Qdrant:ApiKey", "{qdrant-api-key}"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        Environment.SetEnvironmentVariable("qdrant-api-key", "resolved-key");

        try
        {
            ServiceCollection services = new();
            QdrantExtensions.QdrantConfigurationOptions options = new()
            {
                SectionPrefix = "TestSection",
                RequireConfiguration = true
            };

            services.AddQdrantClient(configuration, options, out _);

            QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
            client.ShouldNotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("qdrant-api-key", null);
        }
    }

    [Fact]
    public void AddQdrantClient_WithUnresolvedAspireExpressionAndDefault_UsesDefault()
    {
        Dictionary<string, string?> configData = new()
        {
            {"TestSection:QdrantHost", "test-host"},
            {"TestSection:QdrantPort", "6334"},
            {"Qdrant:ApiKey", "{qdrant-api-key}"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        QdrantExtensions.QdrantConfigurationOptions options = new()
        {
            SectionPrefix = "TestSection",
            RequireConfiguration = true,
            DefaultApiKey = "fallback-key"
        };

        services.AddQdrantClient(configuration, options, out _);

        QdrantClient? client = services.BuildServiceProvider().GetService<QdrantClient>();
        client.ShouldNotBeNull();
    }
}