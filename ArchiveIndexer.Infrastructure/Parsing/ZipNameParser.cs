using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using System.Globalization;


namespace ArchiveIndexer.Infrastructure.Parsing
{

    public sealed class ZipNameParser : IZipNameParser
    {

        private const string TimestampFormat = "MMM_dd_yyyy_HH_mm_ss";

        public ZipInfo Parse(string zipPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(zipPath);

            if (!DateTime.TryParseExact(
                    fileName,
                    TimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var timestamp))
            {
                throw new FormatException(
                    $"Invalid archive filename: {fileName}");
            }
            return new ZipInfo
            {
                ZipName = Path.GetFileName(zipPath),
                Year = timestamp.Year,
                Quarter = ((timestamp.Month - 1) / 3) + 1,
                Week = ISOWeek.GetWeekOfYear(timestamp)
            };
        }
        
    }
   
}
