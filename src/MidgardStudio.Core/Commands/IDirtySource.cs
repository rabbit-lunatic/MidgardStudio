using System;

namespace MidgardStudio.Core.Commands;

/// <summary>
/// Something that can hold unsaved changes. Each source — the undo stack, every client-file tracker —
/// exposes this so dirtiness is aggregated in one place (<see cref="CompositeDirtyState"/>) rather than
/// hand-combined by callers. <see cref="DirtyChanged"/> fires whenever <see cref="IsDirty"/> may have flipped.
/// </summary>
public interface IDirtySource
{
    bool IsDirty { get; }
    event Action? DirtyChanged;
}
