using System.Windows.Controls;
using System.Windows.Input;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ExpensePaymentsView : UserControl
{
    public ExpensePaymentsView()
    {
        InitializeComponent();
    }

    private void AttachmentDropZone_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ExpensePaymentsViewModel vm && vm.BrowseAttachmentCommand.CanExecute(null))
            vm.BrowseAttachmentCommand.Execute(null);
    }
}
