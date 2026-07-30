using ArchiveIndexer.Core.Models;

namespace ArchiveIndexer.Core.Interfaces
{
    public interface IDocumentQueue
    {
        ValueTask EnqueueAsync(ArchiveDocument document, CancellationToken token);

        IAsyncEnumerable<ArchiveDocument> ReadAllAsync(CancellationToken token);
    }
}
