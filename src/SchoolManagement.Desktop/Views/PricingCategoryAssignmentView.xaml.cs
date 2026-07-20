using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class PricingCategoryAssignmentView : UserControl
{
    public PricingCategoryAssignmentView()
    {
        InitializeComponent();
    }

    private void StudentsGrid_OnLoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (DataContext is not PricingCategoryAssignmentViewModel vm)
        {
            e.Row.Tag = (e.Row.GetIndex() + 1).ToString();
            return;
        }

        var pageOffset = (vm.CurrentPage - 1) * vm.PageSize;
        e.Row.Tag = (pageOffset + e.Row.GetIndex() + 1).ToString();
    }

    private void StudentActionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not StudentPricingAssignmentDto student
            || DataContext is not PricingCategoryAssignmentViewModel vm)
        {
            return;
        }

        var menu = new ContextMenu { Style = (Style)FindResource("ErpStudentsContextMenu") };
        if (vm.CanAssignPricingCategory)
        {
            menu.Items.Add(CreateMenuItem("Modifier la catégorie tarifaire", PackIconKind.TagOutline, BrushFrom(30, 94, 255),
                (_, _) => vm.ChangePricingCategoryCommand.Execute(student)));
        }

        menu.Items.Add(CreateMenuItem("Consulter l'historique des changements", PackIconKind.History, BrushFrom(31, 41, 55),
            (_, _) => vm.ViewCategoryHistoryCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Voir les frais applicables", PackIconKind.CashMultiple, BrushFrom(5, 150, 105),
            (_, _) => vm.ViewApplicableFeesCommand.Execute(student)));

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private MenuItem CreateMenuItem(string header, PackIconKind iconKind, Brush iconBrush, RoutedEventHandler handler)
    {
        var icon = new PackIcon
        {
            Kind = iconKind,
            Width = 18,
            Height = 18,
            Foreground = iconBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = header,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(icon);
        panel.Children.Add(label);

        var item = new MenuItem
        {
            Header = panel,
            Style = (Style)FindResource("ErpStudentsContextMenuItem")
        };
        item.Click += handler;
        return item;
    }

    private static SolidColorBrush BrushFrom(byte r, byte g, byte b) =>
        new(Color.FromRgb(r, g, b));
}
