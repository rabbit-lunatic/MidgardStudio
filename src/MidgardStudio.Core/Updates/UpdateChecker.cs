using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MidgardStudio.Core.Updates;

/// <summary>Outcome of an update check.</summary>
public enum UpdateStatus { UpToDate, UpdateAvailable, CheckFailed }

/// <summary>A detailed check result: the status, plus the release when one is newer.</summary>
public sealed record UpdateCheckResult(UpdateStatus Status, UpdateAvailability? Update)
{
    public static readonly UpdateCheckResult UpToDate = new(UpdateStatus.UpToDate, null);
    public static readonly UpdateCheckResult Failed = new(UpdateStatus.CheckFailed, null);
    public static UpdateCheckResult Available(UpdateAvailability update) => new(UpdateStatus.UpdateAvailable, update);
}

/// <summary>
/// Decides whether a newer release than the running build is available, via an injected
/// <see cref="IReleaseFeed"/>. The version comparison is internal — exercise it through the public methods.
/// </summary>
public sealed class UpdateChecker
{
    private readonly IReleaseFeed _feed;

    public UpdateChecker(IReleaseFeed feed) => _feed = feed;

    /// <summary>
    /// The newer release, or <c>null</c> when the build is current or nothing could be determined. Fail-silent;
    /// used by the startup banner, which shows nothing on failure.
    /// </summary>
    public async Task<UpdateAvailability?> CheckAsync(string currentVersion, CancellationToken ct = default)
        => (await CheckDetailedAsync(currentVersion, ct).ConfigureAwait(false)).Update;

    /// <summary>
    /// A check that distinguishes <see cref="UpdateStatus.UpToDate"/> from a <see cref="UpdateStatus.CheckFailed"/>
    /// fetch — for the interactive "Check for updates" UI, which wants to tell "you're current" from "couldn't reach
    /// the server". Pre-releases count as up-to-date (no stable update on offer).
    /// </summary>
    public async Task<UpdateCheckResult> CheckDetailedAsync(string currentVersion, CancellationToken ct = default)
    {
        string? json = await _feed.GetLatestReleaseJsonAsync(ct).ConfigureAwait(false);
        if (json is null) return UpdateCheckResult.Failed;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return UpdateCheckResult.UpToDate;

            string tag = (root.GetProperty("tag_name").GetString() ?? string.Empty).Trim();
            string url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? string.Empty : string.Empty;

            if (Parse(tag) > Parse(currentVersion))
                return UpdateCheckResult.Available(new UpdateAvailability(tag.TrimStart('v', 'V'), url));
            return UpdateCheckResult.UpToDate;
        }
        catch
        {
            return UpdateCheckResult.Failed; // malformed document, missing field, or unparseable version
        }
    }

    /// <summary>Normalize a tag or version string to a comparable major.minor.patch <see cref="Version"/>.</summary>
    private static Version Parse(string s)
    {
        var v = Version.Parse(s.Trim().TrimStart('v', 'V'));
        return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
    }
}
