using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using MidgardStudio.App.ViewModels;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace MidgardStudio.App.Views;

/// <summary>
/// Client itemInfo editor: identified + unidentified sections with a live in-game-style preview, plus
/// description tools (color-code palette, New Line, reset) that insert RO color codes at the caret of
/// whichever description box is focused.
/// </summary>
public partial class ClientItemsView : UserControl
{
    private WpfTextBox? _activeDesc;

    public ClientItemsView()
    {
        InitializeComponent();
        NewLineButton.Click += (_, _) => Insert("^FFFFFF_^000000");   // white "_" on the white in-game bg = blank line
        ResetColorButton.Click += (_, _) => Insert("^000000");
        MasterList.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));
        MasterList.SelectionChanged += (_, _) =>
        {
            if (MasterList.SelectedItem is { } item) MasterList.ScrollIntoView(item);
        };
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header) return;
        if ((DataContext as ClientItemsViewModel)?.List is not { } list) return;

        string? key = header.Column == IdColumn ? "Id" : header.Column == NameColumn ? "Name" : null;
        if (key is null) return;

        list.ToggleSort(key);
        IdColumn.Header = list.HeaderText("Id");
        NameColumn.Header = list.HeaderText("Name");
    }

    private void DescBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is WpfTextBox box) _activeDesc = box;
    }

    private void Swatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe && fe.Tag is string hex)
            Insert("^" + hex);
    }

    /// <summary>Inserts a code at the caret of the active (last-focused) description box, committing the
    /// change to the bound view-model immediately so it records an undo step and flags unsaved changes
    /// (the boxes bind with UpdateSourceTrigger=LostFocus, which otherwise wouldn't fire on a button click).</summary>
    private void Insert(string code)
    {
        var box = _activeDesc ?? IdDescBox;
        int caret = box.CaretIndex;
        box.Text = box.Text.Insert(caret, code);
        box.CaretIndex = caret + code.Length;
        box.GetBindingExpression(WpfTextBox.TextProperty)?.UpdateSource();
        box.Focus();
    }

    private void IdDesc_TextChanged(object sender, TextChangedEventArgs e) => Common.RoColorText.Render(IdPreview, IdDescBox.Text);

    private void UnidDesc_TextChanged(object sender, TextChangedEventArgs e) => Common.RoColorText.Render(UnidPreview, UnidDescBox.Text);
}
