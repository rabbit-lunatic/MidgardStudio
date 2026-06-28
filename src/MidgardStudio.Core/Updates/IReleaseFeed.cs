using System.Threading;
using System.Threading.Tasks;

namespace MidgardStudio.Core.Updates;

/// <summary>
/// Port for fetching the latest published release document. Returns the raw response body, or
/// <c>null</c> when it can't be fetched (offline, rate-limited, non-success response). All logic —
/// parsing the document and comparing versions — lives in <see cref="UpdateChecker"/>, so the whole
/// decision is testable through a fake feed without a network call.
/// </summary>
public interface IReleaseFeed
{
    Task<string?> GetLatestReleaseJsonAsync(CancellationToken ct = default);
}
