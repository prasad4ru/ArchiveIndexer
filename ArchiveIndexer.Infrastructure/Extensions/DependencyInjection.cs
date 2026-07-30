using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Infrastructure.Builders;
using ArchiveIndexer.Infrastructure.Catalog;
using ArchiveIndexer.Infrastructure.Indexing;
using ArchiveIndexer.Infrastructure.Parsing;
using ArchiveIndexer.Infrastructure.Queue;
using ArchiveIndexer.Infrastructure.Scanning;
using ArchiveIndexer.Infrastructure.Search;
using ArchiveIndexer.Infrastructure.Services;
using ArchiveIndexer.Infrastructure.Watching;
using Microsoft.Extensions.DependencyInjection;

namespace ArchiveIndexer.Infrastructure.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddArchiveIndexer(this IServiceCollection services)
        {
            // Queue
            services.AddSingleton<IDocumentQueue, DocumentQueue>();

            // Lucene
            services.AddSingleton<IArchiveIndexer, LuceneIndexer>();

            // Builders
            services.AddSingleton<IArchiveDocumentBuilder, ArchiveDocumentBuilder>();

            // Parsers
            services.AddSingleton<IZipNameParser, ZipNameParser>();
            services.AddSingleton<IXmlFileNameParser, XmlFileNameParser>();
            services.AddSingleton<IZipCatalog, ZipCatalog>();
            services.AddSingleton<IZipRemovalService, ZipRemovalService>();

            // Scanner
            services.AddSingleton<IArchiveScanner, ArchiveScanner>();
            services.AddSingleton<IZipScanner, ZipScanner>();

            // Processor
            services.AddSingleton<IArchiveProcessor, ArchiveProcessor>();

            // Watcher
            services.AddSingleton<IArchiveWatcher, ArchiveWatcher>();

            // Search
            services.AddSingleton<SearchQueryBuilder>();
            services.AddSingleton<ISearchService, LuceneSearcher>();

            // Hosted Services
            services.AddSingleton<LuceneIndexService>();
            services.AddSingleton<ILuceneIndexService>(sp => sp.GetRequiredService<LuceneIndexService>());
            services.AddHostedService(sp => sp.GetRequiredService<LuceneIndexService>());

            services.AddHostedService<ArchiveScannerService>();

            return services;
        }
    }
}