using System.Windows;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Application.SchoolFees.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Views;

public partial class ChangePricingCategoryWindow : Window
{
    private readonly IFinanceApiService _financeApi;
    private readonly StudentPricingAssignmentDto _student;
    private readonly IReadOnlyList<FeePricingCategoryDto> _categories;

    public StudentPricingAssignmentDto? UpdatedAssignment { get; private set; }

    public ChangePricingCategoryWindow(
        StudentPricingAssignmentDto student,
        IReadOnlyList<FeePricingCategoryDto> categories,
        IFinanceApiService financeApi)
    {
        InitializeComponent();
        _student = student;
        _categories = categories;
        _financeApi = financeApi;

        StudentInfoText.Text =
            $"{student.FullName} · {student.RegistrationNumber} · {student.ClassName}\n" +
            $"Catégorie actuelle : {student.FeePricingCategoryName}";

        CategoryCombo.ItemsSource = categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
        CategoryCombo.SelectedItem = categories.FirstOrDefault(c => c.Id == student.FeePricingCategoryId)
            ?? categories.FirstOrDefault();
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (CategoryCombo.SelectedItem is not FeePricingCategoryDto category)
        {
            ErrorText.Text = "Sélectionnez une catégorie tarifaire.";
            return;
        }

        if (category.Id == _student.FeePricingCategoryId)
        {
            ErrorText.Text = "Choisissez une catégorie différente de l'actuelle.";
            return;
        }

        IsEnabled = false;
        try
        {
            UpdatedAssignment = await _financeApi.UpdatePricingAssignmentAsync(
                _student.EnrollmentId,
                new UpdateEnrollmentPricingCategoryRequest(
                    category.Id,
                    string.IsNullOrWhiteSpace(NotesBox.Text) ? null : NotesBox.Text.Trim()));
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
