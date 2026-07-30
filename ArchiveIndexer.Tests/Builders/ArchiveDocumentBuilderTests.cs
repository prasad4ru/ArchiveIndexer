using ArchiveIndexer.Core.Models;
using ArchiveIndexer.Infrastructure.Builders;
using Xunit;

namespace ArchiveIndexer.Tests.Builders;

public class ArchiveDocumentBuilderTests
{
    private readonly ArchiveDocumentBuilder _builder = new();

    private static ZipInfo SampleZipInfo() => new()
    {
        ZipName = "Feb_16_2022_06_12_13.zip",
        Year = 2022,
        Quarter = 1,
        Week = 7
    };

    private static XmlFileMetadata SampleMetadata() => new()
    {
        FileName = "CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML",
        SystemName = "CD6MARSSTAGE11",
        StoreCode = "A48",
        EnvironmentName = "PROD",
        Sequence = 21,
        MessageType = "CD",
        StartTime = new DateTime(2026, 6, 3, 13, 26, 38),
        EndTime = new DateTime(2026, 6, 3, 13, 36, 50)
    };

    [Fact]
    public void Build_ValidInputs_MapsAllFieldsCorrectly()
    {
        var doc = _builder.Build(
            SampleZipInfo(),
            SampleMetadata(),
            @"D:\MarsArchive\Q1",
            @"D:\MarsArchive\Q1\Feb_16_2022_06_12_13.zip",
            "CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML",
            4096);

        Assert.Equal("Q1", doc.FolderName);
        Assert.Equal(@"D:\MarsArchive\Q1", doc.FolderPath);
        Assert.Equal("Feb_16_2022_06_12_13.zip", doc.ZipName);
        Assert.Equal(@"D:\MarsArchive\Q1\Feb_16_2022_06_12_13.zip", doc.ZipPath);
        Assert.Equal(2022, doc.Year);
        Assert.Equal(1, doc.Quarter);
        Assert.Equal(7, doc.Week);
        Assert.Equal("CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML", doc.FileName);
        Assert.Equal(4096, doc.FileSize);
        Assert.Equal("CD6MARSSTAGE11", doc.SystemName);
        Assert.Equal("A48", doc.StoreCode);
        Assert.Equal("PROD", doc.EnvironmentName);
        Assert.Equal("CD", doc.MessageType);
        Assert.Equal(21, doc.Sequence);
        Assert.Equal(new DateTime(2026, 6, 3, 13, 26, 38), doc.StartTime);
        Assert.Equal(new DateTime(2026, 6, 3, 13, 36, 50), doc.EndTime);
        Assert.False(string.IsNullOrWhiteSpace(doc.DocumentId));
    }

    [Fact]
    public void Build_NullZip_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _builder.Build(null!, SampleMetadata(), "folder", "zip.zip", "entry.xml", 1));
    }

    [Fact]
    public void Build_NullXmlMetadata_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _builder.Build(SampleZipInfo(), null!, "folder", "zip.zip", "entry.xml", 1));
    }

    [Theory]
    [InlineData("", "zip.zip", "entry.xml")]
    [InlineData("folder", "", "entry.xml")]
    [InlineData("folder", "zip.zip", "")]
    public void Build_EmptyRequiredStringArgument_ThrowsArgumentException(string folder, string zipPath, string entry)
    {
        Assert.Throws<ArgumentException>(() =>
            _builder.Build(SampleZipInfo(), SampleMetadata(), folder, zipPath, entry, 1));
    }

    [Fact]
    public void Build_SameInputs_ProducesSameDocumentId()
    {
        var doc1 = _builder.Build(SampleZipInfo(), SampleMetadata(), "folder", @"D:\zip.zip", "entry.xml", 1);
        var doc2 = _builder.Build(SampleZipInfo(), SampleMetadata(), "folder", @"D:\zip.zip", "entry.xml", 1);

        Assert.Equal(doc1.DocumentId, doc2.DocumentId);
    }

    [Fact]
    public void Build_DifferentEntryPath_ProducesDifferentDocumentId()
    {
        var doc1 = _builder.Build(SampleZipInfo(), SampleMetadata(), "folder", @"D:\zip.zip", "entryA.xml", 1);
        var doc2 = _builder.Build(SampleZipInfo(), SampleMetadata(), "folder", @"D:\zip.zip", "entryB.xml", 1);

        Assert.NotEqual(doc1.DocumentId, doc2.DocumentId);
    }

    [Fact]
    public void Build_ZipPathCasingDoesNotAffectDocumentId()
    {
        // CreateDocumentId upper-invariants the zip path before hashing.
        var doc1 = _builder.Build(SampleZipInfo(), SampleMetadata(), "folder", @"D:\marsarchive\zip.zip", "entry.xml", 1);
        var doc2 = _builder.Build(SampleZipInfo(), SampleMetadata(), "folder", @"D:\MARSARCHIVE\zip.zip", "entry.xml", 1);

        Assert.Equal(doc1.DocumentId, doc2.DocumentId);
    }

    [Fact]
    public void Build_FolderNameIsLastSegmentOfFolderPath()
    {
        var doc = _builder.Build(SampleZipInfo(), SampleMetadata(), @"D:\MarsArchive\Q4\Week2", @"D:\zip.zip", "entry.xml", 1);

        Assert.Equal("Week2", doc.FolderName);
    }
}
