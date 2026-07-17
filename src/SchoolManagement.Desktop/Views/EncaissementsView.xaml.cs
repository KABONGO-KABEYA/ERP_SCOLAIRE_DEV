using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class EncaissementsView : UserControl
{
    public EncaissementsView()
    {
        InitializeComponent();
    }

    private void StudentsGrid_OnLoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (DataContext is not EncaissementsViewModel vm)
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
            || button.Tag is not StudentPaymentSituationDto student
            || DataContext is not EncaissementsViewModel vm)
        {
            return;
        }

        var menu = new ContextMenu { Style = (Style)FindResource("ErpStudentsContextMenu") };
        menu.Items.Add(CreateMenuItem("Encaisser un paiement", PackIconKind.CashPlus, BrushFrom(30, 94, 255),
            (_, _) => vm.CollectPaymentCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Consulter l'historique des paiements", PackIconKind.History, BrushFrom(31, 41, 55),
            (_, _) => vm.ViewPaymentHistoryCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Voir la situation financière", PackIconKind.ChartBoxOutline, BrushFrom(30, 94, 255),
            (_, _) => vm.ViewFinancialSituationCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Voir les répartitions", PackIconKind.ChartPie, BrushFrom(5, 150, 105),
            (_, _) => vm.ViewAllocationsCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Voir les retenues appliquées", PackIconKind.PercentOutline, BrushFrom(124, 58, 237),
            (_, _) => vm.ViewWithholdingsCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Réimprimer un reçu", PackIconKind.PrinterOutline, BrushFrom(31, 41, 55),
            (_, _) => vm.ReprintReceiptCommand.Execute(student)));

        if (vm.CanMutatePaidPayments)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("Modifier un paiement", PackIconKind.PencilOutline, BrushFrom(30, 94, 255),
                (_, _) => vm.EditPaymentCommand.Execute(student)));
            menu.Items.Add(CreateMenuItem("Annuler un paiement", PackIconKind.CloseCircleOutline, BrushFrom(239, 68, 68),
                (_, _) => vm.CancelPaymentCommand.Execute(student)));
        }

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
