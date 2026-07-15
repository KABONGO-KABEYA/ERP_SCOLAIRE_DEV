using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class StudentsView : UserControl
{
    public StudentsView()
    {
        InitializeComponent();
    }

    private void StudentsGrid_OnLoadingRow(object sender, DataGridRowEventArgs e)
    {
        e.Row.Tag = (e.Row.GetIndex() + 1).ToString();
    }

    private void StudentActionsButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not StudentDto student || DataContext is not StudentsViewModel vm)
        {
            return;
        }

        var menu = new ContextMenu { Style = (Style)FindResource("ErpStudentsContextMenu") };
        menu.Items.Add(CreateMenuItem("Afficher", PackIconKind.EyeOutline, new SolidColorBrush(Color.FromRgb(31, 41, 55)),
            (_, _) => vm.ShowStudentProfileCommand.Execute(student)));
        menu.Items.Add(CreateMenuItem("Dossier élève", PackIconKind.FolderOutline, new SolidColorBrush(Color.FromRgb(30, 94, 255)),
            (_, _) => vm.ShowStudentDossierFilesCommand.Execute(student)));

        if (student.IsEnrolledCurrentYear)
        {
            menu.Items.Add(CreateMenuItem("Modifier", PackIconKind.PencilOutline, new SolidColorBrush(Color.FromRgb(30, 94, 255)),
                (_, _) => vm.EditStudentCommand.Execute(student)));
            menu.Items.Add(CreateMenuItem("Exclure / Abandonner", PackIconKind.AccountRemoveOutline, new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                (_, _) => vm.WithdrawStudentCommand.Execute(student)));
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
}
