using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ArchiveIndexer.Infrastructure.Watching
{
    public sealed class ArchiveWatcher : IArchiveWatcher, IDisposable
    {
        private readonly ArchiveSettings _settings;
        private readonly IArchiveProcessor _processor;
        private readonly IZipCatalog _catalog;
        private readonly IZipRemovalService _removalService;
        private readonly ILogger<ArchiveWatcher> _logger;
        private readonly ConcurrentDictionary<string, byte> _processing = new(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher? _watcher;

        public ArchiveWatcher(IOptions<ArchiveSettings> options, IArchiveProcessor processor, IZipCatalog catalog, IZipRemovalService removalService, ILogger<ArchiveWatcher> logger)
        {
            _settings = options.Value;
            _processor = processor;
            _catalog = catalog;
            _removalService = removalService;
            _logger = logger;
        }

        public void Start()
        {
            Directory.CreateDirectory(_settings.ArchiveRoot);

            _watcher = new FileSystemWatcher(_settings.ArchiveRoot)
            {
                Filter = "*.zip",
                IncludeSubdirectories = true,

                NotifyFilter =
                    NotifyFilters.FileName |
                    NotifyFilters.LastWrite |
                    NotifyFilters.CreationTime |
                    NotifyFilters.Size,
                InternalBufferSize = 64 * 1024
            };

            _watcher.Created += OnZipChanged;
            _watcher.Changed += OnZipChanged;
            _watcher.Renamed += OnZipRenamed;
            _watcher.Deleted += OnZipDeleted;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation("ArchiveWatcher started. Watching: {Path}", _settings.ArchiveRoot);
        }

        private void OnZipChanged(object? sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return;

            _ = Task.Run(() => ProcessZipAsync(e.FullPath));
        }

        private void OnZipRenamed(object? sender, RenamedEventArgs e)
        {
            if (e.OldFullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                _ = Task.Run(() => HandleZipRemovedAsync(e.OldFullPath));
            }

            if (!e.FullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return;

            _ = Task.Run(() => ProcessZipAsync(e.FullPath));
        }

        private void OnZipDeleted(object? sender, FileSystemEventArgs e)
        {
            if (!e.FullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return;

            _ = Task.Run(() => HandleZipRemovedAsync(e.FullPath));
        }

        private async Task HandleZipRemovedAsync(string zipPath)
        {
            try
            {
                _logger.LogInformation("Detected deleted ZIP: {Zip}", Path.GetFileName(zipPath));

                await _removalService.RemoveZipAsync(zipPath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing indexed documents for deleted ZIP {Zip}", zipPath);
            }
        }

        private async Task ProcessZipAsync(string zipPath)
        {
            if (!_processing.TryAdd(zipPath, 0))
                return;

            try
            {
                var file = new FileInfo(zipPath);

                if (!file.Exists)
                    return;

                await WaitUntilReady(file);

                if (!await _catalog.NeedsIndexingAsync(file, CancellationToken.None))
                {
                    _logger.LogDebug("Skipping unchanged ZIP {Zip}", file.Name);

                    return;
                }

                _logger.LogInformation("Detected new/updated ZIP: {Zip}", file.Name);

                await _processor.ProcessAsync(file.FullName, CancellationToken.None);

                await _catalog.UpdateAsync(file, CancellationToken.None);

                _logger.LogInformation("Finished processing {Zip}", file.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ZIP {Zip}", zipPath);
            }
            finally
            {
                _processing.TryRemove(zipPath, out _);
            }
        }

        private async Task WaitUntilReady(FileInfo file)
        {
            const int maxAttempts = 30;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var stream = file.Open(
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.None);

                    return;
                }
                catch (IOException)
                {
                    if (attempt == 1)
                    {
                        _logger.LogInformation("Waiting for ZIP copy to complete: {Zip}", file.Name);
                    }

                    await Task.Delay(1000);
                }
            }

            throw new IOException($"ZIP file '{file.FullName}' never became available.");
        }

        private void OnWatcherError(object? sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), "FileSystemWatcher encountered an error.");
            // Future enhancement:
            // Trigger a full ArchiveScanner scan here if desired.
        }

        public void Stop()
        {
            if (_watcher == null)
                return;

            _watcher.EnableRaisingEvents = false;

            _watcher.Created -= OnZipChanged;
            _watcher.Changed -= OnZipChanged;
            _watcher.Renamed -= OnZipRenamed;
            _watcher.Deleted -= OnZipDeleted;
            _watcher.Error -= OnWatcherError;

            _watcher.Dispose();

            _watcher = null;

            _logger.LogInformation("ArchiveWatcher stopped.");
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
