using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace ArchiveIndexer.Infrastructure.Scanning;

public sealed class ZipScanner(IZipNameParser zipParser, IXmlFileNameParser xmlParser, IArchiveDocumentBuilder builder, ILogger<ZipScanner> logger) : IZipScanner
{
    private const int RetryCount = 5;
    private const int RetryDelayMilliseconds = 1000;
    private const int ProgressInterval = 10000;

    public async IAsyncEnumerable<ArchiveDocument> ScanAsync(string zipPath, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        FileStream stream;

        try
        {
            stream = await OpenZipWithRetryAsync(zipPath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to open ZIP {Zip}", zipPath);
            yield break;
        }

        using (stream)
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            var zipInfo = zipParser.Parse(zipPath);
            var folder = Path.GetDirectoryName(zipPath)!;

            int indexed = 0;
            int skipped = 0;
            int failed = 0;

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                ArchiveDocument? document = null;

                try
                {
                    var metadata = xmlParser.Parse(entry.Name);

                    document = builder.Build(zipInfo, metadata, folder, zipPath, entry.FullName, entry.Length);

                    indexed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogWarning(ex, "Invalid XML filename '{Entry}'", entry.FullName);
                }

                if (document != null)
                    yield return document;

                if ((indexed + failed) % ProgressInterval == 0)
                {
                    logger.LogInformation("{Zip}: {Count} XML files processed...", Path.GetFileName(zipPath), indexed + failed);
                }
            }

            stopwatch.Stop();

            logger.LogInformation(
     """
    ============================================================
    ZIP Processing Completed

    ZIP Name      : {Zip}
    Total Entries : {Entries}
    Indexed       : {Indexed}
    Failed        : {Failed}
    Skipped       : {Skipped}
    Duration      : {Elapsed}

    ============================================================
    """,
     Path.GetFileName(zipPath), archive.Entries.Count, indexed, failed, skipped, stopwatch.Elapsed);
        }
    }

    private static async Task<FileStream> OpenZipWithRetryAsync(string zipPath, CancellationToken token)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= RetryCount; attempt++)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException ex)
            {
                lastException = ex;

                if (attempt == RetryCount)
                    break;

                await Task.Delay(RetryDelayMilliseconds, token);
            }
        }

        throw new IOException($"Unable to open ZIP file '{zipPath}' after {RetryCount} attempts.", lastException);
    }
}