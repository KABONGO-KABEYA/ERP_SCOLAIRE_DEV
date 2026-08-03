using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class GradesView : UserControl
{
    private GradesViewModel? _subscribedVm;

    public GradesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => UnsubscribeVm();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeVm();
        if (e.NewValue is GradesViewModel vm)
        {
            _subscribedVm = vm;
            vm.GlobalColumnsChanged += RebuildGlobalCotationColumns;
            vm.CourseNotesColumnsChanged += RebuildCourseNotesColumns;
            vm.PedagogicalSheetColumnsChanged += RebuildPedagogicalSheetColumns;
        }
    }

    private void UnsubscribeVm()
    {
        if (_subscribedVm is null)
        {
            return;
        }

        _subscribedVm.GlobalColumnsChanged -= RebuildGlobalCotationColumns;
        _subscribedVm.CourseNotesColumnsChanged -= RebuildCourseNotesColumns;
        _subscribedVm.PedagogicalSheetColumnsChanged -= RebuildPedagogicalSheetColumns;
        _subscribedVm = null;
    }

    private void TeacherPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is GradesViewModel vm && sender is PasswordBox box)
        {
            vm.TeacherPassword = box.Password;
        }
    }

    private const double PedagogicalColNumberWidth = 40;
    private const double PedagogicalColMatriculeWidth = 120;
    private const double PedagogicalColMatriculeMinWidth = 80;
    private const double PedagogicalColNameWidth = 240;
    private const double PedagogicalColNameMinWidth = 140;
    private const double PedagogicalColNoteMinWidth = 48;
    private const double PedagogicalColTotalMinWidth = 56;
    private const double PedagogicalColAverageMinWidth = 64;
    private const double PedagogicalHeaderCourseHeight = 24;
    private const double PedagogicalHeaderLeafHeight = 24;

    private ScrollViewer? _pedagogicalBodyScroll;
    private bool _pedagogicalScrollSyncing;

    private void PedagogicalSheetGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachPedagogicalSheetScrollSync();
    }

    private void RebuildPedagogicalSheetColumns()
    {
        if (DataContext is not GradesViewModel vm)
        {
            return;
        }

        PedagogicalSheetGrid.Columns.Clear();
        PedagogicalSheetFrozenHeader.Children.Clear();
        PedagogicalSheetFrozenHeader.ColumnDefinitions.Clear();
        PedagogicalSheetFrozenHeader.RowDefinitions.Clear();
        PedagogicalSheetScrollableHeader.Children.Clear();
        PedagogicalSheetScrollableHeader.ColumnDefinitions.Clear();
        PedagogicalSheetScrollableHeader.RowDefinitions.Clear();

        PedagogicalSheetGrid.Columns.Add(CreatePedagogicalTextColumn(
            nameof(PedagogicalSheetRowVm.RowNumber), PedagogicalColNumberWidth));
        PedagogicalSheetGrid.Columns.Add(CreatePedagogicalTextColumn(
            nameof(PedagogicalSheetRowVm.RegistrationNumber),
            PedagogicalColMatriculeWidth,
            PedagogicalColMatriculeMinWidth));
        PedagogicalSheetGrid.Columns.Add(new DataGridTextColumn
        {
            Binding = new Binding(nameof(PedagogicalSheetRowVm.StudentName)),
            Width = new DataGridLength(PedagogicalColNameWidth),
            MinWidth = PedagogicalColNameMinWidth,
            MaxWidth = 480,
            CanUserResize = true,
            IsReadOnly = true,
            ElementStyle = CreateLeftReadOnlyStyle()
        });
        // Matricule redimensionnable
        PedagogicalSheetGrid.Columns[1].CanUserResize = true;
        PedagogicalSheetGrid.Columns[1].MaxWidth = 280;

        var leafWidths = ComputePedagogicalLeafWidths(vm.PedagogicalSheetLeafColumns);

        for (var i = 0; i < vm.PedagogicalSheetLeafColumns.Count; i++)
        {
            var leaf = vm.PedagogicalSheetLeafColumns[i];
            var col = CreatePedagogicalTextColumn($"LeafDisplays[{i}]", leafWidths[i]);
            col.CellStyle = leaf.Kind switch
            {
                PedagogicalSheetLeafKind.Total => CreateTotalCellStyle(),
                PedagogicalSheetLeafKind.Average => CreateAverageCellStyle(),
                _ => null
            };
            PedagogicalSheetGrid.Columns.Add(col);
        }

        BuildPedagogicalFrozenHeader();
        BuildPedagogicalScrollableHeader(vm, leafWidths);
        Dispatcher.BeginInvoke(AttachPedagogicalSheetScrollSync, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static IReadOnlyList<double> ComputePedagogicalLeafWidths(
        IReadOnlyList<PedagogicalSheetLeafColumnVm> leaves)
    {
        if (leaves.Count == 0)
        {
            return [];
        }

        var widths = new double[leaves.Count];
        var index = 0;
        while (index < leaves.Count)
        {
            var start = index;
            var courseName = leaves[index].CourseName;
            while (index < leaves.Count && leaves[index].CourseId == leaves[start].CourseId)
            {
                index++;
            }

            var span = index - start;
            var baseSum = 0.0;
            for (var i = start; i < index; i++)
            {
                var baseWidth = leaves[i].Kind switch
                {
                    PedagogicalSheetLeafKind.Total => PedagogicalColTotalMinWidth,
                    PedagogicalSheetLeafKind.Average => PedagogicalColAverageMinWidth,
                    _ => PedagogicalColNoteMinWidth
                };
                widths[i] = baseWidth;
                baseSum += baseWidth;
            }

            var nameNeed = MeasurePedagogicalTextWidth(courseName.ToUpperInvariant(), 10, FontWeights.Bold) + 16;
            if (nameNeed > baseSum && span > 0)
            {
                var extra = (nameNeed - baseSum) / span;
                for (var i = start; i < index; i++)
                {
                    widths[i] += extra;
                }
            }
        }

        return widths;
    }

    private static double MeasurePedagogicalTextWidth(string text, double fontSize, FontWeight weight)
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

    private static DataGridTextColumn CreatePedagogicalTextColumn(string bindingPath, double width, double? minWidth = null) =>
        new()
        {
            Binding = new Binding(bindingPath),
            Width = new DataGridLength(width),
            MinWidth = minWidth ?? Math.Min(width, 40),
            IsReadOnly = true,
            ElementStyle = CreateCenteredReadOnlyStyle()
        };

    private void BuildPedagogicalFrozenHeader()
    {
        var totalHeight = PedagogicalHeaderCourseHeight + PedagogicalHeaderLeafHeight;
        PedagogicalSheetFrozenHeader.RowDefinitions.Add(new RowDefinition { Height = new GridLength(totalHeight) });
        PedagogicalSheetFrozenHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PedagogicalColNumberWidth) });
        PedagogicalSheetFrozenHeader.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(PedagogicalColMatriculeWidth),
            MinWidth = PedagogicalColMatriculeMinWidth
        });
        PedagogicalSheetFrozenHeader.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(PedagogicalColNameWidth),
            MinWidth = PedagogicalColNameMinWidth
        });

        AddFrozenHeaderCell("N°", 0, resizable: false);
        AddFrozenHeaderCell("Matricule", 1, resizable: true);
        AddFrozenHeaderCell("Nom de l'élève", 2, resizable: true);
    }

    private void AddFrozenHeaderCell(string text, int column, bool resizable)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x1F, 0x47)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
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
            HorizontalAlignment = column == 2 ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
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
            thumb.DragDelta += (_, e) => ResizePedagogicalFrozenColumn(column, e.HorizontalChange);
            host.Children.Add(thumb);
            border.Child = host;
        }

        Grid.SetColumn(border, column);
        PedagogicalSheetFrozenHeader.Children.Add(border);
    }

    private void ResizePedagogicalFrozenColumn(int columnIndex, double delta)
    {
        if (columnIndex < 0
            || columnIndex >= PedagogicalSheetGrid.Columns.Count
            || columnIndex >= PedagogicalSheetFrozenHeader.ColumnDefinitions.Count)
        {
            return;
        }

        var dataCol = PedagogicalSheetGrid.Columns[columnIndex];
        var min = dataCol.MinWidth > 0 ? dataCol.MinWidth : 40;
        var max = dataCol.MaxWidth is > 0 and < double.PositiveInfinity
            ? dataCol.MaxWidth
            : 600;
        var current = dataCol.ActualWidth > 0 ? dataCol.ActualWidth : dataCol.Width.DisplayValue;
        var newWidth = Math.Clamp(current + delta, min, max);

        dataCol.Width = new DataGridLength(newWidth);
        PedagogicalSheetFrozenHeader.ColumnDefinitions[columnIndex].Width = new GridLength(newWidth);
    }

    private void BuildPedagogicalScrollableHeader(GradesViewModel vm, IReadOnlyList<double> leafWidths)
    {
        var leaves = vm.PedagogicalSheetLeafColumns;
        if (leaves.Count == 0)
        {
            return;
        }

        PedagogicalSheetScrollableHeader.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PedagogicalHeaderCourseHeight) });
        PedagogicalSheetScrollableHeader.RowDefinitions.Add(new RowDefinition { Height = new GridLength(PedagogicalHeaderLeafHeight) });

        for (var i = 0; i < leaves.Count; i++)
        {
            PedagogicalSheetScrollableHeader.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(leafWidths[i])
            });
        }

        var index = 0;
        while (index < leaves.Count)
        {
            var start = index;
            var courseId = leaves[index].CourseId;
            var courseName = leaves[index].CourseName;
            while (index < leaves.Count && leaves[index].CourseId == courseId)
            {
                index++;
            }

            var span = index - start;
            var courseBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x1F, 0x47)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
                BorderThickness = new Thickness(2, 0, 2, 1),
                Padding = new Thickness(4, 1, 4, 1),
                Margin = new Thickness(start == 0 ? 0 : 1, 0, 0, 0)
            };
            courseBorder.Child = new TextBlock
            {
                Text = courseName.ToUpperInvariant(),
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.None,
                ToolTip = courseName
            };
            Grid.SetRow(courseBorder, 0);
            Grid.SetColumn(courseBorder, start);
            Grid.SetColumnSpan(courseBorder, span);
            PedagogicalSheetScrollableHeader.Children.Add(courseBorder);
        }

        for (var i = 0; i < leaves.Count; i++)
        {
            var leaf = leaves[i];
            var isGroupStart = leaf.IsFirstInGroup;
            var isGroupEnd = leaf.IsAverage;
            var leftBorder = isGroupStart ? 2.0 : 0.5;
            var rightBorder = isGroupEnd ? 2.0 : 0.5;

            var label = string.IsNullOrEmpty(leaf.MaxLabel)
                ? leaf.ShortLabel
                : $"{leaf.ShortLabel} {leaf.MaxLabel}";

            var bg = leaf.Kind switch
            {
                PedagogicalSheetLeafKind.Total => Color.FromRgb(0x1E, 0x3A, 0x5F),
                PedagogicalSheetLeafKind.Average => Color.FromRgb(0x16, 0x4E, 0x63),
                _ => Color.FromRgb(0x12, 0x2A, 0x52)
            };

            var evalBorder = new Border
            {
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)),
                BorderThickness = new Thickness(leftBorder, 0, rightBorder, 1),
                Padding = new Thickness(1, 1, 1, 1)
            };
            evalBorder.Child = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
                FontSize = 10,
                Foreground = leaf.Kind switch
                {
                    PedagogicalSheetLeafKind.Total => new SolidColorBrush(Color.FromRgb(0xFD, 0xE6, 0x8A)),
                    PedagogicalSheetLeafKind.Average => new SolidColorBrush(Color.FromRgb(0xA7, 0xF3, 0xD0)),
                    _ => Brushes.White
                },
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = string.IsNullOrEmpty(leaf.MaxLabel)
                    ? leaf.EvaluationTitle
                    : $"{leaf.EvaluationTitle} {leaf.MaxLabel}"
            };
            Grid.SetRow(evalBorder, 1);
            Grid.SetColumn(evalBorder, i);
            PedagogicalSheetScrollableHeader.Children.Add(evalBorder);
        }
    }

    private static Style CreateTotalCellStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED))));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style CreateAverageCellStyle()
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5))));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private void AttachPedagogicalSheetScrollSync()
    {
        if (_pedagogicalBodyScroll is not null)
        {
            _pedagogicalBodyScroll.ScrollChanged -= PedagogicalBodyScroll_OnScrollChanged;
        }

        _pedagogicalBodyScroll = GetVisualChild<ScrollViewer>(PedagogicalSheetGrid);
        if (_pedagogicalBodyScroll is null)
        {
            return;
        }

        _pedagogicalBodyScroll.ScrollChanged -= PedagogicalBodyScroll_OnScrollChanged;
        _pedagogicalBodyScroll.ScrollChanged += PedagogicalBodyScroll_OnScrollChanged;
    }

    private void PedagogicalBodyScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_pedagogicalScrollSyncing || Math.Abs(e.HorizontalChange) < 0.01)
        {
            return;
        }

        _pedagogicalScrollSyncing = true;
        try
        {
            PedagogicalSheetHeaderScroll.ScrollToHorizontalOffset(_pedagogicalBodyScroll!.HorizontalOffset);
        }
        finally
        {
            _pedagogicalScrollSyncing = false;
        }
    }

    private void RebuildCourseNotesColumns()
    {
        if (DataContext is not GradesViewModel vm)
        {
            return;
        }

        CourseNotesGrid.Columns.Clear();

        CourseNotesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "N°",
            Binding = new Binding(nameof(CourseNotesRowVm.RowNumber)),
            Width = new DataGridLength(50),
            IsReadOnly = true
        });
        CourseNotesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Matricule",
            Binding = new Binding(nameof(CourseNotesRowVm.RegistrationNumber)),
            Width = new DataGridLength(110),
            IsReadOnly = true
        });
        CourseNotesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Nom de l'élève",
            Binding = new Binding(nameof(CourseNotesRowVm.StudentName)),
            Width = new DataGridLength(180),
            IsReadOnly = true,
            MinWidth = 140
        });

        for (var i = 0; i < vm.CourseNotesColumns.Count; i++)
        {
            var col = vm.CourseNotesColumns[i];
            CourseNotesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = col.Header,
                Binding = new Binding($"Cells[{i}].Display"),
                Width = new DataGridLength(110),
                MinWidth = 80,
                IsReadOnly = true,
                ElementStyle = CreateCenteredReadOnlyStyle()
            });
        }
    }

    private static Style CreateCenteredReadOnlyStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style CreateLeftReadOnlyStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 0, 6, 0)));
        return style;
    }

    private void RebuildGlobalCotationColumns()
    {
        if (DataContext is not GradesViewModel vm)
        {
            return;
        }

        GlobalCotationGrid.Columns.Clear();

        GlobalCotationGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "N°",
            Binding = new Binding(nameof(GlobalCotationStudentRow.RowNumber)),
            Width = new DataGridLength(44),
            MinWidth = 40,
            CanUserResize = false,
            IsReadOnly = true,
            ElementStyle = CreateCenteredReadOnlyStyle()
        });
        GlobalCotationGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Matricule",
            Binding = new Binding(nameof(GlobalCotationStudentRow.RegistrationNumber)),
            Width = new DataGridLength(140),
            MinWidth = 100,
            MaxWidth = 260,
            CanUserResize = true,
            IsReadOnly = true,
            ElementStyle = CreateLeftReadOnlyStyle()
        });
        GlobalCotationGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Nom de l'élève",
            Binding = new Binding(nameof(GlobalCotationStudentRow.StudentName)),
            Width = new DataGridLength(220),
            MinWidth = 160,
            MaxWidth = 420,
            CanUserResize = true,
            IsReadOnly = true,
            ElementStyle = CreateLeftReadOnlyStyle()
        });

        for (var i = 0; i < vm.GlobalCourseColumns.Count; i++)
        {
            var colIndex = i;
            var courseCol = vm.GlobalCourseColumns[i];
            var courseWidth = Math.Max(
                92,
                MeasurePedagogicalTextWidth(courseCol.CourseName, 11, FontWeights.SemiBold) + 20);

            var header = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(6, 6, 6, 4),
                ToolTip = $"{courseCol.CourseName} — maximum de session"
            };
            var headerStack = new StackPanel();
            headerStack.Children.Add(new TextBlock
            {
                Text = courseCol.CourseName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0B, 0x1F, 0x47)),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = courseCol.CourseName
            });

            var maxRow = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var maxPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            maxPanel.Children.Add(new TextBlock
            {
                Text = "/",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B)),
                Margin = new Thickness(0, 0, 2, 0)
            });
            var maxBox = new TextBox
            {
                Width = 36,
                Height = 22,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                CaretBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                ToolTip = "Maximum de cette session (ne modifie pas le paramétrage du cours)"
            };
            maxBox.SetBinding(TextBox.TextProperty, new Binding(nameof(GlobalCotationCourseColumn.MaxScore))
            {
                Source = courseCol,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            maxPanel.Children.Add(maxBox);
            maxRow.Child = maxPanel;
            headerStack.Children.Add(maxRow);
            header.Child = headerStack;

            var cellFactory = new FrameworkElementFactory(typeof(Border));
            cellFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            cellFactory.SetValue(Border.MarginProperty, new Thickness(4, 3, 4, 3));
            cellFactory.SetValue(Border.PaddingProperty, new Thickness(2));
            cellFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            cellFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)));

            var bgBinding = new MultiBinding
            {
                Converter = new GlobalCellBackgroundConverter()
            };
            bgBinding.Bindings.Add(new Binding($"Cells[{colIndex}].IsInvalid"));
            bgBinding.Bindings.Add(new Binding($"Cells[{colIndex}].IsValid"));
            cellFactory.SetBinding(Border.BackgroundProperty, bgBinding);

            var textFactory = new FrameworkElementFactory(typeof(TextBox));
            textFactory.SetBinding(TextBox.TextProperty, new Binding($"Cells[{colIndex}].ScoreText")
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
            textFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding($"Cells[{colIndex}].ValidationMessage"));
            textFactory.SetValue(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBox.BorderThicknessProperty, new Thickness(0));
            textFactory.SetValue(TextBox.BackgroundProperty, Brushes.Transparent);
            textFactory.SetValue(TextBox.PaddingProperty, new Thickness(2, 0, 2, 0));
            textFactory.SetValue(TextBox.FontSizeProperty, 12.0);
            textFactory.SetValue(TextBox.FontWeightProperty, FontWeights.SemiBold);
            cellFactory.AppendChild(textFactory);

            GlobalCotationGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = header,
                Width = new DataGridLength(courseWidth),
                MinWidth = 88,
                CanUserResize = true,
                CellTemplate = new DataTemplate { VisualTree = cellFactory }
            });
        }
    }

    private void GlobalCotationGrid_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || DataContext is not GradesViewModel vm)
        {
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (vm.SaveGlobalCotationCommand.CanExecute(null))
            {
                _ = vm.SaveGlobalCotationCommand.ExecuteAsync(null);
                e.Handled = true;
            }

            return;
        }

        var shift = Keyboard.Modifiers == ModifierKeys.Shift;
        var isNavKey = e.Key is Key.Enter or Key.Tab
            or Key.Up or Key.Down or Key.Left or Key.Right;
        if (!isNavKey)
        {
            return;
        }

        if (e.Key is Key.Left or Key.Right
            && Keyboard.FocusedElement is TextBox { IsFocused: true } focused
            && !ShouldLeaveTextBox(focused, e.Key))
        {
            return;
        }

        if (grid.CurrentCell.Item is not GlobalCotationStudentRow)
        {
            return;
        }

        var rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
        var colIndex = grid.CurrentCell.Column?.DisplayIndex ?? -1;
        if (rowIndex < 0 || colIndex < 0)
        {
            return;
        }

        var nextRow = rowIndex;
        var nextCol = colIndex;
        const int firstCourseCol = 3;

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Down:
                nextRow++;
                if (nextRow >= grid.Items.Count)
                {
                    if (e.Key == Key.Enter)
                    {
                        nextRow = 0;
                        nextCol++;
                    }
                    else
                    {
                        return;
                    }
                }

                break;
            case Key.Up:
                nextRow--;
                if (nextRow < 0)
                {
                    return;
                }

                break;
            case Key.Tab when shift:
            case Key.Left:
                nextCol--;
                if (nextCol < firstCourseCol)
                {
                    nextCol = grid.Columns.Count - 1;
                    nextRow--;
                }

                break;
            case Key.Tab:
            case Key.Right:
                nextCol++;
                if (nextCol >= grid.Columns.Count)
                {
                    nextCol = firstCourseCol;
                    nextRow++;
                }

                break;
        }

        if (nextCol < firstCourseCol)
        {
            nextCol = firstCourseCol;
        }

        if (nextRow < 0 || nextRow >= grid.Items.Count || nextCol >= grid.Columns.Count)
        {
            return;
        }

        MoveGlobalGridFocus(grid, nextRow, nextCol);
        e.Handled = true;
    }

    private static bool ShouldLeaveTextBox(TextBox box, Key key)
    {
        if (box.Text.Length == 0)
        {
            return true;
        }

        return key switch
        {
            Key.Left => box.CaretIndex <= 0 && box.SelectionLength == 0,
            Key.Right => box.CaretIndex >= box.Text.Length && box.SelectionLength == 0,
            _ => true
        };
    }

    private static void MoveGlobalGridFocus(DataGrid grid, int rowIndex, int colIndex)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);
        grid.SelectedCells.Clear();
        grid.CurrentCell = new DataGridCellInfo(grid.Items[rowIndex], grid.Columns[colIndex]);
        grid.ScrollIntoView(grid.Items[rowIndex], grid.Columns[colIndex]);
        grid.BeginEdit();

        grid.Dispatcher.BeginInvoke(() =>
        {
            if (grid.ItemContainerGenerator.ContainerFromIndex(rowIndex) is not DataGridRow row)
            {
                return;
            }

            var cell = GetDataGridCell(row, colIndex);
            var textBox = cell is null ? null : GetVisualChild<TextBox>(cell);
            textBox?.Focus();
            textBox?.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private static DataGridCell? GetDataGridCell(DataGridRow row, int columnIndex)
    {
        if (GetVisualChild<DataGridCellsPresenter>(row) is not { } presenter)
        {
            return null;
        }

        return presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
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
