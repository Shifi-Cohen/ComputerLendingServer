using Npgsql;

namespace ComputerLending.API.Configuration;

public static class NpgsqlConnectionStrings
{
    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        var builder = IsUri(trimmed)
            ? FromUri(trimmed)
            : new NpgsqlConnectionStringBuilder(trimmed);

        builder.GssEncryptionMode = GssEncryptionMode.Disable;
        return builder.ConnectionString;
    }

    private static bool IsUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static NpgsqlConnectionStringBuilder FromUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host)
            || string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not a valid postgres URI. Use Host=...;Port=5432;Database=...;Username=...;Password=...");
        }

        var separator = uri.UserInfo.IndexOf(':');
        var username = Uri.UnescapeDataString(separator >= 0 ? uri.UserInfo[..separator] : uri.UserInfo);
        var password = separator >= 0 ? Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]) : string.Empty;
        var database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = database,
            Username = username,
            Password = password
        };

        if (RequiresSsl(uri))
            builder.SslMode = SslMode.Require;

        return builder;
    }

    private static bool RequiresSsl(Uri uri)
    {
        if (uri.Host.Contains('.'))
            return true;

        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString(eq >= 0 ? part[..eq] : part);
            var sslMode = Uri.UnescapeDataString(eq >= 0 ? part[(eq + 1)..] : string.Empty);
            if (!key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                continue;

            if (sslMode.Equals("require", StringComparison.OrdinalIgnoreCase)
                || sslMode.Equals("verify-ca", StringComparison.OrdinalIgnoreCase)
                || sslMode.Equals("verify-full", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
