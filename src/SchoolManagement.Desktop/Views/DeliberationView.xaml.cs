using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class DeliberationView
{
    public DeliberationView()
    {
        InitializeComponent();
    }

    private void Rows_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DeliberationViewModel vm)
        {
            return;
        }

        if (sender is not DataGrid grid || grid.SelectedItem is not DeliberationRowVm row)
        {
            return;
        }

        if (vm.OpenDecisionCommand.CanExecute(row))
        {
            vm.OpenDecisionCommand.Execute(row);
        }
    }
}
