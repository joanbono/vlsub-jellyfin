using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Repairs subtitle files whose multi-line cues were flattened into separate
/// cues sharing one timing. Ported from vlsub-go.
/// </summary>
public static partial class SrtRepair
{
    [GeneratedRegex(@"\n[ \t]*\n", RegexOptions.None, 1000)]
    private static partial Regex BlankLine();

    private readonly record struct Cue(string Timing, List<string> Lines);

    /// <summary>
    /// Merges consecutive cues that share a timing.
    /// <para>
    /// Some uploads — SDH ones especially — store a cue that should hold two
    /// lines as two consecutive cues with identical start and end times.
    /// Players anchor subtitles to the bottom of the frame and stack
    /// simultaneous cues upward, so the second line renders *above* the first
    /// and the sentence reads backwards. On dialogue cues it also misattributes
    /// lines to the wrong speaker.
    /// </para>
    /// </summary>
    /// <param name="srt">The subtitle text.</param>
    /// <param name="merged">How many cues were folded into a predecessor.</param>
    /// <returns>The repaired text, or <paramref name="srt"/> unchanged when
    /// there was nothing to merge.</returns>
    public static string Apply(string srt, out int merged)
    {
        merged = 0;
        if (string.IsNullOrEmpty(srt))
        {
            return srt;
        }

        var text = srt.Replace("\r\n", "\n", StringComparison.Ordinal).TrimStart('\uFEFF');

        var cues = new List<Cue>();
        foreach (var block in BlankLine().Split(text.Trim()))
        {
            var lines = block.Trim().Split('\n');

            // A well-formed block is: index, timing, then one or more text lines.
            if (lines.Length < 3 || !lines[1].Contains("-->", StringComparison.Ordinal))
            {
                continue;
            }

            var body = lines.Skip(2).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            cues.Add(new Cue(lines[1].Trim(), body));
        }

        var result = new List<Cue>(cues.Count);
        foreach (var cue in cues)
        {
            if (result.Count > 0 && string.Equals(result[^1].Timing, cue.Timing, StringComparison.Ordinal))
            {
                result[^1].Lines.AddRange(cue.Lines);
                continue;
            }

            result.Add(new Cue(cue.Timing, new List<string>(cue.Lines)));
        }

        merged = cues.Count - result.Count;
        if (merged == 0)
        {
            // Leave healthy input untouched rather than round-tripping it.
            return srt;
        }

        var builder = new StringBuilder(srt.Length);
        for (var i = 0; i < result.Count; i++)
        {
            builder.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(result[i].Timing).Append('\n');
            builder.Append(string.Join("\n", result[i].Lines)).Append("\n\n");
        }

        return builder.ToString();
    }
}
