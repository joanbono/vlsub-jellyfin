using System.Buffers.Binary;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// The OpenSubtitles "moviehash", ported from vlsub-go.
/// </summary>
public static class OpenSubtitlesHash
{
    /// <summary>
    /// Chunk length fixed by the OpenSubtitles hash specification.
    /// </summary>
    public const int ChunkSize = 65536;

    /// <summary>
    /// Computes the hash of a file: the 64-bit wrapping sum of the file size and
    /// every little-endian ulong in the first and last 64 KiB. It identifies a
    /// specific release rather than a title, which is why a match is already in
    /// sync.
    /// </summary>
    /// <returns><c>false</c> if the file is under 128 KiB, where the two chunks
    /// would overlap.</returns>
    public static bool TryCompute(string path, out string hash, out long size)
    {
        hash = string.Empty;
        size = 0;

        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return false;
        }

        size = info.Length;
        if (size < ChunkSize * 2L)
        {
            return false;
        }

        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, FileOptions.SequentialScan);

        var sum = unchecked((ulong)size);
        var buffer = new byte[ChunkSize];

        foreach (var offset in new[] { 0L, size - ChunkSize })
        {
            stream.Seek(offset, SeekOrigin.Begin);
            stream.ReadExactly(buffer, 0, ChunkSize);

            for (var i = 0; i < ChunkSize; i += 8)
            {
                sum = unchecked(sum + BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(i, 8)));
            }
        }

        hash = sum.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        return true;
    }
}
