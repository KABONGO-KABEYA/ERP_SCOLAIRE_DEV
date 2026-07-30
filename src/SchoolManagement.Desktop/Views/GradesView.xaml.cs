using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class GradesView : UserControl
{
    public GradesView()
    {
        InitializeComponent();
    }

    private void TeacherPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is GradesViewModel vm && sender is PasswordBox box)
        {
            vm.TeacherPassword = box.Password;
        }
    }

    private void GradingGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && DataContext is GradesViewModel saveVm)
        {
            if (saveVm.SaveGradesCommand.CanExecute(null))
            {
                _ = saveVm.SaveGradesCommand.ExecuteAsync(null);
                e.Handled = true;
            }

            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (DataContext is not GradesViewModel { CanEditGrades: true })
        {
            return;
        }

        if (Keyboard.FocusedElement is not TextBox { DataContext: GradeEntryEditItem current })
        {
            return;
        }

        if (sender is not DataGrid grid)
        {
            return;
        }

        var currentIndex = grid.Items.IndexOf(current);
        if (currentIndex < 0 || currentIndex >= grid.Items.Count - 1)
        {
            return;
        }

        grid.ScrollIntoView(grid.Items[currentIndex + 1]);

        if (grid.ItemContainerGenerator.ContainerFromIndex(currentIndex + 1) is DataGridRow nextRow)
        {
            var nextScoreBox = FindScoreTextBox(nextRow);
            nextScoreBox?.Focus();
            nextScoreBox?.SelectAll();
        }

        e.Handled = true;
    }

    private static TextBox? FindScoreTextBox(DataGridRow row)
    {
        if (GetVisualChild<DataGridCellsPresenter>(row) is not { } presenter)
        {
            return null;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(presenter); i++)
        {
            if (VisualTreeHelper.GetChild(presenter, i) is not DataGridCell { Column: { DisplayIndex: 3 } } cell)
            {
                continue;
            }

            return GetVisualChild<TextBox>(cell);
        }

        return null;
    }

    private static T? GetVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var nested = GetVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
