using ArchiveIndexer.Infrastructure.Parsing;
using Xunit;

namespace ArchiveIndexer.Tests.Parsing;

public class XmlFileNameParserTests
{
    private readonly XmlFileNameParser _parser = new();

    private const string ValidFileName = "CD6MARSSTAGE11_A48_PROD_21_CD_20260603132638_20260603133650.XML";

    [Fact]
    public void Parse_ValidFileName_ReturnsCorrectMetadata()
    {
        var result = _parser.Parse(ValidFileName);

        Assert.Equal(ValidFileName, result.FileName);
        Assert.Equal("CD6MARSSTAGE11", result.SystemName);
        Assert.Equal("A48", result.StoreCode);
        Assert.Equal("PROD", result.EnvironmentName);
        Assert.Equal(21, result.Sequence);
        Assert.Equal("CD", result.MessageType);
        Assert.Equal(new DateTime(2026, 6, 3, 13, 26, 38), result.StartTime);
        Assert.Equal(new DateTime(2026, 6, 3, 13, 36, 50), result.EndTime);
    }

    [Theory]
    [InlineData("TooFewParts_A48_PROD.xml")]
    [InlineData("Way_Too_Many_Underscore_Separated_Parts_Here_ForThis_Test.xml")]
    [InlineData("")]
    public void Parse_WrongPartCount_ThrowsFormatException(string fileName)
    {
        Assert.Throws<FormatException>(() => _parser.Parse(fileName));
    }

    [Fact]
    public void Parse_NonNumericSequence_ThrowsFormatException()
    {
        const string badSequence = "CD6MARSSTAGE11_A48_PROD_NOTANUMBER_CD_20260603132638_20260603133650.XML";

        Assert.ThrowsAny<FormatException>(() => _parser.Parse(badSequence));
    }

    [Fact]
    public void Parse_InvalidDateSegment_ThrowsFormatException()
    {
        const string badDate = "CD6MARSSTAGE11_A48_PROD_21_CD_NOTADATE_20260603133650.XML";

        Assert.Throws<FormatException>(() => _parser.Parse(badDate));
    }

    [Fact]
    public void TryParse_ValidFileName_ReturnsTrueWithMetadata()
    {
        var success = _parser.TryParse(ValidFileName, out var metadata);

        Assert.True(success);
        Assert.Equal("CD6MARSSTAGE11", metadata.SystemName);
    }

    [Fact]
    public void TryParse_InvalidFileName_ReturnsFalse()
    {
        var success = _parser.TryParse("not_a_valid_name.xml", out _);

        Assert.False(success);
    }

    [Fact]
    public void Parse_EnvironmentTypeIsNeverPopulatedByThisParser()
    {
        // Known gap: XmlFileMetadata.EnvironmentType exists and is carried through
        // ArchiveDocumentBuilder into the index, but nothing in the filename format
        // supplies a value for it, so it's always empty today. This test documents
        // the current, observed behavior rather than asserting what it "should" be -
        // flag to the team if EnvironmentType needs a real source.
        var result = _parser.Parse(ValidFileName);

        Assert.Equal(string.Empty, result.EnvironmentType);
    }
}
