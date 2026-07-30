using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Infrastructure.Scanning;
using ArchiveIndexer.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Scanning;

public class ArchiveScannerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private ArchiveScanner CreateScanner(Mock<IZipCatalog> catalog, Mock<IArchiveProcessor> processor, Mock<IZipRemovalService> removalService, string? archiveRoot = null)
    {
        var settings = Options.Create(new ArchiveSettings { ArchiveRoot = archiveRoot ?? _temp.Path });
        return new ArchiveScanner(settings, catalog.Object, processor.Object, removalService.Object, Mock.Of<ILogger<ArchiveScanner>>());
    }

    private static Mock<IZipCatalog> DefaultCatalogAlwaysNeedsIndexing()
    {
        var catalog = new Mock<IZipCatalog>();
        catalog.Setup(c => c.NeedsIndexingAsync(It.IsAny<FileInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        catalog.Setup(c => c.GetAllZipPathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        return catalog;
    }

    private string CreateDummyZip(string relativePath)
    {
        var path = _temp.GetSubPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[10]);
        return path;
    }

    [Fact]
    public async Task ScanAsync_ProcessesEveryZipFoundOnDisk()
    {
        CreateDummyZip("a.zip");
        CreateDummyZip("b.zip");
        CreateDummyZip("c.zip");

        var catalog = DefaultCatalogAlwaysNeedsIndexing();
        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        catalog.Verify(c => c.UpdateAsync(It.IsAny<FileInfo>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ScanAsync_SkipsZipsTheCatalogSaysAreUnchanged()
    {
        var unchangedPath = CreateDummyZip("unchanged.zip");
        CreateDummyZip("changed.zip");

        var catalog = new Mock<IZipCatalog>();
        catalog.Setup(c => c.NeedsIndexingAsync(
                It.Is<FileInfo>(f => f.FullName == unchangedPath), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        catalog.Setup(c => c.NeedsIndexingAsync(
                It.Is<FileInfo>(f => f.FullName != unchangedPath), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        catalog.Setup(c => c.GetAllZipPathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        processor.Verify(p => p.ProcessAsync(unchangedPath, It.IsAny<CancellationToken>()), Times.Never);
        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_OneZipThrowsDuringProcessing_OtherZipsAreStillProcessed()
    {
        // This is the specific resilience fix: previously an exception from one ZIP
        // (e.g. an unparseable name) aborted the entire scan, silently skipping
        // every ZIP still left in the batch.
        var badPath = CreateDummyZip("bad.zip");
        CreateDummyZip("good1.zip");
        CreateDummyZip("good2.zip");

        var catalog = DefaultCatalogAlwaysNeedsIndexing();

        var processor = new Mock<IArchiveProcessor>();
        processor.Setup(p => p.ProcessAsync(badPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("Invalid archive filename"));

        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        var exception = await Record.ExceptionAsync(() => scanner.ScanAsync(CancellationToken.None));

        Assert.Null(exception); // ScanAsync itself must not throw
        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ScanAsync_FailedZip_DoesNotUpdateCatalog_SoItIsRetriedNextScan()
    {
        var badPath = CreateDummyZip("bad.zip");

        var catalog = DefaultCatalogAlwaysNeedsIndexing();

        var processor = new Mock<IArchiveProcessor>();
        processor.Setup(p => p.ProcessAsync(badPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FormatException("Invalid archive filename"));

        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        catalog.Verify(c => c.UpdateAsync(It.IsAny<FileInfo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_ArchiveRootDoesNotExist_ReturnsWithoutThrowing()
    {
        var catalog = DefaultCatalogAlwaysNeedsIndexing();
        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService, archiveRoot: _temp.GetSubPath("does-not-exist"));

        var exception = await Record.ExceptionAsync(() => scanner.ScanAsync(CancellationToken.None));

        Assert.Null(exception);
        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_NonZipFiles_AreIgnored()
    {
        CreateDummyZip("real.zip");
        File.WriteAllText(_temp.GetSubPath("readme.txt"), "not a zip");

        var catalog = DefaultCatalogAlwaysNeedsIndexing();
        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_ZipsInSubdirectories_AreAlsoFound()
    {
        CreateDummyZip(Path.Combine("Q1", "Week1", "nested.zip"));

        var catalog = DefaultCatalogAlwaysNeedsIndexing();
        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_CatalogedPathNoLongerOnDisk_CallsRemovalService()
    {
        // Simulates a ZIP that was deleted while the service was stopped - the only
        // case the live watcher's Deleted handler can't catch by itself.
        const string missingPath = @"D:\MarsArchive\gone.zip";

        var catalog = new Mock<IZipCatalog>();
        catalog.Setup(c => c.GetAllZipPathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { missingPath });

        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        removalService.Verify(r => r.RemoveZipAsync(missingPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_CatalogedPathStillOnDisk_DoesNotCallRemovalService()
    {
        var stillPresentPath = CreateDummyZip("still-here.zip");

        var catalog = DefaultCatalogAlwaysNeedsIndexing();
        catalog.Setup(c => c.GetAllZipPathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { stillPresentPath });

        var processor = new Mock<IArchiveProcessor>();
        var removalService = new Mock<IZipRemovalService>();

        var scanner = CreateScanner(catalog, processor, removalService);

        await scanner.ScanAsync(CancellationToken.None);

        removalService.Verify(r => r.RemoveZipAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_RemovalServiceThrowsForOneMissingZip_OtherMissingZipsAreStillReconciled()
    {
        const string badPath = @"D:\MarsArchive\bad-gone.zip";
        const string goodPath = @"D:\MarsArchive\good-gone.zip";

        var catalog = new Mock<IZipCatalog>();
        catalog.Setup(c => c.GetAllZipPathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { badPath, goodPath });

        var processor = new Mock<IArchiveProcessor>();

        var removalService = new Mock<IZipRemovalService>();
        removalService.Setup(r => r.RemoveZipAsync(badPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("locked"));

        var scanner = CreateScanner(catalog, processor, removalService);

        var exception = await Record.ExceptionAsync(() => scanner.ScanAsync(CancellationToken.None));

        Assert.Null(exception);
        removalService.Verify(r => r.RemoveZipAsync(goodPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    public void Dispose() => _temp.Dispose();
}
