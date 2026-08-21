using Xunit;

namespace Jellyfin.Plugin.VlsubGo.Tests;

/// <summary>
/// Expected values are derived from the specification by hand rather than copied
/// from a reference implementation, so these check the algorithm and not merely
/// that it stayed the same.
/// </summary>
public class OpenSubtitlesHashTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("vlsubgo").FullName;

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void AllZeroBytesHashToTheFileSize()
    {
        // Every ulong is 0, so the sum collapses to the size: 131072 = 0x20000.
        var path = WriteFile("zeros.mkv", new byte[OpenSubtitlesHash.ChunkSize * 2]);

        Assert.True(OpenSubtitlesHash.TryCompute(path, out var hash, out var size));
        Assert.Equal("0000000000020000", hash);
        Assert.Equal(OpenSubtitlesHash.ChunkSize * 2L, size);
    }

    [Fact]
    public void AllOneBitsWrapAround()
    {
        // Each chunk holds 8192 ulongs of 2^64-1, summing to -8192 mod 2^64.
        // Two chunks give -16384, so the total is 131072-16384 = 114688 = 0x1c000.
        var content = new byte[OpenSubtitlesHash.ChunkSize * 2];
        Array.Fill(content, (byte)0xFF);
        var path = WriteFile("ones.mkv", content);

        Assert.True(OpenSubtitlesHash.TryCompute(path, out var hash, out _));
        Assert.Equal("000000000001c000", hash);
    }

    [Fact]
    public void OnlyTheTwoEndChunksAreRead()
    {
        // A dirty middle must not change the result versus an all-zero file of
        // the same length.
        const int length = OpenSubtitlesHash.ChunkSize * 5;
        var content = new byte[length];
        for (var i = OpenSubtitlesHash.ChunkSize + 8; i < length - OpenSubtitlesHash.ChunkSize - 8; i++)
        {
            content[i] = 0xFF;
        }

        var path = WriteFile("dirty-middle.mkv", content);

        Assert.True(OpenSubtitlesHash.TryCompute(path, out var hash, out var size));
        Assert.Equal(length, size);
        Assert.Equal(((ulong)length).ToString("x16"), hash);
    }

    [Fact]
    public void RejectsFilesUnder128KiB()
    {
        var path = WriteFile("tiny.mkv", new byte[(OpenSubtitlesHash.ChunkSize * 2) - 1]);

        Assert.False(OpenSubtitlesHash.TryCompute(path, out var hash, out _));
        Assert.Empty(hash);
    }

    [Fact]
    public void ReturnsFalseForAMissingFile()
    {
        Assert.False(OpenSubtitlesHash.TryCompute(Path.Combine(_dir, "nope.mkv"), out _, out _));
    }

    [Fact]
    public void HashIsAlwaysSixteenHexDigits()
    {
        var content = new byte[OpenSubtitlesHash.ChunkSize * 2];
        content[0] = 0x01;
        var path = WriteFile("small-sum.mkv", content);

        Assert.True(OpenSubtitlesHash.TryCompute(path, out var hash, out _));
        Assert.Equal(16, hash.Length);
        Assert.Equal("0000000000020001", hash);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
