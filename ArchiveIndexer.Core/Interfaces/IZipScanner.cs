using ArchiveIndexer.Core.Models;

namespace ArchiveIndexer.Core.Interfaces
{
    public interface IZipScanner
    {
        //Task<IReadOnlyCollection<ArchiveDocument>> ScanAsync(string zipFile, CancellationToken cancellationToken);
        IAsyncEnumerable<ArchiveDocument> ScanAsync(string zipPath, CancellationToken cancellationToken);
    }
}
