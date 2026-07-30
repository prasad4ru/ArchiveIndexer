namespace ArchiveIndexer.Core.Interfaces
{
    public interface IArchiveProcessor
    {
        Task ProcessAsync(string zipPath, CancellationToken cancellationToken);
    }
}
