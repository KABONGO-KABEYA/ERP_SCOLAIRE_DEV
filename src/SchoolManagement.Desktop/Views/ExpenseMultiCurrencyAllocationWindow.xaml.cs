using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Application.Accounting.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ExpenseMultiCurrencyAllocationWindow : Window
{
    private readonly ExpenseMultiCurrencyAllocationViewModel _viewModel;

    public ExpenseMultiCurrencyAllocationWindow(
        string accountTitle,
        decimal expenseAmount,
        string primaryCurrencyCode,
        Guid? primaryCurrencyId,
        IReadOnlyList<ExpenseCurrencyBalanceLine> balances,
        DateOnly asOfDate,
        ICurrencyApiService currencyApi,
        IAuthSessionService authSession)
    {
        InitializeComponent();
        _viewModel = new ExpenseMultiCurrencyAllocationViewModel(
            accountTitle,
            expenseAmount,
            primaryCurrencyCode,
            primaryCurrencyId,
            balances,
            asOfDate,
            currencyApi,
            authSession);
        DataContext = _viewModel;
        _viewModel.RequestCloseSuccess += () =>
        {
            DialogResult = true;
            Close();
        };
        _viewModel.RequestCloseCancel += () =>
        {
            DialogResult = false;
            Close();
        };
    }

    public bool Confirmed => _viewModel.Confirmed;
    public IReadOnlyList<CreateExpensePaymentAllocationLine> ConfirmedLines => _viewModel.ConfirmedLines;

    private async void Window_OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.InitializeAsync();

    private async void DataGrid_OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;
        if (e.Row.Item is not ExpenseMultiCurrencyAllocationLineViewModel line)
            return;

        // Laisser le binding se propager avant recalcul.
        await Dispatcher.InvokeAsync(async () =>
        {
            if (e.Column?.Header?.ToString() is "Taux")
                await _viewModel.OnLineRateEditedAsync(line);
            else
                await _viewModel.OnLineAmountEditedAsync(line);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
}
