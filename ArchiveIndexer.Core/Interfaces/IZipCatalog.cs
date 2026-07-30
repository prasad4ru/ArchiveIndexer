namespace ArchiveIndexer.Core.Interfaces
{
    public interface IZipCatalog
    {
        Task<bool> NeedsIndexingAsync(FileInfo zipFile, CancellationToken cancellationToken);

        Task UpdateAsync(FileInfo zipFile, CancellationToken cancellationToken);

        Task RemoveAsync(string zipPath, CancellationToken cancellationToken);

        Task<IReadOnlyCollection<string>> GetAllZipPathsAsync(CancellationToken cancellationToken);
    }
}
