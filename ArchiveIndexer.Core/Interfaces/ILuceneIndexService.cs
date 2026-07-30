namespace ArchiveIndexer.Core.Interfaces
{
    public interface ILuceneIndexService
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}
