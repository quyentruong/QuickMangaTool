using Microsoft.Data.Sqlite;

namespace QuickMangaTool.Tests.Storage;

/// <summary>
/// Unit tests for <see cref="SQLiteDuplicateStore"/>.
/// </summary>
public class SQLiteDuplicateStoreTests
{
    [Fact]
    public void AddHashes_AndLoadAllHashes_RoundTripsValues()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"QuickMangaTool_{Guid.NewGuid():N}.db");

        try
        {
            var hashes = new ulong[] { 0x0123456789ABCDEF, 0x0123456789ABCDEF, 0x1111111111111111 };

            var inserted = SQLiteDuplicateStore.AddHashes(dbPath, hashes);
            var loaded = SQLiteDuplicateStore.LoadAllHashes(dbPath);

            Assert.Equal(2, inserted);
            Assert.Contains(0x0123456789ABCDEFUL, loaded);
            Assert.Contains(0x1111111111111111UL, loaded);
            Assert.Equal(2, loaded.Length);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }
}
