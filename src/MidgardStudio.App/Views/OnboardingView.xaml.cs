using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MidgardStudio.App.ViewModels;

namespace MidgardStudio.App.Views;

public partial class OnboardingView : UserControl
{
    private OnboardingViewModel? _vm;

    public OnboardingView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => { PlayEntrance(); Focus(); }; // take focus so keyboard navigation works immediately
    }

    /// <summary>Keyboard navigation for the tour. Handled at the tunneling stage so focused buttons can't
    /// swallow the keys: Enter/Right → Next, Left → Back, Esc → Skip.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm is null) return;
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Right:
                _vm.NextCommand.Execute(null); e.Handled = true; break;
            case Key.Left:
                _vm.BackCommand.Execute(null); e.Handled = true; break;
            case Key.Escape:
                _vm.SkipCommand.Execute(null); e.Handled = true; break;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = e.NewValue as OnboardingViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The content host is reused across pages, so replay the entrance animation on each page change.
        if (e.PropertyName == nameof(OnboardingViewModel.CurrentIndex)) PlayEntrance();
    }

    private void PlayEntrance()
    {
        if (FindResource("PageEntrance") is Storyboard sb) sb.Begin(this);
    }
}
