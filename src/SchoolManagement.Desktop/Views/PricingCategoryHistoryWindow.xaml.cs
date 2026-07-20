using System.Windows;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Views;

public partial class PricingCategoryHistoryWindow : Window
{
    public PricingCategoryHistoryWindow(
        StudentPricingAssignmentDto student,
        IFinanceApiService financeApi)
    {
        InitializeComponent();
        StudentInfoText.Text =
            $"{student.FullName} · {student.RegistrationNumber} · {student.ClassName} · {student.AcademicYearLabel}";
        _ = LoadAsync(student.EnrollmentId, financeApi);
    }

    private async Task LoadAsync(Guid enrollmentId, IFinanceApiService financeApi)
    {
        try
        {
            var items = await financeApi.GetPricingCategoryHistoryAsync(enrollmentId);
            HistoryGrid.ItemsSource = items;
        }
        catch (Exception ex)
        {
            StudentInfoText.Text += $"\n{ex.Message}";
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
