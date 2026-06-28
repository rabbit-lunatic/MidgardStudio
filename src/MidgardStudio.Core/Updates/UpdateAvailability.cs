namespace MidgardStudio.Core.Updates;

/// <summary>
/// A published release newer than the running build. <see cref="Url"/> is the release's page
/// (GitHub <c>html_url</c>) and may be empty — when it is, the caller supplies its own fallback link.
/// </summary>
public sealed record UpdateAvailability(string Version, string Url);
