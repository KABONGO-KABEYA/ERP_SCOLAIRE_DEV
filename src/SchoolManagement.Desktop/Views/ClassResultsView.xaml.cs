using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ClassResultsView : UserControl
{
    private const double ColRankWidth = 56;
    private const double ColMatriculeWidth = 120;
    private const double ColMatriculeMinWidth = 80;
    private const double ColNameWidth = 240;
    private const double ColNameMinWidth = 140;
    private const double ColCourseMinWidth = 72;
    private const double ColAverageWidth = 72;
    private const double ColPercentWidth = 64;
    private const double ColMentionWidth = 110;
    private const double ColDecisionWidth = 100;
    private const double ColStatusWidth = 90;
    private const double ColActionsWidth = 100;
    private const double HeaderHeight = 48;

    private static readonly SolidColorBrush NavyBrush = CreateFrozenBrush(0x0B, 0x1F, 0x47);
    private static readonly SolidColorBrush NavyBorderBrush = CreateFrozenBrush(0x1E, 0x3A, 0x5F);
    private static readonly SolidColorBrush AverageHeaderBrush = CreateFrozenBrush(0x16, 0x4E, 0x63);
    private static readonly SolidColorBrush AverageAccentBrush = CreateFrozenBrush(0xA7, 0xF3, 0xD0);
    private static readonly SolidColorBrush AverageCellBrush = CreateFrozenBrush(0xEC, 0xFD, 0xF5);

    private ClassResultsViewModel? _subscribedVm;
    private ScrollViewer? _bodyScroll;
    private bool _scrollSyncing;

    public ClassResultsView()
    {
        InitializeComponent();
    }

    private void ClassResultsView_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClassResultsViewModel vm)
        {
            _subscribedVm = vm;
            vm.ColumnsChanged += RebuildColumns;
            RebuildColumns();
        }
    }

    private void ClassResultsView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_bodyScroll is not null)
        {
            _bodyScroll.ScrollChanged -= BodyScroll_OnScrollChanged;
            _bodyScroll = null;
        }

        if (_subscribedVm is not null)
        {
            _subscribedVm.ColumnsChanged -= RebuildColumns;
            _subscribedVm = null;
        }
    }

    private void ResultsGrid_OnLoaded(object sender, RoutedEventArgs e) => AttachScrollSync();

    private void ResultsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ClassResultsViewModel vm
            || ResultsGrid.SelectedItem is not ClassResultRowVm row)
        {
            return;
        }

        if (vm.ConsultCommand.CanExecute(row))
        {
            vm.ConsultCommand.Execute(row);
        }
    }

    private void RebuildColumns()
    {
        if (DataContext is not ClassResultsViewModel vm)
        {
            return;
        }

        ResultsGrid.Columns.Clear();
        ClassResultsFrozenHeader.Children.Clear();
        ClassResultsFrozenHeader.ColumnDefinitions.Clear();
        ClassResultsFrozenHeader.RowDefinitions.Clear();
        ClassResultsScrollableHeader.Children.Clear();
        ClassResultsScrollableHeader.ColumnDefinitions.Clear();
        ClassResultsScrollableHeader.RowDefinitions.Clear();

        ResultsGrid.Columns.Add(CreateCenteredColumn(nameof(ClassResultRowVm.RankDisplay), ColRankWidth));

        var matriculeWidth = Math.Max(
            ColMatriculeWidth,
            MeasureContentColumnWidth(
                vm.FilteredRows.Select(r => r.RegistrationNumber),
                "Matricule",
                ColMatriculeMinWidth,
                280));
        ResultsGrid.Columns.Add(new DataGridTextColumn
        {
            Binding = new Binding(nameof(ClassResultRowVm.RegistrationNumber)),
            Width = new DataGridLength(matriculeWidth),
            MinWidth = ColMatriculeMinWidth,
            MaxWidth = 280,
            CanUserResize = true,
            IsReadOnly = true,
            ElementStyle = CreateLeftStyle()
        });

        var nameWidth = Math.Max(
            ColNameWidth,
            MeasureContentColumnWidth(
                vm.FilteredRows.Select(r => r.StudentName),
                "Nom de l'élève",
                ColNameMinWidth,
                520));
        ResultsGrid.Columns.Add(new DataGridTextColumn
        {
            Binding = new Binding(nameof(ClassResultRowVm.StudentName)),
            Width = new DataGridLength(nameWidth),
            MinWidth = ColNameMinWidth,
            MaxWidth = 520,
            CanUserResize = true,
            IsReadOnly = true,
            ElementStyle = CreateLeftStyle()
        });

        var courseWidths = new double[vm.CourseColumns.Count];
        for (var i = 0; i < vm.CourseColumns.Count; i++)
        {
            var name = vm.CourseColumns[i].CourseName;
            courseWidths[i] = Math.Max(
                ColCourseMinWidth,
                MeasureTextWidth(name.ToUpperInvariant(), 10, FontWeights.Bold) + 16);

            ResultsGrid.Columns.Add(new DataGridTextColumn
            {
                Binding = new Binding($"[{i}]") { Mode = BindingMode.OneWay },
                Width = new DataGridLength(courseWidths[i]),
                MinWidth = ColCourseMinWidth,
                IsReadOnly = true,
                ElementStyle = CreateCenteredStyle()
            });
        }

        var averageCol = CreateCenteredColumn(nameof(ClassResultRowVm.AverageDisplay), ColAverageWidth);
        averageCol.CellStyle = CreateAverageCellStyle();
        ResultsGrid.Columns.Add(averageCol);

        ResultsGrid.Columns.Add(CreateCenteredColumn(nameof(ClassResultRowVm.PercentageDisplay), ColPercentWidth));

        var mentionWidth = Math.Max(
            ColMentionWidth,
            MeasureContentColumnWidth(
                vm.FilteredRows.Select(r => r.Mention),
                "Mention",
                ColMentionWidth,
                280));
        ResultsGrid.Columns.Add(CreateCenteredColumn(nameof(ClassResultRowVm.Mention), mentionWidth));

        var decisionWidth = Math.Max(
            ColDecisionWidth,
            MeasureContentColumnWidth(
                vm.FilteredRows.Select(r => r.DecisionLabel),
                "Décision",
                ColDecisionWidth,
                220));
        ResultsGrid.Columns.Add(new DataGridTemplateColumn
        {
            Width = new DataGridLength(decisionWidth),
            CellTemplate = CreateDecisionTemplate()
        });

        var statusWidth = Math.Max(
            ColStatusWidth,
            MeasureContentColumnWidth(
                vm.FilteredRows.Select(r => r.StatusLabel),
                "Statut",
                ColStatusWidth,
                180));
        ResultsGrid.Columns.Add(CreateCenteredColumn(nameof(ClassResultRowVm.StatusLabel), statusWidth));
        ResultsGrid.Columns.Add(new DataGridTemplateColumn
        {
            Width = new DataGridLength(ColActionsWidth),
            CellTemplate = CreateConsultTemplate(vm)
        });

        BuildFrozenHeader(matriculeWidth, nameWidth);
        BuildScrollableHeader(vm, courseWidths, mentionWidth, decisionWidth, statusWidth);
        Dispatcher.BeginInvoke(AttachScrollSync, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void BuildFrozenHeader(double matriculeWidth, double nameWidth)
    {
        ClassResultsFrozenHeader.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });
        ClassResultsFrozenHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColRankWidth) });
        ClassResultsFrozenHeader.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(matriculeWidth),
            MinWidth = ColMatriculeMinWidth
        });
        ClassResultsFrozenHeader.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(nameWidth),
            MinWidth = ColNameMinWidth
        });

        AddFrozenHeaderCell("Rang", 0, HorizontalAlignment.Center, resizable: false);
        AddFrozenHeaderCell("Matricule", 1, HorizontalAlignment.Center, resizable: true);
        AddFrozenHeaderCell("Nom de l'élève", 2, HorizontalAlignment.Left, resizable: true);
    }

    private void AddFrozenHeaderCell(string text, int column, HorizontalAlignment align, bool resizable)
    {
        var border = new Border
        {
            Background = NavyBrush,
            BorderBrush = NavyBorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(4, 2, resizable ? 8 : 4, 2)
        };

        var label = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            FontSize = 10,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = align,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None
        };

        if (!resizable)
        {
            border.Child = label;
        }
        else
        {
            var host = new Grid();
            host.Children.Add(label);
            var thumb = new Thumb
            {
                Width = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.SizeWE,
                Background = Brushes.Transparent,
                Opacity = 0.01,
                ToolTip = "Glisser pour redimensionner"
            };
            thumb.DragDelta += (_, e) => ResizeFrozenColumn(column, e.HorizontalChange);
            host.Children.Add(thumb);
            border.Child = host;
        }

        Grid.SetColumn(border, column);
        ClassResultsFrozenHeader.Children.Add(border);
    }

    private void ResizeFrozenColumn(int columnIndex, double delta)
    {
        if (columnIndex < 0
            || columnIndex >= ResultsGrid.Columns.Count
            || columnIndex >= ClassResultsFrozenHeader.ColumnDefinitions.Count)
        {
            return;
        }

        var dataCol = ResultsGrid.Columns[columnIndex];
        var min = dataCol.MinWidth > 0 ? dataCol.MinWidth : 40;
        var max = dataCol.MaxWidth is > 0 and < double.PositiveInfinity ? dataCol.MaxWidth : 600;
        var current = dataCol.ActualWidth > 0 ? dataCol.ActualWidth : dataCol.Width.DisplayValue;
        var newWidth = Math.Clamp(current + delta, min, max);
        dataCol.Width = new DataGridLength(newWidth);
        ClassResultsFrozenHeader.ColumnDefinitions[columnIndex].Width = new GridLength(newWidth);
    }

    private void BuildScrollableHeader(
        ClassResultsViewModel vm,
        IReadOnlyList<double> courseWidths,
        double mentionWidth,
        double decisionWidth,
        double statusWidth)
    {
        ClassResultsScrollableHeader.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderHeight) });

        for (var i = 0; i < vm.CourseColumns.Count; i++)
        {
            ClassResultsScrollableHeader.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(courseWidths[i])
            });
            AddScrollableHeaderCell(
                vm.CourseColumns[i].CourseName.ToUpperInvariant(),
                i,
                NavyBrush,
                Brushes.White,
                FontWeights.Bold,
                vm.CourseColumns[i].CourseName);
        }

        var trailing = new (string Label, double Width, Brush Bg, Brush Fg)[]
        {
            ("Moyenne", ColAverageWidth, AverageHeaderBrush, AverageAccentBrush),
            ("%", ColPercentWidth, NavyBrush, Brushes.White),
            ("Mention", mentionWidth, NavyBrush, Brushes.White),
            ("Décision", decisionWidth, NavyBrush, Brushes.White),
            ("Statut", statusWidth, NavyBrush, Brushes.White),
            ("Actions", ColActionsWidth, NavyBrush, Brushes.White)
        };

        var col = vm.CourseColumns.Count;
        foreach (var item in trailing)
        {
            ClassResultsScrollableHeader.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(item.Width)
            });
            AddScrollableHeaderCell(item.Label, col, item.Bg, item.Fg, FontWeights.SemiBold, item.Label);
            col++;
        }
    }

    private double MeasureContentColumnWidth(
        IEnumerable<string?> values,
        string header,
        double minWidth,
        double maxWidth)
    {
        var longest = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .DefaultIfEmpty(header)
            .MaxBy(v => v.Length) ?? header;

        var content = MeasureTextWidth(longest, 12, FontWeights.Normal) + 20;
        var head = MeasureTextWidth(header, 10, FontWeights.SemiBold) + 16;
        return Math.Clamp(Math.Max(content, head), minWidth, maxWidth);
    }

    private void AddScrollableHeaderCell(
        string text,
        int column,
        Brush background,
        Brush foreground,
        FontWeight weight,
        string? toolTip)
    {
        var border = new Border
        {
            Background = background,
            BorderBrush = NavyBorderBrush,
            BorderThickness = new Thickness(0.5, 0, 0.5, 1),
            Padding = new Thickness(4, 2, 4, 2)
        };
        border.Child = new TextBlock
        {
            Text = text,
            FontWeight = weight,
            FontSize = 10,
            Foreground = foreground,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None,
            ToolTip = toolTip
        };
        Grid.SetColumn(border, column);
        ClassResultsScrollableHeader.Children.Add(border);
    }

    private void AttachScrollSync()
    {
        if (_bodyScroll is not null)
        {
            _bodyScroll.ScrollChanged -= BodyScroll_OnScrollChanged;
        }

        _bodyScroll = GetVisualChild<ScrollViewer>(ResultsGrid);
        if (_bodyScroll is null)
        {
            return;
        }

        _bodyScroll.ScrollChanged -= BodyScroll_OnScrollChanged;
        _bodyScroll.ScrollChanged += BodyScroll_OnScrollChanged;
    }

    private void BodyScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollSyncing || Math.Abs(e.HorizontalChange) < 0.01)
        {
            return;
        }

        _scrollSyncing = true;
        try
        {
            ClassResultsHeaderScroll.ScrollToHorizontalOffset(_bodyScroll!.HorizontalOffset);
        }
        finally
        {
            _scrollSyncing = false;
        }
    }

    private static DataGridTextColumn CreateCenteredColumn(string path, double width) =>
        new()
        {
            Binding = new Binding(path),
            Width = new DataGridLength(width),
            MinWidth = Math.Min(width, 40),
            IsReadOnly = true,
            ElementStyle = CreateCenteredStyle()
        };

    private static Style CreateCenteredStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.None));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0)));
        return style;
    }

    private static Style CreateLeftStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 0, 6, 0)));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.None));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        return style;
    }

    private static Style CreateAverageCellStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, AverageCellBrush));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static DataTemplate CreateDecisionTemplate()
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new Binding(nameof(ClassResultRowVm.DecisionLabel)));
        factory.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(ClassResultRowVm.DecisionBrush)));
        factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        factory.SetValue(TextBlock.FontSizeProperty, 11.0);
        factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        template.VisualTree = factory;
        return template;
    }

    private static DataTemplate CreateConsultTemplate(ClassResultsViewModel vm)
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(ContentControl.ContentProperty, "Consulter");
        factory.SetValue(Control.FontSizeProperty, 11.0);
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(Control.PaddingProperty, new Thickness(8, 2, 8, 2));
        factory.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        factory.SetBinding(ButtonBase.CommandProperty, new Binding(nameof(ClassResultsViewModel.ConsultCommand))
        {
            Source = vm
        });
        factory.SetBinding(ButtonBase.CommandParameterProperty, new Binding());
        template.VisualTree = factory;
        return template;
    }

    private static double MeasureTextWidth(string text, double fontSize, FontWeight weight)
    {
        var dpi = VisualTreeHelper.GetDpi(System.Windows.Application.Current?.MainWindow ?? new Window()).PixelsPerDip;
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize,
            Brushes.Black,
            dpi);
        return formatted.WidthIncludingTrailingWhitespace;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
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
