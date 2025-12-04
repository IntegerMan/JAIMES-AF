using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI;

namespace MattEland.Jaimes.ServiceDefaults;

internal static class AiModelConfiguration
{
    public static ProviderType NormalizeProvider(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string sectionName,
        ProviderType currentProvider)
    {
        ProviderType provider = currentProvider;

        string? providerConfigValue = configuration[$"{sectionName}:Provider"];
        if (!string.IsNullOrWhiteSpace(providerConfigValue))
        {
            if (string.Equals(providerConfigValue, "Azure", StringComparison.OrdinalIgnoreCase))
            {
                provider = ProviderType.AzureOpenAI;
            }
            else if (Enum.TryParse(providerConfigValue, ignoreCase: true, out ProviderType providerType))
            {
                provider = providerType;
            }
        }

        return provider;
    }

    public static AuthenticationType NormalizeAuth(
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string sectionName,
        AuthenticationType currentAuth)
    {
        AuthenticationType auth = currentAuth;

        string? authConfigValue = configuration[$"{sectionName}:Auth"];
        if (!string.IsNullOrWhiteSpace(authConfigValue) &&
            Enum.TryParse(authConfigValue, ignoreCase: true, out AuthenticationType authType))
        {
            auth = authType;
        }

        return auth;
    }

    public static (AuthenticationType Auth, string Endpoint, string Name) ApplyOllamaDefaults(
        AuthenticationType auth,
        string? endpoint,
        string? name,
        string? defaultOllamaEndpoint,
        string? defaultOllamaModel,
        string fallbackEndpoint,
        string fallbackModel)
    {
        AuthenticationType resolvedAuth = auth == default ? AuthenticationType.None : auth;

        string resolvedEndpoint = string.IsNullOrWhiteSpace(endpoint)
            ? (!string.IsNullOrWhiteSpace(defaultOllamaEndpoint) ? defaultOllamaEndpoint! : fallbackEndpoint)
            : endpoint!.TrimEnd('/');

        string resolvedName = string.IsNullOrWhiteSpace(name)
            ? (!string.IsNullOrWhiteSpace(defaultOllamaModel) ? defaultOllamaModel! : fallbackModel)
            : name!;

        return (resolvedAuth, resolvedEndpoint, resolvedName);
    }

    public static AzureOpenAIClient CreateAzureOpenAIClient(
        string endpoint,
        AuthenticationType auth,
        string? key)
    {
        if (auth == AuthenticationType.ApiKey)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("API key is required for ApiKey authentication.");
            }

            return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(key));
        }

        if (auth == AuthenticationType.Identity)
        {
            DefaultAzureCredential credential = new();
            return new AzureOpenAIClient(new Uri(endpoint), credential);
        }

        throw new InvalidOperationException($"Authentication type '{auth}' is not supported. Use ApiKey or Identity.");
    }

    public static AzureOpenAIClient CreateOpenAICompatibleClient(
        string? endpoint,
        string apiKey)
    {
        const string DefaultOpenAIEndpoint = "https://api.openai.com/v1";
        string resolved = string.IsNullOrWhiteSpace(endpoint) ? DefaultOpenAIEndpoint : endpoint!;
        return new AzureOpenAIClient(new Uri(resolved), new ApiKeyCredential(apiKey));
    }

    public static OpenAIClient CreateOpenAIClient(
        string? endpoint,
        string apiKey)
    {
        // If a custom endpoint is provided, use it, otherwise fall back to api.openai.com
        Uri baseUri = new(string.IsNullOrWhiteSpace(endpoint) ? "https://api.openai.com/v1" : endpoint);
        return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = baseUri });
    }
}