using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;


namespace ArchiveIndexer.Infrastructure.Indexing
{
    public sealed class LuceneIndexer : IArchiveIndexer
    {
        private readonly IndexWriter _writer;
        public LuceneIndexer(IOptions<ArchiveSettings> options)
        {
            var settings = options.Value;

            var directory = FSDirectory.Open(settings.IndexPath);

            var analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);

            var config = new IndexWriterConfig(
                LuceneVersion.LUCENE_48,
                analyzer);

            _writer = new IndexWriter(directory, config);
        }

        public Task IndexAsync(ArchiveDocument document, CancellationToken cancellationToken)
        {
            var luceneDocument = new Document {

            new StringField("DocumentId", document.DocumentId, Field.Store.YES),
                       
            new StringField("FolderName", document.FolderName, Field.Store.YES),

            new StringField("EntryPath", document.EntryPath, Field.Store.YES),
          
            new StringField("FileName", document.FileName.ToLowerInvariant(), Field.Store.NO),
            new StoredField("FileName", document.FileName),

            new StringField("SystemName", document.SystemName.ToLowerInvariant(), Field.Store.NO),
            new StoredField("SystemName", document.SystemName),

            new StringField("StoreCode", document.StoreCode.ToLowerInvariant(), Field.Store.NO),
            new StoredField("StoreCode", document.StoreCode),

            new StringField("EnvironmentName", document.EnvironmentName.ToLowerInvariant(), Field.Store.NO),
            new StoredField("EnvironmentName", document.EnvironmentName),

            new StringField("MessageType", document.MessageType.ToLowerInvariant(), Field.Store.NO),
            new StoredField("MessageType", document.MessageType),

            new StringField("FolderPath", document.FolderPath, Field.Store.YES),

            new StringField("ZipPath", document.ZipPath, Field.Store.YES),

            new StringField("ZipName", document.ZipName, Field.Store.YES),

            new StringField("EnvironmentType", document.EnvironmentType, Field.Store.YES),

            new Int32Field("Year", document.Year, Field.Store.YES),

            new Int32Field("Quarter", document.Quarter, Field.Store.YES),

            new Int32Field("Week", document.Week, Field.Store.YES),

            new Int32Field("Sequence", document.Sequence, Field.Store.YES),

            new Int64Field("FileSize", document.FileSize, Field.Store.YES),

            new Int64Field("StartTicks",document.StartTime.Ticks,Field.Store.YES),

            new Int64Field("EndTicks",document.EndTime.Ticks,Field.Store.YES)
        };
          
            _writer.AddDocument(luceneDocument);

            return Task.CompletedTask;
        }
       
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            _writer.Commit();
            return Task.CompletedTask;
        }

        public Task DeleteByZipPathAsync(string zipPath, CancellationToken cancellationToken)
        {
            _writer.DeleteDocuments(new Term("ZipPath", zipPath));

            return Task.CompletedTask;
        }

    }
}
