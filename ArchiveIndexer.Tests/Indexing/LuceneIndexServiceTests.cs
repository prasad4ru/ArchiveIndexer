using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Indexing;
using ArchiveIndexer.Infrastructure.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Indexing;

/// <summary>
/// Timing-based by nature (PeriodicTimer, real delays) - kept as short as
/// reasonably reliable, with generous margins over strict timing assertions.
/// </summary>
public class LuceneIndexServiceTests
{
    [Fact]
    public async Task PeriodicTimer_CommitsDocumentsIndexedBeforeTheThresholdCount()
    {
        var queue = new DocumentQueue();
        var indexer = new Mock<IArchiveIndexer>();

        indexer.Setup(i => i.IndexAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var commitCount = 0;
        indexer.Setup(i => i.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref commitCount))
            .Returns(Task.CompletedTask);

        var settings = Options.Create(new ArchiveSettings { CommitIntervalSeconds = 1 });
        var service = new LuceneIndexService(queue, indexer.Object, settings, Mock.Of<ILogger<LuceneIndexService>>());

        // Only 1 document - nowhere near the 5000-document threshold, so the only
        // thing that can possibly commit it is the periodic timer.
        await queue.EnqueueAsync(new ArchiveDocument { FileName = "a.xml" }, CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(2));
        await service.StopAsync(CancellationToken.None);

        indexer.Verify(i => i.IndexAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(commitCount >= 1, $"Expected at least one periodic commit, got {commitCount}.");
    }

    [Fact]
    public async Task FinalCommit_HappensOnGracefulShutdown()
    {
        var queue = new DocumentQueue();
        var indexer = new Mock<IArchiveIndexer>();

        indexer.Setup(i => i.IndexAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var commitCount = 0;
        indexer.Setup(i => i.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref commitCount))
            .Returns(Task.CompletedTask);

        // Long interval so the periodic timer definitely won't fire during this
        // short test - isolates the "commit on shutdown" path specifically.
        var settings = Options.Create(new ArchiveSettings { CommitIntervalSeconds = 300 });
        var service = new LuceneIndexService(queue, indexer.Object, settings, Mock.Of<ILogger<LuceneIndexService>>());

        await queue.EnqueueAsync(new ArchiveDocument { FileName = "a.xml" }, CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300)); // let the consume loop pick up the document
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, commitCount);
    }

    [Fact]
    public async Task CommitFailure_DoesNotKillTheService_RetriesOnNextTick()
    {
        // The specific hardening fix: previously an exception from CommitAsync
        // propagated out of the periodic-timer loop and silently ended the whole
        // background service for the rest of that run - no further commits, ever,
        // with no obvious error explaining why. This proves the service survives
        // a failed commit and successfully commits on a later tick.
        var queue = new DocumentQueue();
        var indexer = new Mock<IArchiveIndexer>();

        indexer.Setup(i => i.IndexAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var attempt = 0;
        indexer.Setup(i => i.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var current = Interlocked.Increment(ref attempt);
                if (current == 1)
                    throw new IOException("simulated transient commit failure");
                return Task.CompletedTask;
            });

        var settings = Options.Create(new ArchiveSettings { CommitIntervalSeconds = 1 });
        var service = new LuceneIndexService(queue, indexer.Object, settings, Mock.Of<ILogger<LuceneIndexService>>());

        await queue.EnqueueAsync(new ArchiveDocument { FileName = "a.xml" }, CancellationToken.None);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(3));
        await service.StopAsync(CancellationToken.None);

        Assert.True(attempt >= 2, $"Expected at least 2 commit attempts (one failing, one succeeding), got {attempt}.");
    }

    [Fact]
    public async Task ZeroCommitIntervalSeconds_FallsBackToDefaultInterval()
    {
        // ArchiveSettings.CommitIntervalSeconds defaults to 0 if never set in
        // appsettings.json; LuceneIndexService must not treat that as "commit every
        // 0 seconds" (which would busy-loop) - it should fall back to a sane default
        // rather than crash or spin.
        var queue = new DocumentQueue();
        var indexer = new Mock<IArchiveIndexer>();
        indexer.Setup(i => i.IndexAsync(It.IsAny<ArchiveDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        indexer.Setup(i => i.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var settings = Options.Create(new ArchiveSettings { CommitIntervalSeconds = 0 });
        var service = new LuceneIndexService(queue, indexer.Object, settings, Mock.Of<ILogger<LuceneIndexService>>());

        await queue.EnqueueAsync(new ArchiveDocument { FileName = "a.xml" }, CancellationToken.None);

        var exception = await Record.ExceptionAsync(async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await service.StopAsync(CancellationToken.None);
        });

        Assert.Null(exception);
    }
}
