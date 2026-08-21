using Xunit;

namespace Jellyfin.Plugin.VlsubGo.Tests;

public class LanguageCodesTests
{
    [Theory]
    [InlineData("en", "eng")]
    [InlineData("EN", "eng")]
    [InlineData(" en ", "eng")]
    [InlineData("es", "spa")]
    [InlineData("ca", "cat")]
    [InlineData("de", "ger")] // 639-2/B, not "deu"
    [InlineData("fr", "fre")] // 639-2/B, not "fra"
    [InlineData("el", "ell")] // OpenSubtitles serves Greek as "ell"
    [InlineData("sr", "scc")] // and Serbian as "scc"
    public void MapsKnownCodes(string two, string expected)
    {
        Assert.True(LanguageCodes.TryGetThreeLetter(two, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("zz")]
    [InlineData("eng")]
    public void RejectsUnknownInput(string input)
    {
        Assert.False(LanguageCodes.TryGetThreeLetter(input, out var actual));
        Assert.Empty(actual);
    }
}
