using ArchiveIndexer.Core.Models;


namespace ArchiveIndexer.Core.Interfaces
{
    public interface IXmlFileNameParser
    {
        bool TryParse(string fileName, out XmlFileMetadata metadata);
        XmlFileMetadata Parse(string fileName);
    }
}
