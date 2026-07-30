using ArchiveIndexer.Core.Models;

namespace ArchiveIndexer.Core.Interfaces
{
    public interface ISearchService
    {
        Task<IReadOnlyCollection<SearchResult>> SearchAsync(SearchRequest request, CancellationToken token);
    }
}
