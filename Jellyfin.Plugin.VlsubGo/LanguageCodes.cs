namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Maps ISO 639-1 codes onto the ISO 639-2/B codes opensubtitles.org serves.
/// Ported from vlsub-go. Jellyfin normally supplies the three-letter form, but
/// it is not guaranteed, and an unrecognised language silently returns nothing.
/// </summary>
public static class LanguageCodes
{
    /// <summary>
    /// Note the OpenSubtitles-specific spellings: Greek is "ell" rather than
    /// "gre", and Serbian is "scc" rather than "srp".
    /// </summary>
    private static readonly Dictionary<string, string> TwoToThree = new(StringComparer.OrdinalIgnoreCase)
    {
        ["af"] = "afr", ["ar"] = "ara", ["az"] = "aze", ["be"] = "bel", ["bg"] = "bul",
        ["bn"] = "ben", ["bs"] = "bos", ["br"] = "bre", ["ca"] = "cat", ["cs"] = "cze",
        ["cy"] = "wel", ["da"] = "dan", ["de"] = "ger", ["el"] = "ell", ["en"] = "eng",
        ["eo"] = "epo", ["es"] = "spa", ["et"] = "est", ["eu"] = "baq", ["fa"] = "per",
        ["fi"] = "fin", ["fr"] = "fre", ["ga"] = "gle", ["gd"] = "gla", ["gl"] = "glg",
        ["he"] = "heb", ["hi"] = "hin", ["hr"] = "hrv", ["hu"] = "hun", ["hy"] = "arm",
        ["id"] = "ind", ["is"] = "ice", ["it"] = "ita", ["ja"] = "jpn", ["ka"] = "geo",
        ["kk"] = "kaz", ["km"] = "khm", ["ko"] = "kor", ["ku"] = "kur", ["la"] = "lat",
        ["lb"] = "ltz", ["lt"] = "lit", ["lv"] = "lav", ["mk"] = "mac", ["ml"] = "mal",
        ["mn"] = "mon", ["ms"] = "may", ["ne"] = "nep", ["nl"] = "dut", ["no"] = "nor",
        ["oc"] = "oci", ["pl"] = "pol", ["pt"] = "por", ["ro"] = "rum", ["ru"] = "rus",
        ["si"] = "sin", ["sk"] = "slo", ["sl"] = "slv", ["sq"] = "alb", ["sr"] = "scc",
        ["sv"] = "swe", ["sw"] = "swa", ["ta"] = "tam", ["te"] = "tel", ["th"] = "tha",
        ["tl"] = "tgl", ["tr"] = "tur", ["uk"] = "ukr", ["ur"] = "urd", ["vi"] = "vie",
        ["zh"] = "chi",
    };

    public static bool TryGetThreeLetter(string twoLetter, out string threeLetter)
    {
        if (!string.IsNullOrWhiteSpace(twoLetter)
            && TwoToThree.TryGetValue(twoLetter.Trim(), out var mapped))
        {
            threeLetter = mapped;
            return true;
        }

        threeLetter = string.Empty;
        return false;
    }
}
