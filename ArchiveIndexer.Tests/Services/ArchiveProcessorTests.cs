using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Services;

public class ArchiveProcessorTests
{
    private static async IAsyncEnumerable<ArchiveDocument> ToAsyncEnumerable(IEnumerable<ArchiveDocument> docs)
    {
        foreach (var doc in docs)
        {
            yield return doc;
            await Task.Yield();
        }
    }

    [Fact]
    public async Task ProcessAsync_EnqueuesEveryDocumentFromScanner()
    {
        var docs = new[]
        {
            new ArchiveDocument { FileName = "1.xml" },
            new ArchiveDocument { FileName = "2.xml" },
            new ArchiveDocument { FileName = "3.xml" }
        };

        var scanner = new Mock<IZipScanner>();
        scanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(docs));

        var queue = new Mock<IDocumentQueue>();
        var enqueued = new List<ArchiveDocument>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()))
            .Callback<ArchiveDocument, CancellationToken>((d, _) => enqueued.Add(d))
            .Returns(ValueTask.CompletedTask);

        var processor = new ArchiveProcessor(scanner.Object, queue.Object, Mock.Of<ILogger<ArchiveProcessor>>());

        await processor.ProcessAsync(@"D:\zip.zip", CancellationToken.None);

        Assert.Equal(3, enqueued.Count);
        Assert.Equal(new[] { "1.xml", "2.xml", "3.xml" }, enqueued.Select(d => d.FileName));
    }

    [Fact]
    public async Task ProcessAsync_NoDocumentsFromScanner_EnqueuesNothing()
    {
        var scanner = new Mock<IZipScanner>();
        scanner.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(Array.Empty<ArchiveDocument>()));

        var queue = new Mock<IDocumentQueue>();

        var processor = new ArchiveProcessor(scanner.Object, queue.Object, Mock.Of<ILogger<ArchiveProcessor>>());

        await processor.ProcessAsync(@"D:\empty.zip", CancellationToken.None);

        queue.Verify(q => q.EnqueueAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_PassesTheZipPathToTheScanner()
    {
        const string zipPath = @"D:\MarsArchive\Feb_16_2022_06_12_13.zip";

        var scanner = new Mock<IZipScanner>();
        scanner.Setup(s => s.ScanAsync(zipPath, It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(Array.Empty<ArchiveDocument>()));

        var queue = new Mock<IDocumentQueue>();

        var processor = new ArchiveProcessor(scanner.Object, queue.Object, Mock.Of<ILogger<ArchiveProcessor>>());

        await processor.ProcessAsync(zipPath, CancellationToken.None);

        scanner.Verify(s => s.ScanAsync(zipPath, It.IsAny<CancellationToken>()), Times.Once);
    }
}
