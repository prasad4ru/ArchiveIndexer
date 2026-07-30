using System.IO.Compression;

namespace ArchiveIndexer.Core.Models
{
    public sealed class ArchiveIndexingContext
    {
        public string FolderPath { get; init; } = string.Empty;
        public string ZipPath { get; init; } = string.Empty;
        public ZipInfo ZipInfo { get; init; } = new();
        public ZipArchive ZipArchive { get; init; } = default!;
    }
}
