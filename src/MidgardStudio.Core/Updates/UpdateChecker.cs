using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MidgardStudio.Core.Updates;

/// <summary>
/// Decides whether a newer release than the running build is available, via an injected
/// <see cref="IReleaseFeed"/>. Fail-silent: any fetch/parse problem yields <c>null</c> (no update shown).
/// The version comparison is internal — exercise it through <see cref="CheckAsync"/>.
/// </summary>
public sealed class UpdateChecker
{
    private readonly IReleaseFeed _feed;

    public UpdateChecker(IReleaseFeed feed) => _feed = feed;

    /// <summary>
    /// Returns the newer release, or <c>null</c> when the build is current or nothing could be determined.
    /// </summary>
    public async Task<UpdateAvailability?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        string? json = await _feed.GetLatestReleaseJsonAsync(ct).ConfigureAwait(false);
        if (json is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

            string tag = (root.GetProperty("tag_name").GetString() ?? string.Empty).Trim();
            string url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;

            if (Parse(tag) > Parse(currentVersion))
                return new UpdateAvailability(tag.TrimStart('v', 'V'), url);
            return null;
        }
        catch
        {
            return null; // malformed document, missing field, or unparseable version → fail-silent
        }
    }

    /// <summary>Normalize a tag or version string to a comparable major.minor.patch <see cref="Version"/>.</summary>
    private static Version Parse(string s)
    {
        var v = Version.Parse(s.Trim().TrimStart('v', 'V'));
        return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
    }
}
