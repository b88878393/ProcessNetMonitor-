namespace ProcessNetMonitor.Tests;

public class FilterParsingTests
{
    [Fact]
    public void ParsePortFilter_WithColon_ReturnsPort()
    {
        var filter = ":8080";
        var isPortFilter = filter.StartsWith(":") || filter.StartsWith("port:");
        
        Assert.True(isPortFilter);
        
        var portStr = filter.Replace(":", "").Trim();
        var success = int.TryParse(portStr, out int port);
        
        Assert.True(success);
        Assert.Equal(8080, port);
    }
    
    [Fact]
    public void ParsePortFilter_WithPortPrefix_ReturnsPort()
    {
        var filter = "port:443";
        var isPortFilter = filter.StartsWith(":") || filter.StartsWith("port:");
        
        Assert.True(isPortFilter);
        
        var portStr = filter.Replace("port:", "").Replace(":", "").Trim();
        var success = int.TryParse(portStr, out int port);
        
        Assert.True(success);
        Assert.Equal(443, port);
    }
    
    [Fact]
    public void ParsePidFilter_WithPidPrefix_ReturnsPid()
    {
        var filter = "pid:1234";
        var isPidFilter = filter.StartsWith("pid:");
        
        Assert.True(isPidFilter);
        
        var pidStr = filter.Replace("pid:", "").Trim();
        var success = int.TryParse(pidStr, out int pid);
        
        Assert.True(success);
        Assert.Equal(1234, pid);
    }
    
    [Fact]
    public void FormatBytes_FormatsCorrectly()
    {
        Assert.Equal("0 B", FormatBytes(0));
        Assert.Equal("1023 B", FormatBytes(1023));
        Assert.Equal("1 KB", FormatBytes(1024));
        Assert.Equal("1 MB", FormatBytes(1024 * 1024));
        Assert.Equal("1.5 MB", FormatBytes((long)(1.5 * 1024 * 1024)));
    }
    
    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    [Theory]
    [InlineData("chrome", "chrome.exe")]
    [InlineData("chrome", "Google Chrome")]
    [InlineData("1234", "1234")]
    [InlineData("system", "SYSTEM")]
    public void StringContains_CaseInsensitive(string filter, string text)
    {
        Assert.True(text.ToLower().Contains(filter.ToLower()));
    }
}
