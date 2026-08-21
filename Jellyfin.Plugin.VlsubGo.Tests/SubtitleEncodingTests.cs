using System.Text;
using Xunit;

namespace Jellyfin.Plugin.VlsubGo.Tests;

public class SubtitleEncodingTests
{
    [Fact]
    public void ValidUtf8PassesThrough()
    {
        const string text = "Ja, aixo es catala - de veritat?";
        var actual = SubtitleEncoding.ToText(Encoding.UTF8.GetBytes(text), out var converted);

        Assert.False(converted);
        Assert.Equal(text, actual);
    }

    [Fact]
    public void StripsAByteOrderMark()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("hello")).ToArray();

        var actual = SubtitleEncoding.ToText(bytes, out _);

        Assert.Equal("hello", actual);
    }

    [Fact]
    public void TranscodesTheLatin1Range()
    {
        // 0xE8 is invalid on its own in UTF-8; Windows-1252 reads it as "e-grave".
        var actual = SubtitleEncoding.ToText(new byte[] { (byte)'c', (byte)'a', (byte)'f', 0xE8 }, out var converted);

        Assert.True(converted);
        Assert.Equal("caf" + (char)0x00E8, actual);
    }

    [Fact]
    public void TranscodesTheCp1252HighRange()
    {
        // 0x92 is a right single quote in Windows-1252 but unassigned in ISO-8859-1.
        var actual = SubtitleEncoding.ToText(new byte[] { (byte)'i', (byte)'t', 0x92, (byte)'s' }, out var converted);

        Assert.True(converted);
        Assert.Equal("it" + (char)0x2019 + "s", actual);
    }

    [Fact]
    public void MapsTheEuroSign()
    {
        var actual = SubtitleEncoding.ToText(new byte[] { 0x80, 0x35 }, out var converted);

        Assert.True(converted);
        Assert.Equal((char)0x20AC + "5", actual);
    }

    [Fact]
    public void HandlesEmptyInput()
    {
        Assert.Equal(string.Empty, SubtitleEncoding.ToText(Array.Empty<byte>(), out var converted));
        Assert.False(converted);
    }
}
