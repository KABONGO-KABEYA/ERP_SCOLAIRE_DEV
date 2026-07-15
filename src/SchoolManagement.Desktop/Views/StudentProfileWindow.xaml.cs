using System.Windows;
using SchoolManagement.Application.Students.DTOs;

namespace SchoolManagement.Desktop.Views;

public partial class StudentProfileWindow : Window
{
    public StudentProfileWindow(StudentProfileDto profile)
    {
        InitializeComponent();
        TitleText.Text = profile.Student.FullName;
        SubtitleText.Text = $"Matricule {profile.Student.RegistrationNumber}";

        AddLine("Nom", profile.Student.LastName);
        AddLine("Postnom", profile.Student.MiddleName ?? "—");
        AddLine("Prénom", profile.Student.FirstName);
        AddLine("Sexe", profile.Student.Gender.ToString());
        AddLine("Date de naissance", profile.Student.DateOfBirth.ToString("dd/MM/yyyy"));
        AddLine("Téléphone", profile.Student.Phone ?? "—");
        AddLine("Email", profile.Student.Email ?? "—");
        if (profile.Student.IsEnrolledCurrentYear)
        {
            AddLine("Classe année courante", profile.Student.CurrentYearClassName ?? "—");
        }

        EnrollmentsGrid.ItemsSource = profile.Enrollments;
    }

    private void AddLine(string label, string value)
    {
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var labelBlock = new System.Windows.Controls.TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.Gray
        };
        var valueBlock = new System.Windows.Controls.TextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
        System.Windows.Controls.Grid.SetColumn(valueBlock, 1);
        grid.Children.Add(labelBlock);
        grid.Children.Add(valueBlock);
        IdentityPanel.Children.Add(grid);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
