using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class SettingsView : UserControl
{
    public static readonly DependencyProperty ActivePlaceholderKeyProperty =
        DependencyProperty.Register(
            nameof(ActivePlaceholderKey),
            typeof(string),
            typeof(SettingsView),
            new PropertyMetadata(null));

    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    public string? ActivePlaceholderKey
    {
        get => (string?)GetValue(ActivePlaceholderKeyProperty);
        set => SetValue(ActivePlaceholderKeyProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SettingsNavigationBridge.SectionSelected += OnSettingsSectionSelected;
        ApplyCurrentSelection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        SettingsNavigationBridge.SectionSelected -= OnSettingsSectionSelected;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        ApplyCurrentSelection();

    private void ApplyCurrentSelection()
    {
        if (SettingsNavigationBridge.CurrentSelection is { } item)
        {
            OnSettingsSectionSelected(item);
        }
    }

    private void OnSettingsSectionSelected(SettingsNavItem item)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            SettingsNavigationBridge.ApplyToViewModel(viewModel, item);
        }

        ActivePlaceholderKey = item.Section is null ? item.Key : null;
    }
}
