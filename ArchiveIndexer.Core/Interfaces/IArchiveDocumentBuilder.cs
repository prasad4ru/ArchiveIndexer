using ArchiveIndexer.Core.Models;

namespace ArchiveIndexer.Core.Interfaces
{
    public interface IArchiveDocumentBuilder
    {
        ArchiveDocument Build(ZipInfo zipInfo, XmlFileMetadata metadata, string folderPath, string zipPath, string entryPath, long fileSize);
    }
}
