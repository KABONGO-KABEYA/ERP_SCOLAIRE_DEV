using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class DeliberationWorkspaceView
{
    private DeliberationWorkspaceViewModel? _subscribedVm;

    public DeliberationWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.Session.SheetChanged -= OnSheetChanged;
            _subscribedVm = null;
        }

        if (e.NewValue is DeliberationWorkspaceViewModel vm)
        {
            _subscribedVm = vm;
            vm.Session.SheetChanged += OnSheetChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.Session.SheetChanged -= OnSheetChanged;
            _subscribedVm = null;
        }
    }

    private void OnSheetChanged() =>
        Dispatcher.BeginInvoke(FitColumnsToContent, System.Windows.Threading.DispatcherPriority.Loaded);

    private void ResultsGrid_OnLoaded(object sender, RoutedEventArgs e) => FitColumnsToContent();

    private void FitColumnsToContent()
    {
        if (ResultsGrid.Items.Count == 0)
        {
            return;
        }

        ResultsGrid.UpdateLayout();

        // Colonnes texte : adapter à l'en-tête + contenu (jamais tronqué).
        foreach (var column in ResultsGrid.Columns)
        {
            if (column is DataGridTextColumn && column.Width.IsStar)
            {
                continue; // Observation reste flexible
            }

            if (column is DataGridTextColumn or DataGridTemplateColumn)
            {
                column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToHeader);
                ResultsGrid.UpdateLayout();
                var headerWidth = column.ActualWidth;

                column.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
                ResultsGrid.UpdateLayout();
                var cellsWidth = column.ActualWidth;

                var fitted = Math.Max(headerWidth, cellsWidth);
                fitted = Math.Max(fitted, column.MinWidth > 0 ? column.MinWidth : fitted);
                // Marge interne pour padding / ComboBox
                fitted += column is DataGridTemplateColumn ? 28 : 12;
                column.Width = new DataGridLength(fitted);
            }
        }

        // Conduite / Décision : largeur selon le plus long libellé des options.
        if (DataContext is DeliberationWorkspaceViewModel vm)
        {
            FitComboColumn(ConductColumn, vm.Session.ConductOptions.Select(c => c.Label));
            if (vm.ShowDecisionColumn)
            {
                FitComboColumn(DecisionColumn, vm.Session.AvailableDecisions.Select(d => d.Label));
            }
        }
    }

    private void FitComboColumn(DataGridTemplateColumn column, IEnumerable<string> labels)
    {
        var longest = labels
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .DefaultIfEmpty(column.Header?.ToString() ?? string.Empty)
            .MaxBy(l => l.Length) ?? string.Empty;

        var measured = MeasureTextWidth(longest, 12, FontWeights.Normal) + 48;
        var header = MeasureTextWidth(column.Header?.ToString() ?? string.Empty, 10, FontWeights.SemiBold) + 24;
        var width = Math.Max(Math.Max(measured, header), column.MinWidth > 0 ? column.MinWidth : 130);
        column.Width = new DataGridLength(width);
    }

    private double MeasureTextWidth(string text, double fontSize, FontWeight weight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var formatted = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily("Segoe UI"),
                FontStyles.Normal,
                weight,
                FontStretches.Normal),
            fontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(ResultsGrid).PixelsPerDip);

        return formatted.Width;
    }

    private void ResultsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not DeliberationWorkspaceViewModel vm)
        {
            return;
        }

        if (!vm.Session.CanSetFinalDecision)
        {
            return;
        }

        if (vm.OpenSelectedDecisionCommand.CanExecute(null))
        {
            vm.OpenSelectedDecisionCommand.Execute(null);
        }
    }
}
