using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VlsubGo;

/// <summary>
/// One search hit, normalised out of the XML-RPC response.
/// </summary>
public sealed record SubtitleCandidate
{
    public required string Name { get; init; }
    public required string Language { get; init; }
    public required string Format { get; init; }
    public required string DownloadLink { get; init; }
    public int Downloads { get; init; }
    public float FrameRate { get; init; }
    public bool HearingImpaired { get; init; }
    public bool HashMatch { get; init; }
    public bool Trusted { get; init; }
}

/// <summary>
/// Client for the legacy XML-RPC API on opensubtitles.org. No API key is
/// required: LogIn is called with empty credentials and only a registered
/// User-Agent, which is how the vlsub VLC extension works.
/// </summary>
public sealed class OpenSubtitlesOrgClient
{
    private const string Endpoint = "https://api.opensubtitles.org/xml-rpc";

    /// <summary>
    /// The API rate-limits on the User-Agent and rejects unregistered ones, so
    /// the string vlsub uses is sent verbatim.
    /// </summary>
    private const string UserAgent = "VLSub 0.10.2";

    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private string? _token;

    public OpenSubtitlesOrgClient(HttpClient http, ILogger logger)
    {
        _http = http;
        _logger = logger;
    }

    private async Task<IReadOnlyDictionary<string, object?>> CallAsync(
        string method, object?[] parameters, CancellationToken cancellationToken)
    {
        var body = XmlRpc.BuildRequest(method, parameters);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/xml"),
        };
        request.Headers.UserAgent.ParseAdd(UserAgent);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = XmlRpc.ParseResponse(xml);

        if (parsed is not Dictionary<string, object?> map)
        {
            throw new InvalidOperationException($"{method}: expected a struct response");
        }

        var status = XmlRpc.GetString(map, "status");
        if (!string.IsNullOrEmpty(status) && !status.StartsWith("200", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{method}: {status}");
        }

        return map;
    }

    private async Task EnsureLoggedInAsync(CancellationToken cancellationToken)
    {
        if (_token is not null)
        {
            return;
        }

        await _loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_token is not null)
            {
                return;
            }

            var map = await CallAsync("LogIn", new object?[] { string.Empty, string.Empty, "en", UserAgent }, cancellationToken)
                .ConfigureAwait(false);

            var token = XmlRpc.GetString(map, "token");
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("LogIn returned no token");
            }

            _token = token;
            _logger.LogDebug("vlsub-go: anonymous session established");
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>
    /// Searches by file hash and by title in a single call. The server reports
    /// which criterion matched in MatchedBy, so both are sent together.
    /// </summary>
    public async Task<IReadOnlyList<SubtitleCandidate>> SearchAsync(
        string language,
        string? hash,
        long size,
        string? title,
        int? season,
        int? episode,
        CancellationToken cancellationToken)
    {
        await EnsureLoggedInAsync(cancellationToken).ConfigureAwait(false);

        var criteria = new List<object?>();

        if (!string.IsNullOrEmpty(hash) && size > 0)
        {
            criteria.Add(new Dictionary<string, object?>
            {
                ["sublanguageid"] = language,
                ["moviehash"] = hash,
                ["moviebytesize"] = size.ToString(CultureInfo.InvariantCulture),
            });
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var byTitle = new Dictionary<string, object?>
            {
                ["sublanguageid"] = language,
                ["query"] = title,
            };
            if (season is > 0)
            {
                byTitle["season"] = season.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (episode is > 0)
            {
                byTitle["episode"] = episode.Value.ToString(CultureInfo.InvariantCulture);
            }

            criteria.Add(byTitle);
        }

        if (criteria.Count == 0)
        {
            return Array.Empty<SubtitleCandidate>();
        }

        var map = await CallAsync("SearchSubtitles", new object?[] { _token, criteria }, cancellationToken)
            .ConfigureAwait(false);

        // With no hits the server sends data as boolean false, not an array.
        if (!map.TryGetValue("data", out var data) || data is not List<object?> rows)
        {
            return Array.Empty<SubtitleCandidate>();
        }

        var results = new List<SubtitleCandidate>(rows.Count);
        foreach (var row in rows)
        {
            if (row is not Dictionary<string, object?> item)
            {
                continue;
            }

            var link = XmlRpc.GetString(item, "SubDownloadLink");
            if (string.IsNullOrEmpty(link))
            {
                continue;
            }

            var name = XmlRpc.GetString(item, "MovieReleaseName");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = XmlRpc.GetString(item, "SubFileName");
            }

            var rank = XmlRpc.GetString(item, "UserRank");

            results.Add(new SubtitleCandidate
            {
                Name = name.Trim(),
                Language = XmlRpc.GetString(item, "SubLanguageID"),
                Format = XmlRpc.GetString(item, "SubFormat"),
                DownloadLink = link,
                Downloads = XmlRpc.GetInt(item, "SubDownloadsCnt"),
                FrameRate = XmlRpc.GetFloat(item, "MovieFPS"),
                HearingImpaired = XmlRpc.GetString(item, "SubHearingImpaired") == "1",
                HashMatch = XmlRpc.GetString(item, "MatchedBy") == "moviehash",
                Trusted = rank.Equals("trusted", StringComparison.OrdinalIgnoreCase)
                          || rank.Equals("administrator", StringComparison.OrdinalIgnoreCase)
                          || rank.Equals("platinum member", StringComparison.OrdinalIgnoreCase),
            });
        }

        return results;
    }

    /// <summary>
    /// Fetches a subtitle, decompressing the gzip the download endpoint serves.
    /// </summary>
    public async Task<byte[]> DownloadAsync(string link, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, link);
        request.Headers.UserAgent.ParseAdd(UserAgent);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return Gunzip(raw);
    }

    /// <summary>
    /// Decompresses when the payload carries the gzip magic number. The endpoint
    /// always gzips, but a plain body is tolerated.
    /// </summary>
    internal static byte[] Gunzip(byte[] data)
    {
        if (data.Length < 2 || data[0] != 0x1F || data[1] != 0x8B)
        {
            return data;
        }

        using var input = new MemoryStream(data);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
