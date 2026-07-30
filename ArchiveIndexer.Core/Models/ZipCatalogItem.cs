namespace ArchiveIndexer.Core.Models
{
    public sealed class ZipCatalogItem
    {
        public string ZipPath { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }

        public DateTime LastIndexedUtc { get; set; }
    }
}
