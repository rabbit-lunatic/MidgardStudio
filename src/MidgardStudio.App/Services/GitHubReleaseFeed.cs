using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MidgardStudio.Core.Updates;

namespace MidgardStudio.App.Services;

/// <summary>
/// Production <see cref="IReleaseFeed"/>: fetches the latest published release document from GitHub.
/// Anonymous (the public releases API allows unauthenticated reads). Fail-silent — any non-success
/// response or transport error returns <c>null</c>, so the checker simply shows nothing. All parsing
/// and version logic lives in <see cref="UpdateChecker"/>; this adapter only does the network call.
/// </summary>
public sealed class GitHubReleaseFeed : IReleaseFeed
{
    /// <summary>The human releases page; the banner links here when a release omits its own <c>html_url</c>.</summary>
    public const string ReleasesPage = "https://github.com/fahhadalsubaie/MidgardStudio/releases";
    private const string LatestApi = "https://api.github.com/repos/fahhadalsubaie/MidgardStudio/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("MidgardStudio-UpdateCheck"); // GitHub requires a User-Agent
        return c;
    }

    public async Task<string?> GetLatestReleaseJsonAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync(LatestApi, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null; // 403 rate-limit, 404, etc.
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null; // offline / cancelled / DNS — show nothing
        }
    }
}
