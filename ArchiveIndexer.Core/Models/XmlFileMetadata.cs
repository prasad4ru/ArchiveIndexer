namespace ArchiveIndexer.Core.Models
{
    public sealed class XmlFileMetadata
    {
        public string FileName { get; set; } = string.Empty;

        public string SystemName { get; set; } = string.Empty;

        public string StoreCode { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = string.Empty;

        public int Sequence { get; set; }

        public string MessageType { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public string EnvironmentType { get; set; } = string.Empty;
    }
}
