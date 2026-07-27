using System.Windows;
using SchoolManagement.Application.Finance.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views.Encaissements;

public partial class CollectPaymentWindow : Window
{
    private readonly CollectPaymentViewModel _viewModel;

    public CollectPaymentWindow(
        StudentPaymentSituationDto situation,
        IPaymentApiService paymentApi,
        IFinanceApiService financeApi,
        ICurrencyApiService currencyApi,
        IAuthSessionService authSession,
        IStudentDossierPathResolver dossierPathResolver,
        IFeeTypeStatementPrintService statementPrint)
    {
        InitializeComponent();
        _viewModel = new CollectPaymentViewModel(
            situation,
            paymentApi,
            financeApi,
            currencyApi,
            authSession,
            dossierPathResolver,
            statementPrint);
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

    public bool NeedsRefresh => DialogResult == true;

    private async void Window_OnLoaded(object sender, RoutedEventArgs e) =>
        await _viewModel.InitializeAsync();
}
