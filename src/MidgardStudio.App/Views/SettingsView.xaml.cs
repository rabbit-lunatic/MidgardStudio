using System.ComponentModel;
using System.Windows.Controls;
using MidgardStudio.App.ViewModels;

namespace MidgardStudio.App.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _vm;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Hook();
        Loaded += (_, _) => Hook();
    }

    /// <summary>Tracks the active view-model and renders the live Autocomplete preview when it changes.</summary>
    private void Hook()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as SettingsViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelChanged;
            RenderPreview();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.PreviewText)) RenderPreview();
    }

    private void RenderPreview()
    {
        if (_vm is not null) Common.RoColorText.Render(PreviewBox, _vm.PreviewText);
    }
}
