using System.IO.Compression;
using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Builders;
using ArchiveIndexer.Infrastructure.Parsing;
using ArchiveIndexer.Infrastructure.Scanning;
using ArchiveIndexer.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ArchiveIndexer.Tests.Scanning;

public class ZipScannerTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private readonly ZipScanner _scanner = new(
        new ZipNameParser(),
        new XmlFileNameParser(),
        new ArchiveDocumentBuilder(),
        Mock.Of<ILogger<ZipScanner>>());

    private string CreateZip(string zipFileName, params (string EntryName, string Content)[] entries)
    {
        var path = _temp.GetSubPath(zipFileName);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var (entryName, content) in entries)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return path;
    }

    private static async Task<List<ArchiveDocument>> CollectAsync(IAsyncEnumerable<ArchiveDocument> source)
    {
        var results = new List<ArchiveDocument>();
        await foreach (var item in source)
        {
            results.Add(item);
        }
        return results;
    }

    private const string ValidEntry1 =
        "CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML";

    private const string ValidEntry2 =
        "ATDMARSSTAGE11_A43_PROD_0_ATD_20220216020542_20220216021021.XML";

    [Fact]
    public async Task ScanAsync_ValidZipWithValidXmlEntries_YieldsOneDocumentPerEntry()
    {
        var zipPath = CreateZip("Feb_16_2022_06_12_13.zip",
            (ValidEntry1, "<xml/>"),
            (ValidEntry2, "<xml/>"));

        var results = await CollectAsync(_scanner.ScanAsync(zipPath, CancellationToken.None));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, d => d.FileName == ValidEntry1);
        Assert.Contains(results, d => d.FileName == ValidEntry2);
    }

    [Fact]
    public async Task ScanAsync_ValidEntry_MapsZipAndXmlMetadataCorrectly()
    {
        var zipPath = CreateZip("Feb_16_2022_06_12_13.zip", (ValidEntry1, "<xml/>"));

        var results = await CollectAsync(_scanner.ScanAsync(zipPath, CancellationToken.None));

        var doc = Assert.Single(results);

        Assert.Equal("Feb_16_2022_06_12_13.zip", doc.ZipName);
        Assert.Equal(zipPath, doc.ZipPath);
        Assert.Equal(2022, doc.Year);
        Assert.Equal("CD6MARSSTAGE11", doc.SystemName);
        Assert.Equal("A48", doc.StoreCode);
        Assert.Equal("PROD", doc.EnvironmentName);
        Assert.Equal("CD", doc.MessageType);
        Assert.Equal(ValidEntry1, doc.EntryPath); // flat zip: EntryPath == FullName == entry name
    }

    [Fact]
    public async Task ScanAsync_OneInvalidEntryAmongValidOnes_SkipsOnlyTheInvalidOne()
    {
        var zipPath = CreateZip("Feb_16_2022_06_12_13.zip",
            (ValidEntry1, "<xml/>"),
            ("not_a_valid_name.xml", "<xml/>"),
            (ValidEntry2, "<xml/>"));

        var results = await CollectAsync(_scanner.ScanAsync(zipPath, CancellationToken.None));

        // The bad entry must not silently abort the rest of the zip - both good
        // entries should still come through.
        Assert.Equal(2, results.Count);
        Assert.Contains(results, d => d.FileName == ValidEntry1);
        Assert.Contains(results, d => d.FileName == ValidEntry2);
    }

    [Fact]
    public async Task ScanAsync_NonXmlEntries_AreSkippedWithoutError()
    {
        var zipPath = CreateZip("Feb_16_2022_06_12_13.zip",
            (ValidEntry1, "<xml/>"),
            ("readme.txt", "not xml"));

        var results = await CollectAsync(_scanner.ScanAsync(zipPath, CancellationToken.None));

        Assert.Single(results);
        Assert.Equal(ValidEntry1, results[0].FileName);
    }

    [Fact]
    public async Task ScanAsync_ZipFileNameDoesNotMatchConvention_ThrowsFormatException()
    {
        var zipPath = CreateZip("not_a_valid_zip_name.zip", (ValidEntry1, "<xml/>"));

        await Assert.ThrowsAsync<FormatException>(() =>
            CollectAsync(_scanner.ScanAsync(zipPath, CancellationToken.None)));
    }

    [Fact]
    public async Task ScanAsync_NonExistentZipFile_YieldsNoDocumentsAndDoesNotThrow()
    {
        // OpenZipWithRetryAsync retries a missing file up to 5 times with 1s
        // delays before giving up - this test takes a few seconds by design.
        var missingPath = _temp.GetSubPath("does_not_exist.zip");

        var results = await CollectAsync(_scanner.ScanAsync(missingPath, CancellationToken.None));

        Assert.Empty(results);
    }

    [Fact]
    public async Task ScanAsync_EmptyZip_YieldsNoDocuments()
    {
        var zipPath = CreateZip("Feb_16_2022_06_12_13.zip");

        var results = await CollectAsync(_scanner.ScanAsync(zipPath, CancellationToken.None));

        Assert.Empty(results);
    }

    public void Dispose() => _temp.Dispose();
}
