using MidgardStudio.Core.Commands;

namespace MidgardStudio.Tests;

/// <summary>
/// <see cref="CompositeDirtyState"/> behaviour, plus <see cref="EditCommandStack"/> acting as an
/// <see cref="IDirtySource"/>. Tested through the interface with a fake source — no App, no WPF.
/// </summary>
public class DirtyStateTests
{
    private sealed class FakeDirtySource : IDirtySource
    {
        private bool _dirty;
        public bool IsDirty => _dirty;
        public event Action? DirtyChanged;
        public void SetDirty(bool value) { _dirty = value; DirtyChanged?.Invoke(); }
    }

    [Fact]
    public void Composite_is_dirty_when_any_source_is_dirty()
    {
        var a = new FakeDirtySource();
        var b = new FakeDirtySource();
        var composite = new CompositeDirtyState(a, b);

        Assert.False(composite.IsDirty);
        a.SetDirty(true);
        Assert.True(composite.IsDirty);
    }

    [Fact]
    public void Composite_raises_DirtyChanged_when_a_source_changes()
    {
        var a = new FakeDirtySource();
        var composite = new CompositeDirtyState(a);
        int fired = 0;
        composite.DirtyChanged += () => fired++;

        a.SetDirty(true);

        Assert.Equal(1, fired);
    }

    private sealed class NoOpCommand : IEditCommand
    {
        public string Description => "noop";
        public void Do() { }
        public void Undo() { }
    }

    [Fact]
    public void Command_stack_is_a_dirty_source_tracking_modified_state()
    {
        var stack = new EditCommandStack();
        IDirtySource source = stack;
        int fired = 0;
        source.DirtyChanged += () => fired++;

        Assert.False(source.IsDirty);
        stack.Execute(new NoOpCommand());

        Assert.True(source.IsDirty);
        Assert.True(fired >= 1);
    }
}
