namespace ArchiveIndexer.Core.Configuration
{
    public sealed class ArchiveSettings
    {
        public string ArchiveRoot { get; set; } = string.Empty;

        public string IndexPath { get; set; } = string.Empty;

        public string CatalogPath { get; set; } = string.Empty;

        public bool EnableWatcher { get; set; }

        public int MaxParallelZipScans { get; set; }

        public int CommitIntervalSeconds { get; set; }

        public string[] SupportedExtensions { get; set; } = Array.Empty<string>();
    }
}
