using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace ArchiveIndexer.Infrastructure.Search
{
    public sealed class SearchQueryBuilder
    {
        private readonly IXmlFileNameParser _parser;

        public SearchQueryBuilder(IXmlFileNameParser parser)
        {
            _parser = parser;
        }

        public Query Build(SearchRequest request)
        {
            return request.Mode switch
            {
                SearchMode.Exact => BuildExact(request),
                SearchMode.SetMatch => BuildSetMatch(request),
                SearchMode.PrimeMatch => BuildPrimeMatch(request),
                _ => throw new NotSupportedException()
            };
        }

        private Query BuildExact(SearchRequest request)
        {
            return new TermQuery(new Term("FileName", request.FileName.ToLowerInvariant()));
        }

        private Query BuildSetMatch(SearchRequest request)
        {           
            var metadata = _parser.Parse(request.FileName);

            var from = metadata.StartTime.AddDays(-request.Days);

            var to = metadata.StartTime.AddDays(request.Days);

            var query = new BooleanQuery();

            query.Add(new TermQuery(new Term("SystemName", metadata.SystemName.ToLowerInvariant())), Occur.MUST);

            query.Add(new TermQuery(new Term("StoreCode", metadata.StoreCode.ToLowerInvariant())), Occur.MUST);

            query.Add(new TermQuery(new Term("EnvironmentName", metadata.EnvironmentName.ToLowerInvariant())), Occur.MUST);

            query.Add(new TermQuery(new Term("MessageType", metadata.MessageType.ToLowerInvariant())), Occur.MUST);

            query.Add(NumericRangeQuery.NewInt64Range("StartTicks", from.Ticks, to.Ticks, true, true), Occur.MUST);

            return query;
        }

        private Query BuildPrimeMatch(SearchRequest request)
        {
            var metadata = _parser.Parse(request.FileName);
            var from = metadata.StartTime.AddDays(-request.Days);
            var to = metadata.StartTime.AddDays(request.Days);
            return NumericRangeQuery.NewInt64Range("StartTicks", from.Ticks, to.Ticks, true, true);
        }

    }
}
