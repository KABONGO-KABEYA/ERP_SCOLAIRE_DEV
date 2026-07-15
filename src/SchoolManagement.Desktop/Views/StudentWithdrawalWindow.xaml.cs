using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Application.Students;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Desktop.Views;

public partial class StudentWithdrawalWindow : Window
{
    private readonly WithdrawalReasonsDto _reasons;

    public StudentWithdrawalDialogResult? Result { get; private set; }

    public StudentWithdrawalWindow(string studentFullName, WithdrawalReasonsDto reasons)
    {
        InitializeComponent();
        _reasons = reasons;
        StudentNameText.Text = $"Élève : {studentFullName}";
        LoadReasons();
    }

    private void LoadReasons()
    {
        ReasonCombo.ItemsSource = ExclusionRadio.IsChecked == true
            ? _reasons.ExclusionReasons
            : _reasons.AbandonReasons;
        ReasonCombo.SelectedIndex = 0;
        UpdateCustomReasonVisibility();
    }

    private void WithdrawalType_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        LoadReasons();
    }

    private void ReasonCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateCustomReasonVisibility();

    private void UpdateCustomReasonVisibility()
    {
        var code = ReasonCombo.SelectedValue as string;
        CustomReasonBox.Visibility = string.Equals(code, StudentWithdrawalReasons.CustomCode, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (ReasonCombo.SelectedValue is not string reasonCode || string.IsNullOrWhiteSpace(reasonCode))
        {
            ErrorText.Text = "Sélectionnez une raison.";
            return;
        }

        var type = ExclusionRadio.IsChecked == true
            ? StudentWithdrawalType.Exclusion
            : StudentWithdrawalType.Abandon;

        string? customReason = null;
        if (string.Equals(reasonCode, StudentWithdrawalReasons.CustomCode, StringComparison.OrdinalIgnoreCase))
        {
            customReason = CustomReasonBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(customReason))
            {
                ErrorText.Text = "Veuillez préciser la raison.";
                return;
            }
        }

        try
        {
            StudentWithdrawalReasons.ResolveLabel(type, reasonCode, customReason);
        }
        catch (DomainException ex)
        {
            ErrorText.Text = ex.Message;
            return;
        }

        Result = new StudentWithdrawalDialogResult
        {
            Confirmed = true,
            WithdrawalType = type,
            ReasonCode = reasonCode,
            CustomReason = customReason
        };

        DialogResult = true;
        Close();
    }
}

public sealed class StudentWithdrawalDialogResult
{
    public bool Confirmed { get; init; }

    public StudentWithdrawalType WithdrawalType { get; init; }

    public string ReasonCode { get; init; } = string.Empty;

    public string? CustomReason { get; init; }
}
