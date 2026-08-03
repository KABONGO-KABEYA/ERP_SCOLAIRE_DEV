using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Setup.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.Desktop.ViewModels;

public partial class InitialSetupViewModel : ViewModelBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public InitialSetupViewModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        var now = DateTime.Today;
        var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
        AcademicYearLabel = $"{startYear}-{startYear + 1}";
        AcademicYearStart = new DateOnly(startYear, 9, 1);
        AcademicYearEnd = new DateOnly(startYear + 1, 7, 31);
        FeeTypesText = "Frais scolaires\nFrais d'inscription";
        InstallmentsText = "Inscription\n1ère tranche\n2ème tranche\n3ème tranche";
        PricingCategoriesText = "Général";
    }

    [ObservableProperty] private int _step = 1;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private string _schoolName = "";
    [ObservableProperty] private string _legalName = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _city = "";
    [ObservableProperty] private string _province = "";
    [ObservableProperty] private string _phone = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private Currency _selectedCurrency = Currency.CDF;
    [ObservableProperty] private string? _logoPath;
    [ObservableProperty] private string _logoFileName = "";

    [ObservableProperty] private string _academicYearLabel = "";
    [ObservableProperty] private DateOnly _academicYearStart;
    [ObservableProperty] private DateOnly _academicYearEnd;

    public DateTime? AcademicYearStartDate
    {
        get => AcademicYearStart.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value is DateTime dt)
                AcademicYearStart = DateOnly.FromDateTime(dt);
            OnPropertyChanged();
        }
    }

    public DateTime? AcademicYearEndDate
    {
        get => AcademicYearEnd.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value is DateTime dt)
                AcademicYearEnd = DateOnly.FromDateTime(dt);
            OnPropertyChanged();
        }
    }

    [ObservableProperty] private string _adminUserName = "admin";
    [ObservableProperty] private string _adminEmail = "";
    [ObservableProperty] private string _adminFirstName = "";
    [ObservableProperty] private string _adminLastName = "";
    [ObservableProperty] private string _adminPassword = "";
    [ObservableProperty] private string _adminPasswordConfirm = "";

    [ObservableProperty] private string _feeTypesText = "";
    [ObservableProperty] private string _installmentsText = "";
    [ObservableProperty] private string _pricingCategoriesText = "";

    public IReadOnlyList<Currency> Currencies { get; } = [Currency.CDF, Currency.USD];

    public string StepTitle => Step switch
    {
        1 => "Établissement",
        2 => "Année scolaire",
        3 => "Administrateur",
        4 => "Paramètres financiers de base",
        _ => "Configuration"
    };

    partial void OnStepChanged(int value) => OnPropertyChanged(nameof(StepTitle));

    [RelayCommand]
    private void BrowseLogo()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Logo de l'établissement",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tous|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            LogoPath = dlg.FileName;
            LogoFileName = Path.GetFileName(dlg.FileName);
        }
    }

    [RelayCommand]
    private void Next()
    {
        ErrorMessage = null;
        if (Step == 1 && string.IsNullOrWhiteSpace(SchoolName))
        {
            ErrorMessage = "Indiquez le nom de l'école.";
            return;
        }

        if (Step == 2)
        {
            if (string.IsNullOrWhiteSpace(AcademicYearLabel))
            {
                ErrorMessage = "Indiquez le libellé de l'année scolaire.";
                return;
            }

            if (AcademicYearEnd <= AcademicYearStart)
            {
                ErrorMessage = "La date de fin doit être après la date de début.";
                return;
            }
        }

        if (Step == 3)
        {
            if (string.IsNullOrWhiteSpace(AdminUserName) ||
                string.IsNullOrWhiteSpace(AdminFirstName) ||
                string.IsNullOrWhiteSpace(AdminLastName) ||
                string.IsNullOrWhiteSpace(AdminEmail))
            {
                ErrorMessage = "Complétez les informations administrateur.";
                return;
            }

            if (AdminPassword.Length < 8)
            {
                ErrorMessage = "Mot de passe : 8 caractères minimum.";
                return;
            }

            if (AdminPassword != AdminPasswordConfirm)
            {
                ErrorMessage = "Les mots de passe ne correspondent pas.";
                return;
            }
        }

        if (Step < 4)
            Step++;
    }

    [RelayCommand]
    private void Back()
    {
        ErrorMessage = null;
        if (Step > 1)
            Step--;
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            string? logoBase64 = null;
            if (!string.IsNullOrWhiteSpace(LogoPath) && File.Exists(LogoPath))
                logoBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(LogoPath));

            var feeTypes = FeeTypesText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(n => new InitialFeeTypeRequest(n, SelectedCurrency, true))
                .ToList();

            var installments = InstallmentsText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var categories = PricingCategoriesText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var request = new CompleteInitialSetupRequest(
                SchoolName,
                NullIfEmpty(LegalName),
                NullIfEmpty(Address),
                NullIfEmpty(City),
                NullIfEmpty(Province),
                NullIfEmpty(Phone),
                NullIfEmpty(Email),
                SelectedCurrency,
                NullIfEmpty(LogoFileName),
                logoBase64,
                AcademicYearLabel,
                AcademicYearStart,
                AcademicYearEnd,
                AdminUserName,
                AdminEmail,
                AdminPassword,
                AdminFirstName,
                AdminLastName,
                feeTypes,
                installments,
                categories);

            var client = _httpClientFactory.CreateClient("SchoolApi");
            using var response = await client.PostAsJsonAsync("api/v1/setup/complete", request);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
                throw new InvalidOperationException(err?.Message ?? $"Erreur HTTP {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<CompleteInitialSetupResultDto>>();
            MessageBox.Show(
                $"Configuration terminée.\n\nÉcole : {body?.Data?.SchoolName}\nCompte : {body?.Data?.AdminUserName}\n\nConnectez-vous avec ce compte.",
                "ERP Scolaire",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Completed?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event Action? Completed;

    public static async Task<bool> NeedsSetupAsync(IHttpClientFactory httpClientFactory, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient("SchoolApi");
            using var response = await client.GetAsync("api/v1/setup/status", ct);
            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<InitialSetupStatusDto>>(cancellationToken: ct);
            return body?.Data?.NeedsSetup == true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
