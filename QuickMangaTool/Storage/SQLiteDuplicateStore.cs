using Microsoft.Data.Sqlite;
using System.Globalization;

static class SQLiteDuplicateStore
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS duplicates (
            phash TEXT PRIMARY KEY
        );
        """;

    public static void EnsureDatabase(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText = CreateTableSql;
        create.ExecuteNonQuery();
    }

    public static ulong[] LoadAllHashes(string dbPath)
    {
        if (!File.Exists(dbPath))
            return [];

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT phash FROM duplicates;";

        using var reader = command.ExecuteReader();
        var hashes = new List<ulong>();

        while (reader.Read())
        {
            if (reader.IsDBNull(0))
                continue;

            var value = reader.GetString(0);
            if (TryParseHash(value, out ulong parsed))
                hashes.Add(parsed);
        }

        return hashes.ToArray();
    }

    public static int AddHashes(string dbPath, IEnumerable<ulong> hashes)
    {
        EnsureDatabase(dbPath);

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO duplicates(phash) VALUES ($phash);";
        var hashParameter = command.CreateParameter();
        hashParameter.ParameterName = "$phash";
        command.Parameters.Add(hashParameter);

        int inserted = 0;

        foreach (var hash in hashes)
        {
            hashParameter.Value = ToStorage(hash);
            int affected = command.ExecuteNonQuery();
            inserted += affected;
        }

        transaction.Commit();
        return inserted;
    }

    public static string ToStorage(ulong hash) => hash.ToString("x16", CultureInfo.InvariantCulture);

    public static bool TryParseHash(string value, out ulong hash)
    {
        hash = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out hash);
    }
}
