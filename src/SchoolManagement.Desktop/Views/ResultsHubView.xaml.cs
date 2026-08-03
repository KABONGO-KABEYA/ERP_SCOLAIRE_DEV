using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ResultsHubView : UserControl
{
    public ResultsHubView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResultsNavigationBridge.SectionSelected += OnResultsSectionSelected;
        if (ResultsNavigationBridge.CurrentSelection is { } item && DataContext is ResultsHubViewModel vm)
        {
            ResultsNavigationBridge.ApplyToViewModel(vm, item);
        }
        else if (DataContext is ResultsHubViewModel hub && string.IsNullOrWhiteSpace(hub.ActiveNavKey))
        {
            var defaultItem = ResultsNavCatalog.DefaultItem;
            ResultsNavigationBridge.ApplyToViewModel(hub, defaultItem);
            ResultsNavigationBridge.Select(defaultItem);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResultsNavigationBridge.SectionSelected -= OnResultsSectionSelected;
    }

    private void OnResultsSectionSelected(ResultsNavItem item)
    {
        if (DataContext is ResultsHubViewModel viewModel)
        {
            ResultsNavigationBridge.ApplyToViewModel(viewModel, item);
        }
    }
}
