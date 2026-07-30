using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Infrastructure.Catalog;
using ArchiveIndexer.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Catalog;

public class ZipCatalogTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private ZipCatalog CreateCatalog()
    {
        var settings = Options.Create(new ArchiveSettings { IndexPath = _temp.Path });
        return new ZipCatalog(settings, Mock.Of<ILogger<ZipCatalog>>());
    }

    private FileInfo CreateRealZipFile(string name, int sizeBytes = 100)
    {
        var path = _temp.GetSubPath(name);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        return new FileInfo(path);
    }

    [Fact]
    public async Task NeedsIndexingAsync_UnknownFile_ReturnsTrue()
    {
        var catalog = CreateCatalog();
        var file = CreateRealZipFile("new.zip");

        var result = await catalog.NeedsIndexingAsync(file, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task NeedsIndexingAsync_AfterUpdate_UnchangedFile_ReturnsFalse()
    {
        var catalog = CreateCatalog();
        var file = CreateRealZipFile("stable.zip");

        await catalog.UpdateAsync(file, CancellationToken.None);

        // Re-read from disk, exactly as ArchiveScanner/ArchiveWatcher would on the next pass.
        var sameFile = new FileInfo(file.FullName);

        var result = await catalog.NeedsIndexingAsync(sameFile, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task NeedsIndexingAsync_FileSizeChangedSinceUpdate_ReturnsTrue()
    {
        var catalog = CreateCatalog();
        var file = CreateRealZipFile("grows.zip", sizeBytes: 100);

        await catalog.UpdateAsync(file, CancellationToken.None);

        File.WriteAllBytes(file.FullName, new byte[500]);
        var changedFile = new FileInfo(file.FullName);

        var result = await catalog.NeedsIndexingAsync(changedFile, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task NeedsIndexingAsync_LastWriteTimeChangedSinceUpdate_ReturnsTrue()
    {
        var catalog = CreateCatalog();
        var file = CreateRealZipFile("touched.zip");

        await catalog.UpdateAsync(file, CancellationToken.None);

        File.SetLastWriteTimeUtc(file.FullName, DateTime.UtcNow.AddDays(1));
        var touchedFile = new FileInfo(file.FullName);

        var result = await catalog.NeedsIndexingAsync(touchedFile, CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task RemoveAsync_RemovesEntry_SoItNeedsIndexingAgain()
    {
        var catalog = CreateCatalog();
        var file = CreateRealZipFile("removed.zip");

        await catalog.UpdateAsync(file, CancellationToken.None);
        await catalog.RemoveAsync(file.FullName, CancellationToken.None);

        var result = await catalog.NeedsIndexingAsync(new FileInfo(file.FullName), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task RemoveAsync_PathNeverCataloged_DoesNotThrow()
    {
        var catalog = CreateCatalog();

        var exception = await Record.ExceptionAsync(() =>
            catalog.RemoveAsync(@"D:\never\existed.zip", CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetAllZipPathsAsync_ReturnsEveryUpdatedPath()
    {
        var catalog = CreateCatalog();
        var file1 = CreateRealZipFile("a.zip");
        var file2 = CreateRealZipFile("b.zip");

        await catalog.UpdateAsync(file1, CancellationToken.None);
        await catalog.UpdateAsync(file2, CancellationToken.None);

        var paths = await catalog.GetAllZipPathsAsync(CancellationToken.None);

        Assert.Contains(file1.FullName, paths);
        Assert.Contains(file2.FullName, paths);
        Assert.Equal(2, paths.Count);
    }

    [Fact]
    public async Task GetAllZipPathsAsync_NothingCataloged_ReturnsEmpty()
    {
        var catalog = CreateCatalog();

        var paths = await catalog.GetAllZipPathsAsync(CancellationToken.None);

        Assert.Empty(paths);
    }

    [Fact]
    public async Task Entries_PersistAcrossNewCatalogInstances()
    {
        var file = CreateRealZipFile("persisted.zip");

        var firstInstance = CreateCatalog();
        await firstInstance.UpdateAsync(file, CancellationToken.None);

        // A brand-new instance pointed at the same IndexPath should load the
        // previously saved ZipCatalog.json from disk - this is exactly what
        // happens on every Worker restart.
        var secondInstance = CreateCatalog();

        var result = await secondInstance.NeedsIndexingAsync(new FileInfo(file.FullName), CancellationToken.None);

        Assert.False(result);
    }

    public void Dispose() => _temp.Dispose();
}
