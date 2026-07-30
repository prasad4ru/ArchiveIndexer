using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArchiveIndexer.Infrastructure.Scanning
{

    public sealed class ArchiveScanner : IArchiveScanner
    {
        private readonly ArchiveSettings _settings;
        private readonly IZipCatalog _catalog;
        private readonly IArchiveProcessor _processor;
        private readonly IZipRemovalService _removalService;
        private readonly ILogger<ArchiveScanner> _logger;

        public ArchiveScanner(IOptions<ArchiveSettings> options, IZipCatalog catalog, IArchiveProcessor processor, IZipRemovalService removalService, ILogger<ArchiveScanner> logger)
        {
            _settings = options.Value;
            _catalog = catalog;
            _processor = processor;
            _removalService = removalService;
            _logger = logger;
        }

        public async Task ScanAsync(CancellationToken token)
        {
            if (!Directory.Exists(_settings.ArchiveRoot))
            {
                _logger.LogError("Archive root does not exist: {Path}", _settings.ArchiveRoot);
                return;
            }

            var zipFiles = Directory.EnumerateFiles(_settings.ArchiveRoot, "*.zip", SearchOption.AllDirectories).ToList();

            _logger.LogInformation("Found {Count} ZIP files.", zipFiles.Count);

            var zipFilesOnDisk = new HashSet<string>(zipFiles, StringComparer.OrdinalIgnoreCase);

            foreach (var zip in zipFiles)
            {
                token.ThrowIfCancellationRequested();

                var file = new FileInfo(zip);

                if (!await _catalog.NeedsIndexingAsync(file, token))
                {
                    _logger.LogDebug("Skipping unchanged ZIP {Zip}", file.Name);
                    continue;
                }

                _logger.LogInformation("Processing {Zip}", file.Name);

                try
                {
                    await _processor.ProcessAsync(file.FullName, token);

                    await _catalog.UpdateAsync(file, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to process ZIP {Zip} - skipping for this scan.", file.Name);
                }
            }

            await ReconcileDeletedZipsAsync(zipFilesOnDisk, token);

            _logger.LogInformation("Archive scan completed.");
        }

        private async Task ReconcileDeletedZipsAsync(HashSet<string> zipFilesOnDisk, CancellationToken token)
        {

            var catalogedPaths = await _catalog.GetAllZipPathsAsync(token);

            var missingPaths = catalogedPaths.Where(path => !zipFilesOnDisk.Contains(path)).ToList();

            if (missingPaths.Count == 0)
                return;

            _logger.LogInformation("{Count} cataloged ZIP(s) no longer exist on disk - removing their indexed documents.", missingPaths.Count);

            foreach (var path in missingPaths)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await _removalService.RemoveZipAsync(path, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to remove indexed documents for missing ZIP {Zip}.", path);
                }
            }
        }
    }
}