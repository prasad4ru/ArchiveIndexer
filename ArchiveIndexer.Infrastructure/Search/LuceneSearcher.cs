using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.Extensions.Options;

namespace ArchiveIndexer.Infrastructure.Search
{

    public sealed class LuceneSearcher : ISearchService, IDisposable
    {
        private readonly SearchQueryBuilder _queryBuilder;
        private readonly ArchiveSettings _settings;
        private readonly object _initLock = new();        
        private SearcherManager? _searcherManager;

        public LuceneSearcher(SearchQueryBuilder queryBuilder, IOptions<ArchiveSettings> options)
        {
            _queryBuilder = queryBuilder;
            _settings = options.Value;
        }

        public Task<IReadOnlyCollection<SearchResult>> SearchAsync(SearchRequest request, CancellationToken token)
        {
            var results = new List<SearchResult>();

            var manager = GetOrCreateSearcherManager();          
            manager.MaybeRefresh();

            var searcher = manager.Acquire();

            try
            {
                Query query = _queryBuilder.Build(request);

                TopDocs topDocs = searcher.Search(query, request.PageSize);

                foreach (var hit in topDocs.ScoreDocs)
                {
                    token.ThrowIfCancellationRequested();

                    var document = searcher.Doc(hit.Doc);

                    results.Add(SearchMapper.Map(document));
                }
            }
            finally
            {
                manager.Release(searcher);
            }

            return Task.FromResult<IReadOnlyCollection<SearchResult>>(results);
        }

        private SearcherManager GetOrCreateSearcherManager()
        {            
            if (_searcherManager != null)
                return _searcherManager;

            lock (_initLock)
            {
                if (_searcherManager != null)
                    return _searcherManager;

                var directory = FSDirectory.Open(_settings.IndexPath);

                _searcherManager = new SearcherManager(directory, null);

                return _searcherManager;
            }
        }

        public void Dispose()
        {
            _searcherManager?.Dispose();
        }
    } 
}

