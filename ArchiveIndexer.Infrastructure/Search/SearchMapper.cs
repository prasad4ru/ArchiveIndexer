using ArchiveIndexer.Core.Models;
using Lucene.Net.Documents;

namespace ArchiveIndexer.Infrastructure.Search
{
    public static class SearchMapper
    {
        public static SearchResult Map(Document doc)
        {
            return new SearchResult
            {
                FolderName = doc.Get("FolderName"),

                ZipName = doc.Get("ZipName"),

                ZipPath = doc.Get("ZipPath"),

                FileName = doc.Get("FileName"),

                EntryPath = doc.Get("EntryPath"),

                StoreCode = doc.Get("StoreCode"),

                EnvironmentName = doc.Get("EnvironmentName"),

                MessageType = doc.Get("MessageType"),

                StartTime = new DateTime(long.Parse(doc.Get("StartTicks"))),

                EndTime = new DateTime(long.Parse(doc.Get("EndTicks")))
            };
        }
    }
}
