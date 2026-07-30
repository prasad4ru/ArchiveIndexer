using ArchiveIndexer.Core.Interfaces;
using Microsoft.Extensions.Logging;


namespace ArchiveIndexer.Infrastructure.Services
{
    public sealed class ZipRemovalService : IZipRemovalService
    {
        private readonly IArchiveIndexer _indexer;
        private readonly IZipCatalog _catalog;
        private readonly ILogger<ZipRemovalService> _logger;

        public ZipRemovalService(IArchiveIndexer indexer, IZipCatalog catalog, ILogger<ZipRemovalService> logger)
        {
            _indexer = indexer;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task RemoveZipAsync(string zipPath, CancellationToken cancellationToken)
        {
            await _indexer.DeleteByZipPathAsync(zipPath, cancellationToken);

            await _indexer.CommitAsync(cancellationToken);

            await _catalog.RemoveAsync(zipPath, cancellationToken);

            _logger.LogInformation("Removed indexed documents and catalog entry for deleted ZIP: {Zip}", Path.GetFileName(zipPath));
        }
    }
}
