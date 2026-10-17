using ProcessNetMonitor.Core;

namespace ProcessNetMonitor.Tests;

public class FilterParsingTests
{
    [Theory]
    [InlineData(":8080", 8080)]
    [InlineData("port:443", 443)]
    [InlineData("PORT:80", 80)]
    public void Parse_PortFilters(string input, int expectedPort)
    {
        var parsed = ProcessFilterParser.Parse(input);
        Assert.Equal(ProcessFilterParser.FilterKind.Port, parsed.Kind);
        Assert.Equal(expectedPort, parsed.Port);
    }

    [Fact]
    public void Parse_PidFilter()
    {
        var parsed = ProcessFilterParser.Parse("pid:1234");
        Assert.Equal(ProcessFilterParser.FilterKind.Pid, parsed.Kind);
        Assert.Equal(1234, parsed.Pid);
    }

    [Fact]
    public void Parse_TextFilter()
    {
        var parsed = ProcessFilterParser.Parse("chrome");
        Assert.Equal(ProcessFilterParser.FilterKind.Text, parsed.Kind);
        Assert.Equal("chrome", parsed.Text);
    }

    [Fact]
    public void Matches_Port()
    {
        var filter = ProcessFilterParser.Parse(":8080");
        Assert.True(ProcessFilterParser.Matches(filter, "app", 1, "", "", new[] { 8080, 443 }));
        Assert.False(ProcessFilterParser.Matches(filter, "app", 1, "", "", new[] { 80 }));
    }

    [Fact]
    public void Matches_Pid()
    {
        var filter = ProcessFilterParser.Parse("pid:42");
        Assert.True(ProcessFilterParser.Matches(filter, "x", 42, "", "", Array.Empty<int>()));
        Assert.False(ProcessFilterParser.Matches(filter, "x", 7, "", "", Array.Empty<int>()));
    }

    [Fact]
    public void Matches_PathAndCommandLine()
    {
        var filter = ProcessFilterParser.Parse("widgets");
        Assert.True(ProcessFilterParser.Matches(filter, "svc", 1, @"C:\tools\widgets.exe", "", Array.Empty<int>()));
        Assert.True(ProcessFilterParser.Matches(filter, "svc", 1, "", "--name widgets", Array.Empty<int>()));
        Assert.False(ProcessFilterParser.Matches(filter, "svc", 1, @"C:\other.exe", "--flag", Array.Empty<int>()));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    public void FormatBytes_FormatsCorrectly(long bytes, string expected)
    {
        Assert.Equal(expected, ByteFormatter.FormatBytes(bytes));
    }

    [Fact]
    public void FormatRate_AppendsPerSecond()
    {
        Assert.Equal("1 KB/s", ByteFormatter.FormatRate(1024));
    }
}
