namespace ArchiveIndexer.Core.Models
{
    public sealed class ZipInfo
    {
        public string ZipName { get; set; } = string.Empty;

        public int Year { get; set; }

        public int Quarter { get; set; }

        public int Week { get; set; }
    }
}
