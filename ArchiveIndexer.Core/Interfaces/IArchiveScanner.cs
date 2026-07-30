namespace ArchiveIndexer.Core.Interfaces
{
    public interface IArchiveScanner
    {
        Task ScanAsync(CancellationToken cancellationToken);
    }
}
