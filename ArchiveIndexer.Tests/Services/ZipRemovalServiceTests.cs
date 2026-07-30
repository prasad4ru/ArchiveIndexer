using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Services;

public class ZipRemovalServiceTests
{
    [Fact]
    public async Task RemoveZipAsync_CallsDeleteThenCommitThenCatalogRemove_InOrder()
    {
        var indexer = new Mock<IArchiveIndexer>();
        var catalog = new Mock<IZipCatalog>();
        var logger = Mock.Of<ILogger<ZipRemovalService>>();

        var callOrder = new List<string>();

        indexer.Setup(i => i.DeleteByZipPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("delete"))
            .Returns(Task.CompletedTask);

        indexer.Setup(i => i.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("commit"))
            .Returns(Task.CompletedTask);

        catalog.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("catalog-remove"))
            .Returns(Task.CompletedTask);

        var service = new ZipRemovalService(indexer.Object, catalog.Object, logger);

        await service.RemoveZipAsync(@"D:\MarsArchive\old.zip", CancellationToken.None);

        Assert.Equal(new[] { "delete", "commit", "catalog-remove" }, callOrder);
    }

    [Fact]
    public async Task RemoveZipAsync_PassesTheSameZipPathToIndexerAndCatalog()
    {
        var indexer = new Mock<IArchiveIndexer>();
        var catalog = new Mock<IZipCatalog>();
        var logger = Mock.Of<ILogger<ZipRemovalService>>();

        const string zipPath = @"D:\MarsArchive\old.zip";

        var service = new ZipRemovalService(indexer.Object, catalog.Object, logger);

        await service.RemoveZipAsync(zipPath, CancellationToken.None);

        indexer.Verify(i => i.DeleteByZipPathAsync(zipPath, It.IsAny<CancellationToken>()), Times.Once);
        catalog.Verify(c => c.RemoveAsync(zipPath, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveZipAsync_CommitsExactlyOnce()
    {
        var indexer = new Mock<IArchiveIndexer>();
        var catalog = new Mock<IZipCatalog>();
        var logger = Mock.Of<ILogger<ZipRemovalService>>();

        var service = new ZipRemovalService(indexer.Object, catalog.Object, logger);

        await service.RemoveZipAsync(@"D:\zip.zip", CancellationToken.None);

        indexer.Verify(i => i.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveZipAsync_WhenDeleteThrows_DoesNotCallCommitOrCatalogRemove()
    {
        var indexer = new Mock<IArchiveIndexer>();
        var catalog = new Mock<IZipCatalog>();
        var logger = Mock.Of<ILogger<ZipRemovalService>>();

        indexer.Setup(i => i.DeleteByZipPathAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk error"));

        var service = new ZipRemovalService(indexer.Object, catalog.Object, logger);

        await Assert.ThrowsAsync<IOException>(() =>
            service.RemoveZipAsync(@"D:\zip.zip", CancellationToken.None));

        indexer.Verify(i => i.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        catalog.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
