using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using SchoolManagement.Desktop.ViewModels;



namespace SchoolManagement.Desktop.Views;



public partial class GradesView : UserControl

{

    public GradesView()

    {

        InitializeComponent();

        Loaded += (_, _) =>

        {

            if (DataContext is GradesViewModel viewModel)

            {

                viewModel.PropertyChanged += (_, args) =>

                {

                    if (args.PropertyName == nameof(GradesViewModel.CanEditGrades))

                    {

                        GradingGrid.IsReadOnly = !viewModel.CanEditGrades;

                    }

                };

                GradingGrid.IsReadOnly = !viewModel.CanEditGrades;

            }

        };

    }



    private void GradingGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)

    {

        if (sender is not DataGrid grid || grid.SelectedItem is not GradeEntryEditItem current)

        {

            return;

        }



        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)

        {

            var currentIndex = grid.Items.IndexOf(current);

            if (currentIndex >= 0 && currentIndex < grid.Items.Count - 1)

            {

                grid.SelectedIndex = currentIndex + 1;

                grid.ScrollIntoView(grid.SelectedItem);

                grid.CurrentCell = new DataGridCellInfo(grid.SelectedItem, grid.Columns[3]);

                grid.BeginEdit();

                e.Handled = true;

            }

        }



        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && DataContext is GradesViewModel vm)

        {

            if (vm.SaveGradesCommand.CanExecute(null))

            {

                _ = vm.SaveGradesCommand.ExecuteAsync(null);

                e.Handled = true;

            }

        }

    }

}


