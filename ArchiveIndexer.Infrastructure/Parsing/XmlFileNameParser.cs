using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using System.Globalization;

namespace ArchiveIndexer.Infrastructure.Parsing
{
    public sealed class XmlFileNameParser : IXmlFileNameParser
    {
        public XmlFileMetadata Parse(string xmlName)
        {
            var name = Path.GetFileNameWithoutExtension(xmlName);

            var parts = name.Split('_');

            if (parts.Length != 7)
                throw new FormatException($"Invalid XML filename : {xmlName}");

            return new XmlFileMetadata
            {
                FileName = Path.GetFileName(xmlName),

                SystemName = parts[0],

                StoreCode = parts[1],

                EnvironmentName = parts[2],

                Sequence = int.Parse(parts[3]),

                MessageType = parts[4],

                StartTime = DateTime.ParseExact(parts[5], "yyyyMMddHHmmss", CultureInfo.InvariantCulture),

                EndTime = DateTime.ParseExact(parts[6], "yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            };
        }

        public bool TryParse(string fileName, out XmlFileMetadata metadata)
        {
            metadata = default!;

            try
            {
                metadata = Parse(fileName);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
