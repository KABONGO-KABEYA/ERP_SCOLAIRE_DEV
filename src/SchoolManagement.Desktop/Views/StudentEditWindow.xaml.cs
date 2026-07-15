using System.Windows;
using SchoolManagement.Application.Students.DTOs;

namespace SchoolManagement.Desktop.Views;

public partial class StudentEditWindow : Window
{
    private readonly StudentDto _student;

    public UpdateStudentRequest? Result { get; private set; }

    public StudentEditWindow(StudentDto student)
    {
        _student = student;
        InitializeComponent();
        RegistrationText.Text = $"Matricule {student.RegistrationNumber}";
        LastNameField.Text = student.LastName;
        MiddleNameField.Text = student.MiddleName ?? string.Empty;
        FirstNameField.Text = student.FirstName;
        PhoneField.Text = student.Phone ?? string.Empty;
        EmailField.Text = student.Email ?? string.Empty;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LastNameField.Text) || string.IsNullOrWhiteSpace(FirstNameField.Text))
        {
            MessageBox.Show(this, "Le nom et le prénom sont obligatoires.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = new UpdateStudentRequest(
            FirstNameField.Text.Trim(),
            LastNameField.Text.Trim(),
            string.IsNullOrWhiteSpace(MiddleNameField.Text) ? null : MiddleNameField.Text.Trim(),
            _student.Gender,
            _student.DateOfBirth,
            null,
            null,
            string.IsNullOrWhiteSpace(PhoneField.Text) ? null : PhoneField.Text.Trim(),
            string.IsNullOrWhiteSpace(EmailField.Text) ? null : EmailField.Text.Trim());

        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
