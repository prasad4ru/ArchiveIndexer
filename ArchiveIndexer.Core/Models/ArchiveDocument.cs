namespace ArchiveIndexer.Core.Models
{
    public sealed class ArchiveDocument
    {
        public string DocumentId { get; set; } = string.Empty;

        public string FolderName { get; set; } = string.Empty;
        public string FolderPath { get; set; } = string.Empty;

        public string ZipName { get; set; } = string.Empty;
        public string ZipPath { get; set; } = string.Empty;

        public int Year { get; set; }
        public int Quarter { get; set; }
        public int Week { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string EntryPath { get; set; } = string.Empty;
        public long FileSize { get; set; }

        public string SystemName { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string EnvironmentType { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;

        public int Sequence { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}