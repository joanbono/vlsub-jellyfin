using System.Globalization;
using System.Text;
using Jellyfin.Plugin.VlsubGo.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// Subtitle provider backed by the keyless opensubtitles.org XML-RPC API.
/// Jellyfin discovers this type automatically.
/// </summary>
public class VlsubSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<VlsubSubtitleProvider> _logger;
    private readonly OpenSubtitlesOrgClient _client;

    public VlsubSubtitleProvider(ILogger<VlsubSubtitleProvider> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _client = new OpenSubtitlesOrgClient(httpClientFactory.CreateClient(NamedClient), logger);
    }

    /// <summary>
    /// Name of the HttpClient this provider requests from the factory.
    /// </summary>
    public const string NamedClient = "VlsubGo";

    public string Name => "vlsub-go (OpenSubtitles.org)";

    public IEnumerable<VideoContentType> SupportedMediaTypes { get; } =
        new[] { VideoContentType.Episode, VideoContentType.Movie };

    private static PluginConfiguration Config =>
        Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(
        SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        // The .org API keys subtitles by ISO 639-2/B, which is what Jellyfin
        // puts in Language; TwoLetterISOLanguageName is the ISO 639-1 form.
        var language = request.Language;
        if (string.IsNullOrWhiteSpace(language))
        {
            _logger.LogDebug("vlsub-go: search skipped, no language on the request");
            return Array.Empty<RemoteSubtitleInfo>();
        }

        string? hash = null;
        long size = 0;
        if (!string.IsNullOrEmpty(request.MediaPath))
        {
            try
            {
                if (OpenSubtitlesHash.TryCompute(request.MediaPath, out var computed, out var length))
                {
                    hash = computed;
                    size = length;
                }
                else
                {
                    _logger.LogDebug("vlsub-go: {Path} too small to hash", request.MediaPath);
                }
            }
            catch (IOException ex)
            {
                // An unreadable file is not fatal; the title search still works.
                _logger.LogWarning(ex, "vlsub-go: could not hash {Path}", request.MediaPath);
            }
        }

        var title = request.ContentType == VideoContentType.Episode
            ? request.SeriesName
            : request.Name;

        IReadOnlyList<SubtitleCandidate> candidates;
        try
        {
            candidates = await _client.SearchAsync(
                language,
                hash,
                size,
                title,
                request.ParentIndexNumber,
                request.IndexNumber,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _logger.LogError(ex, "vlsub-go: search failed");
            return Array.Empty<RemoteSubtitleInfo>();
        }

        var config = Config;
        _logger.LogInformation(
            "vlsub-go: {Count} result(s) for {Title} in {Language}, {Hashed} by hash",
            candidates.Count, title, language, candidates.Count(c => c.HashMatch));

        return Rank(candidates, config.PreferSubRip)
            .Take(Math.Max(1, config.MaxResults))
            .Select(c => new RemoteSubtitleInfo
            {
                Id = SubtitleId.Encode(c.DownloadLink, Extension(c.Format), c.Language),
                Name = string.IsNullOrEmpty(c.Name) ? "(unnamed)" : c.Name,
                ProviderName = Name,
                Format = Extension(c.Format),
                ThreeLetterISOLanguageName = c.Language,
                DownloadCount = c.Downloads,
                FrameRate = c.FrameRate > 0 ? c.FrameRate : null,
                HearingImpaired = c.HearingImpaired,
                IsHashMatch = c.HashMatch,
                Comment = Comment(c),
            })
            .ToList();
    }

    public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
    {
        if (!SubtitleId.TryDecode(id, out var link, out var format, out var language))
        {
            throw new ArgumentException("Malformed subtitle id", nameof(id));
        }

        var raw = await _client.DownloadAsync(link, cancellationToken).ConfigureAwait(false);
        var text = SubtitleEncoding.ToText(raw, out var converted);
        if (converted)
        {
            _logger.LogDebug("vlsub-go: transcoded a subtitle from Windows-1252");
        }

        if (Config.RepairSplitCues && string.Equals(format, "srt", StringComparison.OrdinalIgnoreCase))
        {
            text = SrtRepair.Apply(text, out var merged);
            if (merged > 0)
            {
                _logger.LogInformation(
                    "vlsub-go: merged {Count} split cue(s) that would have rendered bottom-to-top", merged);
            }
        }

        return new SubtitleResponse
        {
            Format = format,
            Language = language,
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(text)),
        };
    }

    /// <summary>
    /// Orders candidates best-first. A hash match wins outright: it is the only
    /// signal that the subtitle was timed against this exact release.
    /// </summary>
    internal static IEnumerable<SubtitleCandidate> Rank(
        IEnumerable<SubtitleCandidate> candidates, bool preferSubRip) =>
        candidates
            .OrderByDescending(c => c.HashMatch)
            .ThenByDescending(c => preferSubRip && Extension(c.Format) == "srt")
            .ThenByDescending(c => c.Trusted)
            .ThenByDescending(c => c.Downloads);

    private static string Extension(string format)
    {
        var f = format.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(f) ? "srt" : f;
    }

    private static string Comment(SubtitleCandidate c)
    {
        var tags = new List<string>();
        if (c.HashMatch)
        {
            tags.Add("hash match");
        }

        if (c.Trusted)
        {
            tags.Add("trusted uploader");
        }

        if (c.FrameRate > 0)
        {
            tags.Add(c.FrameRate.ToString("0.###", CultureInfo.InvariantCulture) + " fps");
        }

        return string.Join(", ", tags);
    }
}
