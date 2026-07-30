using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class PersonnelHubView : UserControl
{
    public PersonnelHubView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PersonnelNavigationBridge.SectionSelected += OnPersonnelSectionSelected;
        if (PersonnelNavigationBridge.CurrentSelection is { } item && DataContext is PersonnelHubViewModel vm)
        {
            PersonnelNavigationBridge.ApplyToViewModel(vm, item);
        }
        else if (DataContext is PersonnelHubViewModel hub && string.IsNullOrWhiteSpace(hub.ActiveNavKey))
        {
            var defaultItem = PersonnelNavCatalog.DefaultItem;
            PersonnelNavigationBridge.ApplyToViewModel(hub, defaultItem);
            PersonnelNavigationBridge.Select(defaultItem);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PersonnelNavigationBridge.SectionSelected -= OnPersonnelSectionSelected;
    }

    private void OnPersonnelSectionSelected(PersonnelNavItem item)
    {
        if (DataContext is PersonnelHubViewModel viewModel)
        {
            PersonnelNavigationBridge.ApplyToViewModel(viewModel, item);
        }
    }
}
