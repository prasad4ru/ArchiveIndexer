using ArchiveIndexer.Core.Models;
namespace ArchiveIndexer.Core.Interfaces
{
    public interface IArchiveIndexer
    {
        Task IndexAsync(ArchiveDocument documents, CancellationToken cancellationToken);

        Task CommitAsync(CancellationToken cancellationToken);

        Task DeleteByZipPathAsync(string zipPath, CancellationToken cancellationToken);
    }
}
