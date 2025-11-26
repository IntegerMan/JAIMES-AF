using System.Globalization;

namespace MattEland.Jaimes.Workers.DocumentChunking.Configuration;

public static class QdrantConnectionStringParser
{
    public static void ApplyQdrantConnectionString(
        string connectionString,
        ref string? host,
        ref string? port,
        ref string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri))
        {
            host ??= uri.Host;
            if (uri.Port > 0)
            {
                port ??= uri.Port.ToString(CultureInfo.InvariantCulture);
            }

            ExtractApiKeyFromQuery(uri.Query, ref apiKey);
            return;
        }

        if (TryParseHostAndPort(connectionString, out string? parsedHost, out string? parsedPort))
        {
            host ??= parsedHost;
            if (string.IsNullOrWhiteSpace(port) && !string.IsNullOrWhiteSpace(parsedPort))
            {
                port = parsedPort;
            }
        }

        string[] segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            string[] keyValue = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length != 2)
            {
                continue;
            }

            string key = keyValue[0];
            string value = keyValue[1];

            if (string.Equals(key, "Endpoint", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Uri", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "GrpcUri", StringComparison.OrdinalIgnoreCase))
            {
                ApplyQdrantConnectionString(value, ref host, ref port, ref apiKey);
                continue;
            }

            if (string.Equals(key, "Host", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Hostname", StringComparison.OrdinalIgnoreCase))
            {
                host ??= value;
                continue;
            }

            if (string.Equals(key, "Port", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "GrpcPort", StringComparison.OrdinalIgnoreCase))
            {
                port ??= value;
                continue;
            }

            if (string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Api-Key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Api_Key", StringComparison.OrdinalIgnoreCase))
            {
                apiKey ??= value;
            }
        }
    }

    private static bool TryParseHostAndPort(string value, out string? host, out string? port)
    {
        host = null;
        port = null;

        if (string.IsNullOrWhiteSpace(value) || value.Contains('='))
        {
            return false;
        }

        string[] hostParts = value.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (hostParts.Length == 0)
        {
            return false;
        }

        host = hostParts[0];
        if (hostParts.Length > 1)
        {
            port = hostParts[1];
        }

        return true;
    }

    private static void ExtractApiKeyFromQuery(string query, ref string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(query) || !string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        string trimmedQuery = query.TrimStart('?');
        string[] pairs = trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split('=', 2);
            if (keyValue.Length != 2)
            {
                continue;
            }

            string key = keyValue[0];
            if (string.Equals(key, "api-key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "apikey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "api_key", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = Uri.UnescapeDataString(keyValue[1]);
                break;
            }
        }
    }
}



