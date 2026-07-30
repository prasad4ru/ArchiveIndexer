using ArchiveIndexer.Core.Interfaces;
using Microsoft.Extensions.Logging;


namespace ArchiveIndexer.Infrastructure.Services
{
    public sealed class ArchiveProcessor : IArchiveProcessor
    {
        private readonly IZipScanner _scanner;
        private readonly IDocumentQueue _queue;
        private readonly ILogger<ArchiveProcessor> _logger;

        public ArchiveProcessor(IZipScanner scanner, IDocumentQueue queue, ILogger<ArchiveProcessor> logger)
        {
            _scanner = scanner;
            _queue = queue;
            _logger = logger;
        }

        public async Task ProcessAsync(string zipPath, CancellationToken token)
        {
            int count = 0;
            await foreach (var document in _scanner.ScanAsync(zipPath, token))
            {
                _logger.LogInformation("Queueing {File}", document.FileName);
                await _queue.EnqueueAsync(document, token);
                count++;
            }
            _logger.LogInformation("ZIP {Zip} queued {Count} documents", Path.GetFileName(zipPath), count);
        }
    }
}
