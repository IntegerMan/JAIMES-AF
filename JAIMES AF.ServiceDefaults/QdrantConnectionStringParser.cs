using System.Globalization;

namespace MattEland.Jaimes.ServiceDefaults;

/// <summary>
/// Parses Qdrant connection strings to extract host, port, and API key information.
/// Supports various connection string formats including URIs, key-value pairs, and host:port formats.
/// Handles IPv4 and IPv6 addresses correctly.
/// </summary>
public static class QdrantConnectionStringParser
{
    /// <summary>
    /// Applies connection string values to the provided host, port, and API key references.
    /// Connection string can be in various formats:
    /// - URI format: "grpc://host:port?api-key=key"
    /// - Key-value pairs: "Host=host;Port=port;ApiKey=key"
    /// - Simple format: "host:port"
    /// </summary>
    public static void ApplyQdrantConnectionString(
        string connectionString,
        ref string? host,
        ref string? port,
        ref string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri))
        {
            host ??= uri.Host;
            if (uri.Port > 0) port ??= uri.Port.ToString(CultureInfo.InvariantCulture);

            ExtractApiKeyFromQuery(uri.Query, ref apiKey);
            return;
        }

        if (TryParseHostAndPort(connectionString, out string? parsedHost, out string? parsedPort))
        {
            host ??= parsedHost;
            if (string.IsNullOrWhiteSpace(port) && !string.IsNullOrWhiteSpace(parsedPort)) port = parsedPort;
        }

        string[] segments = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string segment in segments)
        {
            string[] keyValue = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (keyValue.Length != 2) continue;

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
                apiKey ??= value;
        }
    }

    private static bool TryParseHostAndPort(string value, out string? host, out string? port)
    {
        host = null;
        port = null;

        if (string.IsNullOrWhiteSpace(value) || value.Contains('=') || value.Contains(';')) return false;

        // Handle IPv6 addresses in brackets: [::1]:6334 or [2001:db8::1]:6334
        if (value.StartsWith('['))
        {
            int closingBracketIndex = value.IndexOf(']');
            if (closingBracketIndex > 0)
            {
                // Extract host (everything between [ and ])
                host = value.Substring(1, closingBracketIndex - 1);

                // Check if there's a port after the closing bracket
                if (closingBracketIndex < value.Length - 1 && value[closingBracketIndex + 1] == ':')
                    port = value.Substring(closingBracketIndex + 2);

                return !string.IsNullOrWhiteSpace(host);
            }
        }

        // Handle IPv4 or unbracketed IPv6 addresses
        // For IPv6 without brackets, the last colon separates the port
        // Count colons to detect IPv6 (IPv6 has multiple colons, IPv4 has only one)
        int colonCount = value.Count(c => c == ':');

        if (colonCount == 1)
        {
            // IPv4 address: host:port
            int lastColonIndex = value.LastIndexOf(':');
            host = value.Substring(0, lastColonIndex);
            port = value.Substring(lastColonIndex + 1);
            return !string.IsNullOrWhiteSpace(host);
        }
        else if (colonCount > 1)
        {
            // Likely IPv6 without brackets - find the last colon as port separator
            // This is ambiguous but we'll assume the last segment is the port
            int lastColonIndex = value.LastIndexOf(':');
            host = value.Substring(0, lastColonIndex);
            port = value.Substring(lastColonIndex + 1);

            // Validate that the port segment is numeric
            if (int.TryParse(port, out _)) return !string.IsNullOrWhiteSpace(host);

            // If port is not numeric, treat the whole value as host (no port)
            host = value;
            port = null;
            return true;
        }

        // No colon found - treat entire value as host
        host = value;
        return true;
    }

    private static void ExtractApiKeyFromQuery(string query, ref string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(query) || !string.IsNullOrWhiteSpace(apiKey)) return;

        string trimmedQuery = query.TrimStart('?');
        string[] pairs = trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (string pair in pairs)
        {
            string[] keyValue = pair.Split('=', 2);
            if (keyValue.Length != 2) continue;

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