using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class FinanceHubView : UserControl
{
    public FinanceHubView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FinanceNavigationBridge.SectionSelected += OnFinanceSectionSelected;
        if (FinanceNavigationBridge.CurrentSelection is { } item && DataContext is FinanceHubViewModel vm)
        {
            FinanceNavigationBridge.ApplyToViewModel(vm, item);
        }
        else if (DataContext is FinanceHubViewModel hub && string.IsNullOrWhiteSpace(hub.ActiveNavKey))
        {
            var defaultItem = FinanceNavCatalog.DefaultItem;
            FinanceNavigationBridge.ApplyToViewModel(hub, defaultItem);
            FinanceNavigationBridge.Select(defaultItem);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        FinanceNavigationBridge.SectionSelected -= OnFinanceSectionSelected;
    }

    private void OnFinanceSectionSelected(FinanceNavItem item)
    {
        if (DataContext is FinanceHubViewModel viewModel)
        {
            FinanceNavigationBridge.ApplyToViewModel(viewModel, item);
        }
    }
}
