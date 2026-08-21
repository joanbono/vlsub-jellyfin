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
    public async Task AllZeroBytesHashToTheFileSize()
    {
        // Every ulong is 0, so the sum collapses to the size: 131072 = 0x20000.
        var path = WriteFile("zeros.mkv", new byte[OpenSubtitlesHash.ChunkSize * 2]);

        var result = await OpenSubtitlesHash.ComputeAsync(path, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("0000000000020000", result.Value);
        Assert.Equal(OpenSubtitlesHash.ChunkSize * 2L, result.Size);
    }

    [Fact]
    public async Task AllOneBitsWrapAround()
    {
        // Each chunk holds 8192 ulongs of 2^64-1, summing to -8192 mod 2^64.
        // Two chunks give -16384, so the total is 131072-16384 = 114688 = 0x1c000.
        var content = new byte[OpenSubtitlesHash.ChunkSize * 2];
        Array.Fill(content, (byte)0xFF);
        var path = WriteFile("ones.mkv", content);

        var result = await OpenSubtitlesHash.ComputeAsync(path, CancellationToken.None);

        Assert.Equal("000000000001c000", result.Value);
    }

    [Fact]
    public async Task OnlyTheTwoEndChunksAreRead()
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

        var result = await OpenSubtitlesHash.ComputeAsync(path, CancellationToken.None);

        Assert.Equal(length, result.Size);
        Assert.Equal(((ulong)length).ToString("x16"), result.Value);
    }

    [Fact]
    public async Task RejectsFilesUnder128KiB()
    {
        var path = WriteFile("tiny.mkv", new byte[(OpenSubtitlesHash.ChunkSize * 2) - 1]);

        var result = await OpenSubtitlesHash.ComputeAsync(path, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ReturnsNoneForAMissingFile()
    {
        var result = await OpenSubtitlesHash.ComputeAsync(
            Path.Combine(_dir, "nope.mkv"), CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ReturnsNoneForAnEmptyPath()
    {
        var result = await OpenSubtitlesHash.ComputeAsync(string.Empty, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task HashIsAlwaysSixteenHexDigits()
    {
        var content = new byte[OpenSubtitlesHash.ChunkSize * 2];
        content[0] = 0x01;
        var path = WriteFile("small-sum.mkv", content);

        var result = await OpenSubtitlesHash.ComputeAsync(path, CancellationToken.None);

        Assert.Equal(16, result.Value.Length);
        Assert.Equal("0000000000020001", result.Value);
    }

    [Fact]
    public async Task ConcurrentHashingStaysCorrectUnderTheGate()
    {
        // The concurrency cap must not corrupt results under parallel load.
        var paths = Enumerable.Range(0, 8).Select(i =>
        {
            var content = new byte[OpenSubtitlesHash.ChunkSize * 2];
            content[0] = (byte)i;
            return WriteFile($"parallel-{i}.mkv", content);
        }).ToList();

        var results = await Task.WhenAll(
            paths.Select(p => OpenSubtitlesHash.ComputeAsync(p, CancellationToken.None)));

        for (var i = 0; i < paths.Count; i++)
        {
            Assert.Equal($"00000000000200{i:x2}", results[i].Value);
        }
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);
}
