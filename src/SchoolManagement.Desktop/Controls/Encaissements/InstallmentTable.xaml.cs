using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.Models;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Controls.Encaissements;

public partial class InstallmentTable
{
    public InstallmentTable()
    {
        InitializeComponent();
    }

    private void TodayPaymentBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not CollectPaymentViewModel vm)
        {
            return;
        }

        if (sender is not TextBox { DataContext: InstallmentCollectRow row })
        {
            return;
        }

        vm.OnTodayPaymentTextChanged(row);
    }

    private void TodayPaymentBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectPaymentViewModel vm)
        {
            return;
        }

        if (sender is not TextBox { DataContext: InstallmentCollectRow row })
        {
            return;
        }

        vm.OnTodayPaymentLostFocus(row);
    }
}
