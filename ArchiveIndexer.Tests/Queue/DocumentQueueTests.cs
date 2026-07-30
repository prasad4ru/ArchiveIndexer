using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Queue;
using Xunit;

namespace ArchiveIndexer.Tests.Queue;

public class DocumentQueueTests
{
    [Fact]
    public async Task EnqueueThenRead_ReturnsTheSameDocument()
    {
        var queue = new DocumentQueue();
        var doc = new ArchiveDocument { FileName = "test.xml" };

        await queue.EnqueueAsync(doc, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await foreach (var read in queue.ReadAllAsync(cts.Token))
        {
            Assert.Same(doc, read);
            return;
        }

        Assert.Fail("Expected to read one document from the queue but got none.");
    }

    [Fact]
    public async Task EnqueueMultiple_PreservesOrder()
    {
        var queue = new DocumentQueue();

        var doc1 = new ArchiveDocument { FileName = "1.xml" };
        var doc2 = new ArchiveDocument { FileName = "2.xml" };
        var doc3 = new ArchiveDocument { FileName = "3.xml" };

        await queue.EnqueueAsync(doc1, CancellationToken.None);
        await queue.EnqueueAsync(doc2, CancellationToken.None);
        await queue.EnqueueAsync(doc3, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var results = new List<string>();

        await foreach (var doc in queue.ReadAllAsync(cts.Token))
        {
            results.Add(doc.FileName);
            if (results.Count == 3)
                break;
        }

        Assert.Equal(new[] { "1.xml", "2.xml", "3.xml" }, results);
    }

    [Fact]
    public async Task ReadAllAsync_RespectsCancellation()
    {
        var queue = new DocumentQueue();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in queue.ReadAllAsync(cts.Token))
            {
                // Nothing was ever enqueued - this should throw due to the
                // already-cancelled token rather than hang waiting for an item.
            }
        });
    }
}
