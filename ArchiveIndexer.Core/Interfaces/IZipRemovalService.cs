
namespace ArchiveIndexer.Core.Interfaces
{
    public interface IZipRemovalService
    {
        Task RemoveZipAsync(string zipPath, CancellationToken cancellationToken);
    }
}
