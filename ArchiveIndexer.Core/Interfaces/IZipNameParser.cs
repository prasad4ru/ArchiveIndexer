using ArchiveIndexer.Core.Models;

namespace ArchiveIndexer.Core.Interfaces
{
    public interface IZipNameParser
    {
        ZipInfo Parse(string zipName);
    }
}
