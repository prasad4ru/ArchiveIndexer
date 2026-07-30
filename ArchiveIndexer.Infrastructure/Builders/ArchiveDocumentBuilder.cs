using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace ArchiveIndexer.Infrastructure.Builders;

public sealed class ArchiveDocumentBuilder : IArchiveDocumentBuilder
{
    public ArchiveDocument Build(ZipInfo zip, XmlFileMetadata xml, string folder, string zipPath, string entry, long fileSize)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(xml);

        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);

        var folderName = Path.GetFileName(folder);

        return new ArchiveDocument
        {
            DocumentId = CreateDocumentId(zipPath, entry),

            FolderName = folderName,
            FolderPath = folder,

            ZipName = zip.ZipName,
            ZipPath = zipPath,

            Year = zip.Year,
            Quarter = zip.Quarter,
            Week = zip.Week,

            FileName = xml.FileName,
            EntryPath = entry,
            FileSize = fileSize,

            SystemName = xml.SystemName,
            StoreCode = xml.StoreCode,
            EnvironmentName = xml.EnvironmentName,
            EnvironmentType = xml.EnvironmentType,
            MessageType = xml.MessageType,
            Sequence = xml.Sequence,

            StartTime = xml.StartTime,
            EndTime = xml.EndTime
        };
    }

    private static string CreateDocumentId(string zipPath, string entryPath)
    {
        var input =
            $"{Path.GetFullPath(zipPath).ToUpperInvariant()}|{entryPath.Replace('\\', '/')}";

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }
}