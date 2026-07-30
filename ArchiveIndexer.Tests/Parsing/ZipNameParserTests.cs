using System.Globalization;
using ArchiveIndexer.Infrastructure.Parsing;
using Xunit;

namespace ArchiveIndexer.Tests.Parsing;

public class ZipNameParserTests
{
    private readonly ZipNameParser _parser = new();

    [Fact]
    public void Parse_ValidTimestampFileName_ReturnsCorrectZipInfo()
    {
        var result = _parser.Parse(@"D:\MarsArchive\Feb_16_2022_06_12_13.zip");

        Assert.Equal("Feb_16_2022_06_12_13.zip", result.ZipName);
        Assert.Equal(2022, result.Year);
        Assert.Equal(1, result.Quarter); // February -> Q1
    }

    [Fact]
    public void Parse_ZipNameKeepsExtension()
    {
        var result = _parser.Parse(@"D:\MarsArchive\Feb_16_2022_06_12_13.zip");

        Assert.EndsWith(".zip", result.ZipName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_WeekMatchesIsoWeekOfYear()
    {
        var expectedDate = new DateTime(2022, 2, 16, 6, 12, 13);
        var expectedWeek = ISOWeek.GetWeekOfYear(expectedDate);

        var result = _parser.Parse(@"D:\MarsArchive\Feb_16_2022_06_12_13.zip");

        Assert.Equal(expectedWeek, result.Week);
    }

    [Theory]
    [InlineData("Jan_01_2026_00_00_00.zip", 1)]
    [InlineData("Mar_31_2026_23_59_59.zip", 1)]
    [InlineData("Apr_01_2026_00_00_00.zip", 2)]
    [InlineData("Jun_30_2026_00_00_00.zip", 2)]
    [InlineData("Jul_01_2026_00_00_00.zip", 3)]
    [InlineData("Sep_30_2026_00_00_00.zip", 3)]
    [InlineData("Oct_01_2026_00_00_00.zip", 4)]
    [InlineData("Dec_31_2026_00_00_00.zip", 4)]
    public void Parse_DerivesCorrectCalendarQuarter(string fileName, int expectedQuarter)
    {
        var result = _parser.Parse(fileName);

        Assert.Equal(expectedQuarter, result.Quarter);
    }

    [Theory]
    [InlineData("MARS_ARCHIVE_2026_Q4_WEEK2.zip")] // old convention, no longer supported
    [InlineData("not_a_valid_name.zip")]
    [InlineData("Feb_16_2022.zip")] // missing time components
    [InlineData("Feb_35_2022_06_12_13.zip")] // invalid day
    [InlineData("Xyz_16_2022_06_12_13.zip")] // invalid month abbreviation
    [InlineData("")]
    public void Parse_UnrecognizedFileName_ThrowsFormatException(string fileName)
    {
        Assert.Throws<FormatException>(() => _parser.Parse(fileName));
    }

    [Fact]
    public void Parse_MonthAbbreviationIsCaseInsensitive()
    {
        var upper = _parser.Parse("FEB_16_2022_06_12_13.zip");
        var lower = _parser.Parse("feb_16_2022_06_12_13.zip");

        Assert.Equal(upper.Year, lower.Year);
        Assert.Equal(upper.Quarter, lower.Quarter);
        Assert.Equal(upper.Week, lower.Week);
    }
}
