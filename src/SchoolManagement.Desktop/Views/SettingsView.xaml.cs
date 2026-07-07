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
    }

    public string? ActivePlaceholderKey
    {
        get => (string?)GetValue(ActivePlaceholderKeyProperty);
        set => SetValue(ActivePlaceholderKeyProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        SettingsNavigationBridge.SectionSelected += OnSettingsSectionSelected;

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        SettingsNavigationBridge.SectionSelected -= OnSettingsSectionSelected;

    private void OnSettingsSectionSelected(SettingsNavItem item)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        if (item.Section is SettingsSection section)
        {
            ActivePlaceholderKey = null;
            var node = viewModel.SettingsNodes
                .SelectMany(node => node.Children)
                .FirstOrDefault(node => node.Section == section);

            if (node is not null)
            {
                viewModel.SelectedSettingsNode = node;
            }

            return;
        }

        ActivePlaceholderKey = item.Key;
    }
}
