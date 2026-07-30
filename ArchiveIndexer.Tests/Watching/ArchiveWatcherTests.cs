using System.Diagnostics;
using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Infrastructure.Watching;
using ArchiveIndexer.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Watching;

/// <summary>
/// Exercises the live FileSystemWatcher-backed ArchiveWatcher against a real
/// temp directory. These are inherently timing-dependent (real OS filesystem
/// events, dispatched via Task.Run) - assertions poll with a generous timeout
/// rather than asserting on a fixed delay wherever there's a positive event to
/// wait for. The two "nothing should happen" tests are the exception, since
/// there's no positive signal to poll for absence of an event.
/// </summary>
public class ArchiveWatcherTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private (ArchiveWatcher Watcher, Mock<IArchiveProcessor> Processor, Mock<IZipCatalog> Catalog, Mock<IZipRemovalService> RemovalService)
        CreateWatcher(bool needsIndexing = true)
    {
        var settings = Options.Create(new ArchiveSettings { ArchiveRoot = _temp.Path });
        var processor = new Mock<IArchiveProcessor>();

        var catalog = new Mock<IZipCatalog>();
        catalog.Setup(c => c.NeedsIndexingAsync(It.IsAny<FileInfo>(), It.IsAny<CancellationToken>())).ReturnsAsync(needsIndexing);

        var removalService = new Mock<IZipRemovalService>();

        var watcher = new ArchiveWatcher(settings, processor.Object, catalog.Object, removalService.Object, Mock.Of<ILogger<ArchiveWatcher>>());

        return (watcher, processor, catalog, removalService);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
                return;

            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task Start_NewZipFileCreated_IsProcessedAndCataloged()
    {
        var (watcher, processor, catalog, _) = CreateWatcher();

        try
        {
            watcher.Start();

            var zipPath = _temp.GetSubPath("new.zip");
            File.WriteAllBytes(zipPath, new byte[10]);

            await WaitUntilAsync(() => processor.Invocations.Count > 0, TimeSpan.FromSeconds(10));

            processor.Verify(p => p.ProcessAsync(zipPath, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            catalog.Verify(c => c.UpdateAsync(It.IsAny<FileInfo>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
        finally
        {
            watcher.Stop();
        }
    }

    [Fact]
    public async Task Start_ZipFileDeleted_TriggersRemovalService()
    {
        var (watcher, _, _, removalService) = CreateWatcher();

        // Created before Start(), so the watcher never sees a Created event for
        // it - isolates this test to just the deletion path.
        var zipPath = _temp.GetSubPath("to-delete.zip");
        File.WriteAllBytes(zipPath, new byte[10]);

        try
        {
            watcher.Start();

            File.Delete(zipPath);

            await WaitUntilAsync(() => removalService.Invocations.Count > 0, TimeSpan.FromSeconds(10));

            removalService.Verify(r => r.RemoveZipAsync(zipPath, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
        finally
        {
            watcher.Stop();
        }
    }

    [Fact]
    public async Task Start_NonZipFileCreated_IsIgnored()
    {
        var (watcher, processor, _, _) = CreateWatcher();

        try
        {
            watcher.Start();

            File.WriteAllText(_temp.GetSubPath("readme.txt"), "hello");

            // No positive event to poll for here - a fixed wait is unavoidable
            // for a "this should NOT happen" assertion.
            await Task.Delay(1000);

            processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            watcher.Stop();
        }
    }

    [Fact]
    public async Task Start_FileCatalogSaysUnchanged_IsNotProcessed()
    {
        var (watcher, processor, catalog, _) = CreateWatcher(needsIndexing: false);

        try
        {
            watcher.Start();

            var zipPath = _temp.GetSubPath("unchanged.zip");
            File.WriteAllBytes(zipPath, new byte[10]);

            await WaitUntilAsync(
                () => catalog.Invocations.Any(i => i.Method.Name == nameof(IZipCatalog.NeedsIndexingAsync)),
                TimeSpan.FromSeconds(10));

            await Task.Delay(500); // let any (incorrect) processing settle

            processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            watcher.Stop();
        }
    }

    [Fact]
    public async Task Stop_NoLongerRaisesEventsForNewFiles()
    {
        var (watcher, processor, _, _) = CreateWatcher();

        watcher.Start();
        watcher.Stop();

        var zipPath = _temp.GetSubPath("after-stop.zip");
        File.WriteAllBytes(zipPath, new byte[10]);

        await Task.Delay(1000);

        processor.Verify(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Start_ProcessorThrows_DoesNotCrashTheWatcher()
    {
        var (watcher, processor, _, _) = CreateWatcher();
        processor.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated processing failure"));

        try
        {
            watcher.Start();

            var zipPath = _temp.GetSubPath("bad.zip");
            File.WriteAllBytes(zipPath, new byte[10]);

            await WaitUntilAsync(() => processor.Invocations.Count > 0, TimeSpan.FromSeconds(10));

            // Reaching this line at all (test process still alive, no unhandled
            // exception) is the assertion - ProcessZipAsync's try/catch must
            // swallow this rather than crash anything.
            await Task.Delay(200);
        }
        finally
        {
            watcher.Stop();
        }
    }

    public void Dispose() => _temp.Dispose();
}
