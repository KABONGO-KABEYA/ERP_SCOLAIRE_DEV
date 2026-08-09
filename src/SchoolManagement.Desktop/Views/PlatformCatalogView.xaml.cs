using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class PlatformCatalogView : UserControl
{
    public PlatformCatalogView() => InitializeComponent();

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is PlatformCatalogViewModel vm)
        {
            vm.SelectedTreeNode = e.NewValue as CatalogTreeNodeViewModel;
        }
    }
}
