using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MidgardStudio.App.Common;

/// <summary>
/// Attached behaviors for the master lists:
/// <list type="bullet">
/// <item><c>SelectOnRightClick</c> — right-clicking a row selects it (so a list-level ContextMenu acts on the
/// row the user clicked). Clicking a row already inside a multi-selection keeps the whole selection; clicking
/// an unselected row replaces the selection with just that row (Explorer-style).</item>
/// <item><c>SelectedItems</c> — one-way (view → view-model) mirror of a multi-select list's
/// <c>SelectedItems</c> into a bound <see cref="IList"/>, since <see cref="ListBox.SelectedItems"/> isn't
/// bindable. Used for "Copy YAML" of several rows at once.</item>
/// </list>
/// Avoids per-item ContextMenu styles, which can't resolve the themed ListViewItem style from inside a
/// merged ResourceDictionary.
/// </summary>
public static class ListBehaviors
{
    // ---- SelectOnRightClick ----

    public static readonly DependencyProperty SelectOnRightClickProperty = DependencyProperty.RegisterAttached(
        "SelectOnRightClick", typeof(bool), typeof(ListBehaviors), new PropertyMetadata(false, OnSelectOnRightClickChanged));

    public static void SetSelectOnRightClick(DependencyObject o, bool value) => o.SetValue(SelectOnRightClickProperty, value);

    public static bool GetSelectOnRightClick(DependencyObject o) => (bool)o.GetValue(SelectOnRightClickProperty);

    private static void OnSelectOnRightClickChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not ListBox list) return;
        if (e.NewValue is true)
            list.PreviewMouseRightButtonDown += OnRightDown;
        else
            list.PreviewMouseRightButtonDown -= OnRightDown;
    }

    private static void OnRightDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null and not ListBoxItem)
            dep = VisualTreeHelper.GetParent(dep);

        if (dep is ListBoxItem item)
        {
            // Keep an existing multi-selection if the clicked row is part of it; otherwise select just this row.
            if (!item.IsSelected)
            {
                if (sender is ListBox lb) lb.SelectedItems.Clear();
                item.IsSelected = true;
            }
            item.Focus();
        }
        else if (sender is System.Windows.Controls.Primitives.Selector selector)
        {
            selector.SelectedItem = null; // right-clicking empty space clears selection
        }
    }

    // ---- SelectedItems (bindable mirror) ----

    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.RegisterAttached(
        "SelectedItems", typeof(IList), typeof(ListBehaviors), new PropertyMetadata(null, OnSelectedItemsChanged));

    public static void SetSelectedItems(DependencyObject o, IList? value) => o.SetValue(SelectedItemsProperty, value);

    public static IList? GetSelectedItems(DependencyObject o) => (IList?)o.GetValue(SelectedItemsProperty);

    private static void OnSelectedItemsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not ListBox list) return;
        list.SelectionChanged -= OnSelectionChanged;
        if (e.NewValue is not null)
        {
            list.SelectionChanged += OnSelectionChanged;
            Sync(list); // seed the target with the current selection
        }
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list) Sync(list);
    }

    private static void Sync(ListBox list)
    {
        if (GetSelectedItems(list) is not { } target) return;
        target.Clear();
        foreach (var item in list.SelectedItems) target.Add(item);
    }
}
