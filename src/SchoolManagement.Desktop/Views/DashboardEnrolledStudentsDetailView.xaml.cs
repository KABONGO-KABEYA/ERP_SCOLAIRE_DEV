using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class DashboardEnrolledStudentsDetailView : UserControl
{
    public DashboardEnrolledStudentsDetailView() => InitializeComponent();

    private async void ClassExpander_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander || expander.DataContext is not DashboardEnrolledClassRow row)
        {
            return;
        }

        if (DataContext is not DashboardEnrolledStudentsDetailViewModel vm)
        {
            return;
        }

        await vm.LoadClassStudentsCommand.ExecuteAsync(row);
    }
}
