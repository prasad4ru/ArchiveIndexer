namespace ArchiveIndexer.Core.Models
{
    public sealed class SearchRequest
    {
        public string FileName { get; set; } = string.Empty;

        public SearchMode Mode { get; set; }

        public int Days { get; set; } = 2;

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 100;

        public string? StoreCode { get; set; }

        public string? EnvironmentName { get; set; }

        public string? MessageType { get; set; }

        public int? Year { get; set; }

        public int? Quarter { get; set; }
    }
}
