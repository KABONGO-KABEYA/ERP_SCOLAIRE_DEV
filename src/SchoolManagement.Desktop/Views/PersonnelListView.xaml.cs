using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Application.Personnel.DTOs;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class PersonnelListView : UserControl
{
    public PersonnelListView()
    {
        InitializeComponent();
    }

    private void PersonnelActionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button
            || button.Tag is not PersonnelListItemDto item
            || DataContext is not PersonnelListViewModel vm)
        {
            return;
        }

        var menu = new ContextMenu { Style = (Style)FindResource("ErpStudentsContextMenu") };
        menu.Items.Add(CreateMenuItem(
            "Voir la fiche",
            PackIconKind.EyeOutline,
            new SolidColorBrush(Color.FromRgb(31, 41, 55)),
            (_, _) => vm.ViewPersonnelCommand.Execute(item)));
        menu.Items.Add(CreateMenuItem(
            "Modifier",
            PackIconKind.PencilOutline,
            new SolidColorBrush(Color.FromRgb(30, 94, 255)),
            (_, _) => vm.EditPersonnelCommand.Execute(item)));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem(
            "Imprimer la fiche",
            PackIconKind.PrinterOutline,
            new SolidColorBrush(Color.FromRgb(30, 94, 255)),
            (_, _) => vm.PrintPersonnelCommand.Execute(item)));
        menu.Items.Add(CreateMenuItem(
            "Exporter PDF",
            PackIconKind.FilePdfBox,
            new SolidColorBrush(Color.FromRgb(30, 94, 255)),
            (_, _) => vm.ExportPersonnelPdfCommand.Execute(item)));
        menu.Items.Add(CreateMenuItem(
            "Exporter Excel",
            PackIconKind.FileExcelOutline,
            new SolidColorBrush(Color.FromRgb(22, 163, 74)),
            (_, _) => vm.ExportPersonnelExcelCommand.Execute(item)));

        if (item.IsActive)
        {
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem(
                "Désactiver",
                PackIconKind.AccountOffOutline,
                new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                (_, _) => vm.DeactivatePersonnelCommand.Execute(item)));
        }

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private MenuItem CreateMenuItem(string header, PackIconKind iconKind, Brush iconBrush, RoutedEventHandler handler)
    {
        var icon = new PackIcon
        {
            Kind = iconKind,
            Width = 16,
            Height = 16,
            Foreground = iconBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = header,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13
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
}
