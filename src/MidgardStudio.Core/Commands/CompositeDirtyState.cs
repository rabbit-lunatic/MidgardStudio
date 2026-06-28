using System;
using System.Collections.Generic;
using System.Linq;

namespace MidgardStudio.Core.Commands;

/// <summary>
/// Aggregates several <see cref="IDirtySource"/>s into one. Dirty when any source is dirty; raises
/// <see cref="DirtyChanged"/> whenever any source changes — so a caller asks "are there unsaved changes?"
/// at one seam instead of OR-ing every source by hand.
/// </summary>
public sealed class CompositeDirtyState : IDirtySource
{
    private readonly IReadOnlyList<IDirtySource> _sources;

    public CompositeDirtyState(params IDirtySource[] sources)
    {
        _sources = sources;
        foreach (var s in _sources) s.DirtyChanged += Raise;
    }

    public bool IsDirty => _sources.Any(s => s.IsDirty);

    public event Action? DirtyChanged;

    private void Raise() => DirtyChanged?.Invoke();
}
