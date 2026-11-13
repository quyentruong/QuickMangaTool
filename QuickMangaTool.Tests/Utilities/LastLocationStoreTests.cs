namespace QuickMangaTool.Tests.Utilities;

[Collection("AppPathsEnv")]
public class LastLocationStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly string? _originalConfigDir;

    public LastLocationStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "QuickMangaTool_LastLocTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _originalConfigDir = Environment.GetEnvironmentVariable(AppPaths.ConfigDirEnvVar);
        Environment.SetEnvironmentVariable(AppPaths.ConfigDirEnvVar, _testDir);
        LastLocationStore.ClearLastDirectory();
    }

    public void Dispose()
    {
        LastLocationStore.ClearLastDirectory();
        Environment.SetEnvironmentVariable(AppPaths.ConfigDirEnvVar, _originalConfigDir);
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch
        {
            // Ignore test cleanup
        }
    }

    [Fact]
    public void SaveAndGetLastDirectory_ReturnsCorrectPath()
    {
        LastLocationStore.SaveLastDirectory(_testDir);
        var loaded = LastLocationStore.GetLastDirectory();

        Assert.NotNull(loaded);
        Assert.Equal(Path.GetFullPath(_testDir), loaded);
    }

    [Fact]
    public void GetLastDirectory_ReturnsNull_WhenDirectoryWasDeleted()
    {
        var tempDeleted = Path.Combine(_testDir, "WillBeDeleted");
        Directory.CreateDirectory(tempDeleted);

        LastLocationStore.SaveLastDirectory(tempDeleted);
        Assert.NotNull(LastLocationStore.GetLastDirectory());

        Directory.Delete(tempDeleted);
        Assert.Null(LastLocationStore.GetLastDirectory());
    }

    [Fact]
    public void ClearLastDirectory_RemovesSavedEntry()
    {
        LastLocationStore.SaveLastDirectory(_testDir);
        Assert.NotNull(LastLocationStore.GetLastDirectory());

        LastLocationStore.ClearLastDirectory();
        Assert.Null(LastLocationStore.GetLastDirectory());
    }

    [Fact]
    public void SaveLastDirectory_IgnoresNullOrEmpty()
    {
        LastLocationStore.SaveLastDirectory(null!);
        LastLocationStore.SaveLastDirectory("");
        LastLocationStore.SaveLastDirectory("   ");

        Assert.Null(LastLocationStore.GetLastDirectory());
    }
}
