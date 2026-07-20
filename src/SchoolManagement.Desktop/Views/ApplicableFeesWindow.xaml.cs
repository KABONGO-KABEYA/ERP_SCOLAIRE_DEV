using System.Windows;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Views;

public partial class ApplicableFeesWindow : Window
{
    public ApplicableFeesWindow(
        StudentPricingAssignmentDto student,
        IFinanceApiService financeApi)
    {
        InitializeComponent();
        StudentInfoText.Text =
            $"{student.FullName} · {student.RegistrationNumber} · {student.ClassName}\n" +
            $"Catégorie : {student.FeePricingCategoryName} · {student.AcademicYearLabel}";
        _ = LoadAsync(student.EnrollmentId, financeApi);
    }

    private async Task LoadAsync(Guid enrollmentId, IFinanceApiService financeApi)
    {
        try
        {
            var fees = await financeApi.GetApplicableFeesAsync(enrollmentId);
            FeesGrid.ItemsSource = fees.Lines;
            TotalText.Text = fees.Lines.Count == 0
                ? "Aucun frais configuré pour cette catégorie / classe."
                : $"Total : {fees.TotalAmount:N0} {fees.Currency} · {fees.Lines.Count} ligne(s)";
        }
        catch (Exception ex)
        {
            TotalText.Text = ex.Message;
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
