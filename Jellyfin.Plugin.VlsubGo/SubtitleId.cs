using System.Text;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Packs everything needed to fetch a subtitle into the single opaque string
/// Jellyfin hands back to <c>GetSubtitles</c>.
/// </summary>
internal static class SubtitleId
{
    private const char Separator = ':';

    /// <summary>
    /// Encodes the download link, format and language. The link is base64url
    /// encoded because the id travels in a URL path and the .org links contain
    /// slashes.
    /// </summary>
    public static string Encode(string link, string format, string language) =>
        string.Join(Separator, format, language, ToBase64Url(link));

    public static bool TryDecode(string id, out string link, out string format, out string language)
    {
        link = string.Empty;
        format = string.Empty;
        language = string.Empty;

        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        var parts = id.Split(Separator, 3);
        if (parts.Length != 3)
        {
            return false;
        }

        format = parts[0];
        language = parts[1];
        return TryFromBase64Url(parts[2], out link);
    }

    private static string ToBase64Url(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool TryFromBase64Url(string value, out string decoded)
    {
        decoded = string.Empty;

        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: return false;
        }

        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
