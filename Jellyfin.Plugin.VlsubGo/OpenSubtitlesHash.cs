using System.Buffers.Binary;
using System.Globalization;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Result of hashing a media file. <see cref="IsValid"/> is false when the file
/// was too small or could not be read.
/// </summary>
public readonly record struct MovieHash(string Value, long Size)
{
    public static readonly MovieHash None = new(string.Empty, 0);

    public bool IsValid => !string.IsNullOrEmpty(Value);
}

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
    /// Caps how many files are hashed at once. Media usually sits on a network
    /// mount, and a library-wide search would otherwise fan out into hundreds of
    /// concurrent reads.
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(2, 2);

    /// <summary>
    /// Computes the hash of a file: the 64-bit wrapping sum of the file size and
    /// every little-endian ulong in the first and last 64 KiB. It identifies a
    /// specific release rather than a title, which is why a match is already in
    /// sync.
    /// <para>
    /// Asynchronous throughout, deliberately. This runs inside a request handler
    /// and the file is typically on a network mount, where a synchronous read
    /// holds the calling thread for as long as the storage takes to answer.
    /// </para>
    /// </summary>
    /// <returns><see cref="MovieHash.None"/> when the file is missing or under
    /// 128 KiB, the point at which the two chunks would overlap.</returns>
    public static async Task<MovieHash> ComputeAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
        {
            return MovieHash.None;
        }

        var info = new FileInfo(path);
        if (!info.Exists || info.Length < ChunkSize * 2L)
        {
            return MovieHash.None;
        }

        var size = info.Length;

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = ChunkSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            };

            await using var stream = new FileStream(path, options);

            var sum = unchecked((ulong)size);
            var buffer = new byte[ChunkSize];

            foreach (var offset in new[] { 0L, size - ChunkSize })
            {
                stream.Seek(offset, SeekOrigin.Begin);
                await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);

                for (var i = 0; i < ChunkSize; i += 8)
                {
                    sum = unchecked(sum + BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(i, 8)));
                }
            }

            return new MovieHash(sum.ToString("x16", CultureInfo.InvariantCulture), size);
        }
        finally
        {
            Gate.Release();
        }
    }
}
