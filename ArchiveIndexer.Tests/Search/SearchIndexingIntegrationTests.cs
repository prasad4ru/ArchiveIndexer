using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Indexing;
using ArchiveIndexer.Infrastructure.Parsing;
using ArchiveIndexer.Infrastructure.Search;
using ArchiveIndexer.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace ArchiveIndexer.Tests.Search;

/// <summary>
/// Exercises LuceneIndexer, LuceneSearcher, SearchQueryBuilder, and SearchMapper
/// together against a real, temporary, on-disk Lucene index (FSDirectory - the
/// same type of directory the real Worker/UI use). This is the pipeline most
/// heavily debugged over the course of building this system - field casing,
/// missing fields, commit timing, and cross-zip duplicates all trace back here.
/// </summary>
public class SearchIndexingIntegrationTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private LuceneIndexer CreateIndexer()
    {
        var settings = Options.Create(new ArchiveSettings { IndexPath = _temp.Path });
        return new LuceneIndexer(settings);
    }

    private LuceneSearcher CreateSearcher()
    {
        var settings = Options.Create(new ArchiveSettings { IndexPath = _temp.Path });
        var queryBuilder = new SearchQueryBuilder(new XmlFileNameParser());
        return new LuceneSearcher(queryBuilder, settings);
    }

    private static ArchiveDocument SampleDocument(
        string fileName = "CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML",
        string zipName = "Feb_16_2022_06_12_13.zip",
        string zipPath = @"D:\MarsArchive\Feb_16_2022_06_12_13.zip",
        string systemName = "CD6MARSSTAGE11",
        string storeCode = "A48",
        string environmentName = "PROD",
        string messageType = "CD",
        DateTime? startTime = null,
        DateTime? endTime = null,
        string entryPath = "")
    {
        var start = startTime ?? new DateTime(2026, 6, 3, 13, 26, 38);
        var end = endTime ?? new DateTime(2026, 6, 3, 13, 36, 50);

        return new ArchiveDocument
        {
            DocumentId = Guid.NewGuid().ToString(),
            FolderName = "Q4",
            FolderPath = @"D:\MarsArchive\Q4",
            ZipName = zipName,
            ZipPath = zipPath,
            Year = start.Year,
            Quarter = ((start.Month - 1) / 3) + 1,
            Week = 1,
            FileName = fileName,
            EntryPath = string.IsNullOrEmpty(entryPath) ? fileName : entryPath,
            FileSize = 1024,
            SystemName = systemName,
            StoreCode = storeCode,
            EnvironmentName = environmentName,
            EnvironmentType = "",
            MessageType = messageType,
            Sequence = 21,
            StartTime = start,
            EndTime = end
        };
    }

    [Fact]
    public async Task Exact_FindsDocumentByExactFileName()
    {
        var indexer = CreateIndexer();
        var doc = SampleDocument();
        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = doc.FileName },
            CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(doc.FileName, results.First().FileName);
    }

    [Theory]
    [InlineData("cd6marsstage11_a48_prod_21_cd_20260603132638_20260603133650.xml")] // all lower
    [InlineData("CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML")] // exact original
    [InlineData("Cd6MarsStage11_A48_Prod_21_Cd_20260603132638_20260603133650.Xml")] // mixed
    public async Task Exact_MatchIsCaseInsensitiveRegardlessOfQueryCasing(string queryFileName)
    {
        var indexer = CreateIndexer();
        var doc = SampleDocument(); // indexed with original mixed/upper casing
        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = queryFileName },
            CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task Exact_ResultPreservesOriginalCasingRegardlessOfHowItWasFound()
    {
        var indexer = CreateIndexer();
        var doc = SampleDocument(); // "CD6MARSSTAGE11_..." original casing
        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = "cd6marsstage11_a48_prod_21_cd_20260603132638_20260603133650.xml" },
            CancellationToken.None);

        // Matched case-insensitively, but the stored/displayed value must still be
        // the true original casing - this is what the UI uses to build the
        // extracted output filename.
        Assert.Equal(doc.FileName, results.First().FileName);
    }

    [Fact]
    public async Task Exact_SameFileNameInTwoDifferentZips_ReturnsBothMatches()
    {
        var indexer = CreateIndexer();
        var docInZip1 = SampleDocument(zipName: "Feb_16_2022_06_12_13.zip", zipPath: @"D:\zip1.zip");
        var docInZip2 = SampleDocument(zipName: "Feb_16_2023_06_12_13.zip", zipPath: @"D:\zip2.zip");

        await indexer.IndexAsync(docInZip1, CancellationToken.None);
        await indexer.IndexAsync(docInZip2, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = docInZip1.FileName },
            CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.ZipPath == @"D:\zip1.zip");
        Assert.Contains(results, r => r.ZipPath == @"D:\zip2.zip");
    }

    [Fact]
    public async Task Exact_UnknownFileName_ReturnsNoResults()
    {
        var indexer = CreateIndexer();
        await indexer.IndexAsync(SampleDocument(), CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = "does_not_exist.xml" },
            CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SetMatch_FindsDocumentsWithinDayWindow()
    {
        var indexer = CreateIndexer();
        var seed = SampleDocument(startTime: new DateTime(2026, 6, 3, 13, 26, 38));
        var withinWindow = SampleDocument(
            fileName: "ATDMARSSTAGE11_A43_PROD_0_ATD_20260604090000_20260604091500.XML",
            startTime: new DateTime(2026, 6, 4, 9, 0, 0));
        var outsideWindow = SampleDocument(
            fileName: "ATDMARSSTAGE11_A43_PROD_0_ATD_20260701090000_20260701091500.XML",
            startTime: new DateTime(2026, 7, 1, 9, 0, 0));

        await indexer.IndexAsync(seed, CancellationToken.None);
        await indexer.IndexAsync(withinWindow, CancellationToken.None);
        await indexer.IndexAsync(outsideWindow, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.SetMatch, FileName = seed.FileName, Days = 2 },
            CancellationToken.None);

        var fileNames = results.Select(r => r.FileName).ToList();
        Assert.Contains(seed.FileName, fileNames);
        Assert.Contains(withinWindow.FileName, fileNames);
        Assert.DoesNotContain(outsideWindow.FileName, fileNames);
    }

    [Fact]
    public async Task SetMatch_WindowCorrectlySpansAMonthBoundary()
    {
        // Seed dated June 30; a +/-2 day window should reach into July without
        // any special-casing needed (DateTime.AddDays already handles this).
        var indexer = CreateIndexer();
        var seed = SampleDocument(startTime: new DateTime(2026, 6, 30, 12, 0, 0));
        var earlyJuly = SampleDocument(
            fileName: "ATDMARSSTAGE11_A43_PROD_0_ATD_20260702090000_20260702091500.XML",
            startTime: new DateTime(2026, 7, 2, 9, 0, 0));

        await indexer.IndexAsync(seed, CancellationToken.None);
        await indexer.IndexAsync(earlyJuly, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.SetMatch, FileName = seed.FileName, Days = 2 },
            CancellationToken.None);

        Assert.Contains(results, r => r.FileName == earlyJuly.FileName);
    }

    [Fact]
    public async Task PrimeMatch_RequiresSystemStoreEnvironmentAndMessageTypeToAllMatch()
    {
        var indexer = CreateIndexer();
        var seed = SampleDocument(environmentName: "PROD");
        var differentEnvironment = SampleDocument(
            fileName: "CD6MARSSTAGE11_A48_TEST_22_CD_20260604090000_20260604091500.XML",
            environmentName: "TEST",
            startTime: new DateTime(2026, 6, 4, 9, 0, 0));

        await indexer.IndexAsync(seed, CancellationToken.None);
        await indexer.IndexAsync(differentEnvironment, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.PrimeMatch, FileName = seed.FileName, Days = 2 },
            CancellationToken.None);

        var fileNames = results.Select(r => r.FileName).ToList();
        Assert.Contains(seed.FileName, fileNames);
        Assert.DoesNotContain(differentEnvironment.FileName, fileNames);
    }

    [Fact]
    public async Task PrimeMatch_IsCaseInsensitiveOnAllMatchedFields()
    {
        var indexer = CreateIndexer();
        // Indexed with a mix of upper/lower in the actual file/system name.
        var doc = SampleDocument(
            fileName: "Cd6MarsStage11_A48_Prod_21_Cd_20260603132638_20260603133650.Xml",
            systemName: "Cd6MarsStage11",
            storeCode: "A48",
            environmentName: "Prod",
            messageType: "Cd");

        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();

        // Seed filename typed in a completely different casing than what was indexed.
        const string queryFileName =
            "CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML";

        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.PrimeMatch, FileName = queryFileName, Days = 2 },
            CancellationToken.None);

        Assert.Single(results);
    }

    [Fact]
    public async Task DeleteByZipPathAsync_RemovesOnlyDocumentsFromThatZip()
    {
        var indexer = CreateIndexer();
        var docInZip1 = SampleDocument(
            fileName: "ATDMARSSTAGE11_A43_PROD_0_ATD_20260604090000_20260604091500.XML",
            zipPath: @"D:\zip1.zip");
        var docInZip2 = SampleDocument(
            fileName: "ATDMARSSTAGE11_A43_PROD_1_ATD_20260604100000_20260604101500.XML",
            zipPath: @"D:\zip2.zip");

        await indexer.IndexAsync(docInZip1, CancellationToken.None);
        await indexer.IndexAsync(docInZip2, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        await indexer.DeleteByZipPathAsync(@"D:\zip1.zip", CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();

        var resultsForDeletedZip = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = docInZip1.FileName },
            CancellationToken.None);

        var resultsForRemainingZip = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = docInZip2.FileName },
            CancellationToken.None);

        Assert.Empty(resultsForDeletedZip);
        Assert.Single(resultsForRemainingZip);
    }

    [Fact]
    public async Task DeleteByZipPathAsync_WithoutCommit_DocumentStillFoundUntilCommitted()
    {
        // DeleteByZipPathAsync only touches the writer's buffer - a search through
        // a separate, already-open SearcherManager should still see the old data
        // until CommitAsync is called, exactly like IndexAsync's staging behavior.
        var indexer = CreateIndexer();
        var doc = SampleDocument();
        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var beforeDelete = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = doc.FileName },
            CancellationToken.None);
        Assert.Single(beforeDelete);

        await indexer.DeleteByZipPathAsync(doc.ZipPath, CancellationToken.None);
        // Deliberately not committing yet.

        var afterUncommittedDelete = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = doc.FileName },
            CancellationToken.None);
        Assert.Single(afterUncommittedDelete);

        await indexer.CommitAsync(CancellationToken.None);

        var afterCommittedDelete = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = doc.FileName },
            CancellationToken.None);
        Assert.Empty(afterCommittedDelete);
    }

    [Fact]
    public async Task SearchAsync_BeforeAnyCommitExists_ThrowsButThenSucceedsOnceIndexed()
    {
        // Exercises the lazy/retry-on-failure SearcherManager behavior specifically:
        // the first call against an index with zero commits must fail (not crash
        // the app), and the very same LuceneSearcher instance must succeed on a
        // later call once a commit has actually happened - it must not cache the
        // earlier failure forever.
        var searcher = CreateSearcher();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            searcher.SearchAsync(
                new SearchRequest { Mode = SearchMode.Exact, FileName = "anything.xml" },
                CancellationToken.None));

        var indexer = CreateIndexer();
        var doc = SampleDocument(fileName: "anything.xml", entryPath: "anything.xml");
        // "anything.xml" won't parse via XmlFileNameParser for Exact mode, but Exact
        // mode never needs to parse the filename - it's a raw term lookup - so this
        // is a valid, deliberately simple fixture for this test.
        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var resultsAfterCommit = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = "anything.xml" },
            CancellationToken.None);

        Assert.Single(resultsAfterCommit);
    }

    [Fact]
    public async Task SearchResult_IncludesZipPathFolderNameAndEntryPath()
    {
        // These three fields were the ones missing entirely in the earliest version
        // of LuceneIndexer - regression-guard them explicitly.
        var indexer = CreateIndexer();
        var doc = SampleDocument(entryPath: "nested/path/inside/zip.xml");
        await indexer.IndexAsync(doc, CancellationToken.None);
        await indexer.CommitAsync(CancellationToken.None);

        var searcher = CreateSearcher();
        var results = await searcher.SearchAsync(
            new SearchRequest { Mode = SearchMode.Exact, FileName = doc.FileName },
            CancellationToken.None);

        var result = results.First();
        Assert.Equal(doc.ZipPath, result.ZipPath);
        Assert.Equal(doc.FolderName, result.FolderName);
        Assert.Equal("nested/path/inside/zip.xml", result.EntryPath);
    }

    public void Dispose() => _temp.Dispose();
}
