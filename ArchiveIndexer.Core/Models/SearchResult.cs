namespace ArchiveIndexer.Core.Models
{
    public sealed class SearchResult
    {
        public string FolderName { get; set; } = string.Empty;

        public string ZipName { get; set; } = string.Empty;

        public string ZipPath { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string EntryPath { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public string StoreCode { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = string.Empty;

        public string MessageType { get; set; } = string.Empty;
    }
}
