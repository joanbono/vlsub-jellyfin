using Xunit;

namespace Jellyfin.Plugin.VlsubGo.Tests;

public class SubtitleIdTests
{
    [Theory]
    [InlineData("https://dl.opensubtitles.org/en/download/src-api/vrf-19c40c57/sid-ABC/file/1951980526.gz", "srt", "eng")]
    [InlineData("https://example.test/a+b/c?d=e&f=g", "ass", "pob")]
    [InlineData("https://example.test/short", "sub", "spa")]
    public void RoundTrips(string link, string format, string language)
    {
        var id = SubtitleId.Encode(link, format, language);

        Assert.True(SubtitleId.TryDecode(id, out var gotLink, out var gotFormat, out var gotLanguage));
        Assert.Equal(link, gotLink);
        Assert.Equal(format, gotFormat);
        Assert.Equal(language, gotLanguage);
    }

    [Fact]
    public void EncodedIdIsUrlSafe()
    {
        // The id travels in a URL path, and the .org links contain slashes.
        var id = SubtitleId.Encode("https://dl.opensubtitles.org/a/b/c?x=1", "srt", "eng");

        Assert.DoesNotContain('+', id);
        Assert.DoesNotContain('/', id);
        Assert.DoesNotContain('=', id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("srt")]
    [InlineData("srt:eng")]
    [InlineData("srt:eng:!!!not-base64!!!")]
    public void RejectsMalformedIds(string id)
    {
        Assert.False(SubtitleId.TryDecode(id, out _, out _, out _));
    }
}
