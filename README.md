# vlsub-jellyfin

A Jellyfin subtitle provider that downloads from **opensubtitles.org with no account and no
API key**, matching files by hash. A C# port of [vlsub-go](https://github.com/joanbono/vlsub-go),
which is itself a port of the [vlsub](https://github.com/exebetche/vlsub) VLC extension.

Requires Jellyfin **10.11** (target ABI `10.11.0.0`, .NET 9).

## Why another OpenSubtitles plugin

Jellyfin already ships an official OpenSubtitles plugin, and it works well. This one differs
in two ways:

1. **No API key or account.** It calls `LogIn` on the XML-RPC API at `api.opensubtitles.org`
   with empty credentials and a registered User-Agent, exactly as vlsub does. Nothing to
   configure before it works.
2. **It repairs split cues.** Some uploads — SDH ones especially — store a two-line cue as
   two consecutive cues sharing one timing. Players anchor subtitles to the bottom and stack
   simultaneous cues upward, so the second line renders *above* the first and the sentence
   reads backwards. On dialogue cues (`- No.` / `- Here, let me try.`) it also misattributes
   lines to the wrong speaker. This plugin detects and merges those cues on download.

If you want the maintained REST API, quota reporting and a large provider ecosystem, use the
official plugin. If you want zero setup and the repair pass, use this one. Running both is
fine — Jellyfin queries providers in the order you configure.

## Install

### From the plugin repository (recommended)

In Jellyfin, go to **Dashboard → Plugins → Repositories → +** and add:

| field | value |
|---|---|
| Repository Name | `vlsub-go` |
| Repository URL | `https://raw.githubusercontent.com/joanbono/vlsub-jellyfin/main/manifest.json` |

Then **Dashboard → Plugins → Catalog → Subtitles → vlsub-go → Install**, and restart
Jellyfin. Later versions show up as ordinary updates in the Catalog, so upgrading is a
click rather than a re-unzip.

Jellyfin verifies the download against the md5 recorded in the manifest, both of which the
release workflow generates.

> The manifest only exists once the first release has been published. Bump `VERSION`, push,
> and let the workflow run before adding the URL.

### Manual

Download `vlsub-go_<version>.zip` from the
[latest release](https://github.com/joanbono/vlsub-jellyfin/releases/latest) and
unpack it into a folder under your Jellyfin plugin directory:

```sh
mkdir -p /config/plugins/vlsub-go_1.0.0.0
unzip vlsub-go_1.0.0.0.zip -d /config/plugins/vlsub-go_1.0.0.0
```

### Either way

Restart Jellyfin, then enable it per library under
**Dashboard → Libraries → *(library)* → Manage → Subtitle Downloads**. Remember to tick your
languages there — an empty language list means Jellyfin never asks any provider for
subtitles.

## Configuration

**Dashboard → Plugins → vlsub-go**

| setting | default | meaning |
|---|---|---|
| Repair split cues | on | Merge consecutive cues that share a timing |
| Prefer SubRip | on | Rank `.srt` above other formats |
| Maximum results | 30 | Candidates listed per search |

A hash match always outranks everything else, including your format preference, because it
is the only signal that a subtitle was timed against your specific file.

## How matching works

The OpenSubtitles hash is a 64-bit wrapping sum of the file size and every little-endian
`ulong` in the first and last 64 KiB:

```
hash = filesize
     + every little-endian uint64 in the first 64 KiB
     + every little-endian uint64 in the last  64 KiB
```

It identifies a *release*, not a title, which is why a hash match is already in sync. Files
under 128 KiB cannot be hashed — the two chunks would overlap — and fall back to a title
search built from the series name plus season and episode number.

Subtitles that are not valid UTF-8 are transcoded from Windows-1252, which is what this API
commonly serves.

## Layout

| file | role |
|---|---|
| `Plugin.cs` | plugin entry point and configuration pages |
| `VlsubSubtitleProvider.cs` | the `ISubtitleProvider` implementation |
| `OpenSubtitlesOrgClient.cs` | keyless XML-RPC client |
| `XmlRpc.cs` | minimal XML-RPC codec — .NET has none built in |
| `OpenSubtitlesHash.cs` | the file hash |
| `SrtRepair.cs` | split-cue repair |
| `SubtitleEncoding.cs` | Windows-1252 to UTF-8 |
| `SubtitleId.cs` | packs the download link into Jellyfin's opaque id |

## Build

```sh
dotnet build -c Release
dotnet test
```

No Docker or Jellyfin checkout needed; the Jellyfin assemblies come from NuGet
(`Jellyfin.Controller`, `Jellyfin.Model`) and are referenced with
`ExcludeAssets="runtime"` so they are compile-time only. The host provides them at runtime,
and shipping copies would risk loading a different build than the server is running. CI
asserts that none leak into the output.

## Tests

34 tests cover the hash, the XML-RPC codec, the repair pass, id round-tripping and the
encoding fallback.

The hash tests derive their expected values from the specification by hand rather than from a
reference implementation: an all-zero file hashes to its own size, an all-ones file exercises
64-bit wraparound, and a file with a dirty middle section proves only the end chunks are read.
The XML-RPC tests decode a real captured `LogIn` response, an array-of-structs search result,
a fault, and the no-results case where the server sends `data` as boolean `false` rather than
an empty array.

The repair pass was validated against a real 33 KB broken subtitle file and produces output
**byte-identical** to the Go implementation — same SHA-256, 558 cues reduced to 363 with 195
merges.

## Releasing

Bump `VERSION` and push to `main`; the release workflow tags, builds, packages the zip with a
generated `meta.json`, and publishes the release with md5 and sha256 sums. Jellyfin versions
are four numeric parts, e.g. `1.0.1.0`.

## Credit

The approach, the hash and the keyless anonymous login all originate in
[exebetche/vlsub](https://github.com/exebetche/vlsub) by Antoine Bécot.

## License

[Apache-2.0](LICENSE)
