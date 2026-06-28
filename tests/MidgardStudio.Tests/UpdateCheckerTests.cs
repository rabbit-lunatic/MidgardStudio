using MidgardStudio.Core.Updates;

namespace MidgardStudio.Tests;

/// <summary>
/// Behaviour of <see cref="UpdateChecker"/> driven entirely through <see cref="UpdateChecker.CheckAsync"/>
/// against a fake feed — no network, no real assembly version. The version-compare is an implementation
/// detail and is never tested directly.
/// </summary>
public class UpdateCheckerTests
{
    private sealed class FakeFeed : IReleaseFeed
    {
        private readonly string? _json;
        public FakeFeed(string? json) => _json = json;
        public Task<string?> GetLatestReleaseJsonAsync(CancellationToken ct = default) => Task.FromResult(_json);
    }

    /// <summary>A canned GitHub "latest release" document with only the fields the checker reads.</summary>
    private static string Release(string tag, string htmlUrl = "https://example/releases/tag", bool prerelease = false) =>
        $$"""{"tag_name":"{{tag}}","html_url":"{{htmlUrl}}","prerelease":{{(prerelease ? "true" : "false")}}}""";

    [Fact]
    public async Task Higher_patch_release_is_offered_as_an_update()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("v1.0.4")));

        var result = await checker.CheckAsync("1.0.3");

        Assert.NotNull(result);
        Assert.Equal("1.0.4", result!.Version);
    }

    [Fact]
    public async Task Same_version_is_not_an_update()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("v1.0.3")));
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task Older_release_is_not_an_update()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("v1.0.2")));
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task Prerelease_is_never_offered_even_when_newer()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("v2.0.0", prerelease: true)));
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task No_release_document_means_no_update()
    {
        var checker = new UpdateChecker(new FakeFeed(null)); // feed returns null = offline / rate-limited
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task Malformed_json_is_swallowed()
    {
        var checker = new UpdateChecker(new FakeFeed("{ not json"));
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task Missing_tag_name_means_no_update()
    {
        var checker = new UpdateChecker(new FakeFeed("""{"html_url":"https://x"}"""));
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task Tag_and_current_v_prefixes_are_normalized()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("V1.1.0")));

        var r = await checker.CheckAsync("v1.0.3");

        Assert.NotNull(r);
        Assert.Equal("1.1.0", r!.Version);
    }

    [Fact]
    public async Task Four_part_current_version_compares_on_first_three()
    {
        // the running assembly's version carries a 4th, always-zero field
        var checker = new UpdateChecker(new FakeFeed(Release("v1.0.4")));
        Assert.NotNull(await checker.CheckAsync("1.0.3.0"));
    }

    [Fact]
    public async Task Same_first_three_parts_with_a_fourth_field_is_not_an_update()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("v1.0.3")));
        Assert.Null(await checker.CheckAsync("1.0.3.0"));
    }

    [Fact]
    public async Task Missing_html_url_yields_an_empty_url_for_the_caller_to_fill()
    {
        var checker = new UpdateChecker(new FakeFeed("""{"tag_name":"v1.0.4"}"""));

        var r = await checker.CheckAsync("1.0.3");

        Assert.NotNull(r);
        Assert.Equal(string.Empty, r!.Url);
    }

    [Fact]
    public async Task Whitespace_around_the_tag_is_tolerated()
    {
        var checker = new UpdateChecker(new FakeFeed(Release(" v1.0.4 ")));

        var r = await checker.CheckAsync("1.0.3");

        Assert.NotNull(r);
        Assert.Equal("1.0.4", r!.Version);
    }

    [Fact]
    public async Task Build_metadata_tag_is_not_offered()
    {
        // GitHub Actions sometimes appends +build/+sha; System.Version can't parse it → fail-silent.
        var checker = new UpdateChecker(new FakeFeed(Release("v1.0.4+build5")));
        Assert.Null(await checker.CheckAsync("1.0.3"));
    }

    [Fact]
    public async Task Four_part_tag_is_compared_on_its_first_three_parts()
    {
        var checker = new UpdateChecker(new FakeFeed(Release("v1.2.3.4")));
        Assert.Null(await checker.CheckAsync("1.2.3"));
    }
}
