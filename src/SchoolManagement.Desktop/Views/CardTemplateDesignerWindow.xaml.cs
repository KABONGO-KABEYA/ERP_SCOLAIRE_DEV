using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SchoolManagement.Desktop.Printing.CardLayout;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class CardTemplateDesignerWindow : Window
{
    private CardTemplateDesignerViewModel? _vm;
    private bool _dragging;
    private bool _dragMoved;
    private Point _dragStartCanvas;
    private double _originLeft;
    private double _originTop;
    private Border? _dragTarget;
    private string? _dragElementId;

    public CardTemplateDesignerWindow(CardTemplateDesignerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _vm = viewModel;
        viewModel.CanvasInvalidated += RebuildCanvas;
        viewModel.SelectionChromeChanged += UpdateSelectionChrome;
        Loaded += (_, _) =>
        {
            RebuildCanvas();
            Focus();
        };
        Closed += (_, _) =>
        {
            viewModel.CanvasInvalidated -= RebuildCanvas;
            viewModel.SelectionChromeChanged -= UpdateSelectionChrome;
        };
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void RebuildCanvas()
    {
        if (_vm is null || _dragging) return;

        DesignCanvas.Children.Clear();
        DesignCanvas.Width = _vm.CanvasWidthDip;
        DesignCanvas.Height = _vm.CanvasHeightDip;

        try
        {
            DesignCanvas.Background = (Brush)new BrushConverter().ConvertFromString(_vm.BackgroundColor)!;
        }
        catch
        {
            DesignCanvas.Background = Brushes.White;
        }

        if (_vm.SnapToGrid)
            DrawGrid();

        foreach (var element in _vm.Elements.OrderBy(e => e.ZIndex))
        {
            var host = BuildEditableHost(element);
            DesignCanvas.Children.Add(host);
        }
    }

    private void DrawGrid()
    {
        var step = CardLayoutUnits.MmToDip(CardTemplateDesignerViewModel.GridMm) * CardTemplateDesignerViewModel.EditorZoom;
        for (double x = 0; x <= DesignCanvas.Width; x += step)
        {
            DesignCanvas.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = DesignCanvas.Height,
                Stroke = new SolidColorBrush(Color.FromArgb(28, 100, 116, 139)),
                StrokeThickness = 0.5,
                IsHitTestVisible = false
            });
        }

        for (double y = 0; y <= DesignCanvas.Height; y += step)
        {
            DesignCanvas.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = DesignCanvas.Width, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(28, 100, 116, 139)),
                StrokeThickness = 0.5,
                IsHitTestVisible = false
            });
        }
    }

    private Border BuildEditableHost(CardLayoutElement element)
    {
        var zoom = CardTemplateDesignerViewModel.EditorZoom;
        var left = CardLayoutUnits.MmToDip(element.X) * zoom;
        var top = CardLayoutUnits.MmToDip(element.Y) * zoom;
        var width = Math.Max(8, CardLayoutUnits.MmToDip(element.Width) * zoom);
        var height = Math.Max(8, CardLayoutUnits.MmToDip(element.Height) * zoom);
        var selected = _vm?.SelectedElement?.Id == element.Id;

        var content = CardVisualRenderer.Build(
            new CardLayoutDocument
            {
                WidthMm = element.Width,
                HeightMm = element.Height,
                BackgroundColor = "Transparent",
                Elements =
                [
                    CloneForPreview(element)
                ]
            },
            _vm!.PreviewContext(),
            zoom);

        // Empêche le contenu de capturer la souris — le Border gère le drag.
        if (content is UIElement ui)
            ui.IsHitTestVisible = false;

        var host = new Border
        {
            Width = width,
            Height = height,
            Child = content,
            BorderBrush = selected ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : Brushes.Transparent,
            BorderThickness = new Thickness(selected ? 2 : 1),
            Background = selected
                ? new SolidColorBrush(Color.FromArgb(28, 37, 99, 235))
                : new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Cursor = Cursors.SizeAll,
            Tag = element.Id,
            SnapsToDevicePixels = true
        };

        Canvas.SetLeft(host, left);
        Canvas.SetTop(host, top);
        Panel.SetZIndex(host, element.ZIndex + 10);
        host.MouseLeftButtonDown += Element_MouseLeftButtonDown;
        host.MouseMove += Element_MouseMove;
        host.MouseLeftButtonUp += Element_MouseLeftButtonUp;
        host.LostMouseCapture += Element_LostMouseCapture;
        return host;
    }

    private static CardLayoutElement CloneForPreview(CardLayoutElement element) =>
        new()
        {
            Kind = element.Kind,
            X = 0,
            Y = 0,
            Width = element.Width,
            Height = element.Height,
            Text = element.Text,
            DataField = element.DataField,
            FontFamily = element.FontFamily,
            FontSizePt = element.FontSizePt,
            Bold = element.Bold,
            Foreground = element.Foreground,
            Background = element.Background,
            BorderColor = element.BorderColor,
            BorderThickness = element.BorderThickness,
            CornerRadiusMm = element.CornerRadiusMm,
            Opacity = element.Opacity,
            GradientTo = element.GradientTo,
            GradientVertical = element.GradientVertical,
            ImagePath = element.ImagePath,
            HorizontalAlignment = element.HorizontalAlignment,
            Visible = true,
            ZIndex = 1
        };

    private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border fe || _vm is null) return;

        var id = fe.Tag as string;
        _vm.SelectElementById(id);
        UpdateSelectionChrome();

        _dragging = true;
        _dragMoved = false;
        _dragTarget = fe;
        _dragElementId = id;
        _dragStartCanvas = e.GetPosition(DesignCanvas);
        _originLeft = Canvas.GetLeft(fe);
        _originTop = Canvas.GetTop(fe);
        if (double.IsNaN(_originLeft)) _originLeft = 0;
        if (double.IsNaN(_originTop)) _originTop = 0;

        Panel.SetZIndex(fe, 10_000);
        fe.CaptureMouse();
        fe.Opacity = 0.92;
        e.Handled = true;
    }

    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _dragTarget is null || _vm is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndDrag(commit: _dragMoved);
            return;
        }

        var pos = e.GetPosition(DesignCanvas);
        var dx = pos.X - _dragStartCanvas.X;
        var dy = pos.Y - _dragStartCanvas.Y;

        if (!_dragMoved && Math.Abs(dx) < 1 && Math.Abs(dy) < 1)
            return;

        _dragMoved = true;

        var left = _originLeft + dx;
        var top = _originTop + dy;

        left = Math.Clamp(left, 0, Math.Max(0, DesignCanvas.Width - _dragTarget.Width));
        top = Math.Clamp(top, 0, Math.Max(0, DesignCanvas.Height - _dragTarget.Height));

        if (_vm.SnapToGrid)
        {
            var step = CardLayoutUnits.MmToDip(CardTemplateDesignerViewModel.GridMm) * CardTemplateDesignerViewModel.EditorZoom;
            left = Math.Round(left / step) * step;
            top = Math.Round(top / step) * step;
        }

        Canvas.SetLeft(_dragTarget, left);
        Canvas.SetTop(_dragTarget, top);
        _vm.UpdateSelectedPositionLive(left, top);
        e.Handled = true;
    }

    private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        EndDrag(commit: _dragMoved);
        e.Handled = true;
    }

    private void Element_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragging)
            EndDrag(commit: _dragMoved);
    }

    private void EndDrag(bool commit)
    {
        if (_vm is null) return;

        var target = _dragTarget;
        var left = target is null ? 0 : Canvas.GetLeft(target);
        var top = target is null ? 0 : Canvas.GetTop(target);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        if (target is not null)
        {
            target.Opacity = 1.0;
            if (target.IsMouseCaptured)
                target.ReleaseMouseCapture();
        }

        _dragging = false;
        _dragTarget = null;
        _dragElementId = null;

        if (commit)
            _vm.CommitSelectedPosition(left, top);
        else
            UpdateSelectionChrome();
    }

    private void DesignCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_dragging || _vm is null) return;
        if (e.OriginalSource == DesignCanvas)
        {
            _vm.SelectedElement = null;
            UpdateSelectionChrome();
        }
    }

    private void DesignCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        // Drag géré sur l'élément capturé — rien ici.
    }

    private void DesignCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
            EndDrag(commit: _dragMoved);
    }

    private void UpdateSelectionChrome()
    {
        if (_vm is null || _dragging) return;

        foreach (UIElement child in DesignCanvas.Children)
        {
            if (child is not Border border || border.Tag is not string id)
                continue;

            var selected = _vm.SelectedElement?.Id == id;
            border.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
                : Brushes.Transparent;
            border.BorderThickness = new Thickness(selected ? 2 : 1);
            border.Background = selected
                ? new SolidColorBrush(Color.FromArgb(28, 37, 99, 235))
                : new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm?.SelectedElement is null || _dragging) return;

        var stepMm = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 5.0 : 1.0;
        var dx = 0.0;
        var dy = 0.0;

        switch (e.Key)
        {
            case Key.Left: dx = -stepMm; break;
            case Key.Right: dx = stepMm; break;
            case Key.Up: dy = -stepMm; break;
            case Key.Down: dy = stepMm; break;
            case Key.Delete:
                _vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                return;
            default:
                return;
        }

        _vm.NudgeSelected(dx, dy);
        e.Handled = true;
    }
}
