using System.Text;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Decodes subtitle bytes to text. Ported from vlsub-go.
/// </summary>
public static class SubtitleEncoding
{
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>
    /// Code points for the bytes 0x80-0x9F, where Windows-1252 differs from
    /// ISO-8859-1. Given as numeric casts rather than character literals so the
    /// table cannot be corrupted by a tool re-encoding the source. The five
    /// unassigned slots (0x81, 0x8D, 0x8F, 0x90, 0x9D) keep their byte value.
    /// </summary>
    private static readonly char[] Cp1252High =
    {
        (char)0x20AC, (char)0x0081, (char)0x201A, (char)0x0192, // 0x80..0x83
        (char)0x201E, (char)0x2026, (char)0x2020, (char)0x2021, // 0x84..0x87
        (char)0x02C6, (char)0x2030, (char)0x0160, (char)0x2039, // 0x88..0x8B
        (char)0x0152, (char)0x008D, (char)0x017D, (char)0x008F, // 0x8C..0x8F
        (char)0x0090, (char)0x2018, (char)0x2019, (char)0x201C, // 0x90..0x93
        (char)0x201D, (char)0x2022, (char)0x2013, (char)0x2014, // 0x94..0x97
        (char)0x02DC, (char)0x2122, (char)0x0161, (char)0x203A, // 0x98..0x9B
        (char)0x0153, (char)0x009D, (char)0x017E, (char)0x0178, // 0x9C..0x9F
    };

    /// <summary>
    /// Returns the bytes as text. opensubtitles.org frequently serves
    /// Windows-1252 rather than UTF-8; input that is not valid UTF-8 is
    /// transcoded on that assumption, which is right far more often than leaving
    /// mojibake in place.
    /// </summary>
    /// <param name="converted">Whether a transcode happened.</param>
    public static string ToText(byte[] data, out bool converted)
    {
        converted = false;
        if (data.Length == 0)
        {
            return string.Empty;
        }

        // A throwing decoder is how we detect that the input is not UTF-8.
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return strict.GetString(data).TrimStart(ByteOrderMark);
        }
        catch (DecoderFallbackException)
        {
            // Fall through to Windows-1252.
        }

        converted = true;
        var builder = new StringBuilder(data.Length + (data.Length / 4));
        foreach (var b in data)
        {
            if (b < 0x80)
            {
                builder.Append((char)b);
            }
            else if (b < 0xA0)
            {
                builder.Append(Cp1252High[b - 0x80]);
            }
            else
            {
                builder.Append((char)b);
            }
        }

        return builder.ToString();
    }
}
