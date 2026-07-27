using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.DocumentBranding;
using SchoolManagement.Application.DocumentBranding.DTOs;
using SchoolManagement.Domain.Enums;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class DocumentBrandingViewModel : ViewModelBase
{
    private readonly IDocumentBrandingApiService _api;
    private readonly IDocumentBrandingPathResolver _pathResolver;

    public DocumentBrandingViewModel(
        IDocumentBrandingApiService api,
        IDocumentBrandingPathResolver pathResolver)
    {
        _api = api;
        _pathResolver = pathResolver;
        RefreshHeaderPreviewLayout();
    }

    public ObservableCollection<BrandingLogoItemViewModel> Logos { get; } = [];
    public ObservableCollection<BrandingHeaderItemViewModel> Headers { get; } = [];
    public ObservableCollection<BrandingSignatureItemViewModel> Signatures { get; } = [];
    public ObservableCollection<BrandingStampItemViewModel> Stamps { get; } = [];
    public ObservableCollection<DocumentBrandingTypeOptionDto> DocumentTypes { get; } = [];
    public ObservableCollection<HeaderDocumentTypeOptionViewModel> HeaderDocumentTypeOptions { get; } = [];
    public ObservableCollection<HeaderDocumentTypeOptionViewModel> SignatureDocumentTypeOptions { get; } = [];
    public ObservableCollection<HeaderPrintModeOptionDto> PrintModes { get; } = [];

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _validationMessage;

    [ObservableProperty] private BrandingLogoItemViewModel? _selectedLogo;
    [ObservableProperty] private string _logoName = string.Empty;
    [ObservableProperty] private bool _logoIsPrimary;
    [ObservableProperty] private bool _logoIsActive = true;
    [ObservableProperty] private string? _logoPendingImagePath;
    [ObservableProperty] private string? _logoPreviewPath;

    [ObservableProperty] private BrandingHeaderItemViewModel? _selectedHeader;
    [ObservableProperty] private string _headerName = string.Empty;
    [ObservableProperty] private HeaderPrintMode _headerPrintMode = HeaderPrintMode.FullImage;
    [ObservableProperty] private bool _headerIsActive = true;
    [ObservableProperty] private string? _headerPendingImagePath;
    [ObservableProperty] private string? _headerPreviewPath;
    [ObservableProperty] private double _headerMarginLeftMm;
    [ObservableProperty] private double _headerMarginRightMm;
    [ObservableProperty] private double _headerMaxHeightMm = 20;

    [ObservableProperty] private BrandingSignatureItemViewModel? _selectedSignature;
    [ObservableProperty] private string _signatureName = string.Empty;
    [ObservableProperty] private string _signatureFunction = string.Empty;
    [ObservableProperty] private bool _signatureIsActive = true;
    [ObservableProperty] private string? _signaturePendingImagePath;
    [ObservableProperty] private string? _signaturePreviewPath;

    [ObservableProperty] private BrandingStampItemViewModel? _selectedStamp;
    [ObservableProperty] private string _stampName = string.Empty;
    [ObservableProperty] private bool _stampIsActive = true;
    [ObservableProperty] private string? _stampPendingImagePath;
    [ObservableProperty] private string? _stampPreviewPath;

    [ObservableProperty] private string? _footerAddress;
    [ObservableProperty] private string? _footerPhone;
    [ObservableProperty] private string? _footerEmail;
    [ObservableProperty] private string? _footerWebsite;
    [ObservableProperty] private string? _footerPoBox;
    [ObservableProperty] private string? _footerSchoolMotto;
    [ObservableProperty] private string? _footerFreeText;

    partial void OnSelectedLogoChanged(BrandingLogoItemViewModel? value)
    {
        if (value is null)
        {
            ClearLogoForm();
            return;
        }

        LogoName = value.Name;
        LogoIsPrimary = value.IsPrimary;
        LogoIsActive = value.IsActive;
        LogoPendingImagePath = null;
        LogoPreviewPath = value.PreviewPath;
    }

    partial void OnSelectedHeaderChanged(BrandingHeaderItemViewModel? value)
    {
        if (value is null)
        {
            ClearHeaderForm();
            return;
        }

        HeaderName = value.Name;
        SetHeaderDocumentTypeSelection(value.ApplicableDocumentTypes);
        HeaderPrintMode = value.PrintMode;
        HeaderIsActive = value.IsActive;
        HeaderMarginLeftMm = (double)value.MarginLeftMm;
        HeaderMarginRightMm = (double)value.MarginRightMm;
        HeaderMaxHeightMm = (double)(value.MaxHeightMm ?? 20m);
        HeaderPendingImagePath = null;
        HeaderPreviewPath = value.PreviewPath;
        RefreshHeaderPreviewLayout();
    }

    partial void OnSelectedSignatureChanged(BrandingSignatureItemViewModel? value)
    {
        if (value is null)
        {
            ClearSignatureForm();
            return;
        }

        SignatureName = value.SignatoryName;
        SignatureFunction = value.Function;
        SetSignatureDocumentTypeSelection(value.ApplicableDocumentTypes);
        SignatureIsActive = value.IsActive;
        SignaturePendingImagePath = null;
        SignaturePreviewPath = value.PreviewPath;
    }

    partial void OnSelectedStampChanged(BrandingStampItemViewModel? value)
    {
        if (value is null)
        {
            ClearStampForm();
            return;
        }

        StampName = value.Name;
        StampIsActive = value.IsActive;
        StampPendingImagePath = null;
        StampPreviewPath = value.PreviewPath;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        ValidationMessage = null;
        try
        {
            var lookups = await _api.GetLookupsAsync();
            DocumentTypes.Clear();
            PrintModes.Clear();
            HeaderDocumentTypeOptions.Clear();
            SignatureDocumentTypeOptions.Clear();
            foreach (var item in lookups.DocumentTypes)
            {
                DocumentTypes.Add(item);
                HeaderDocumentTypeOptions.Add(new HeaderDocumentTypeOptionViewModel(item.Value, item.Label));
                SignatureDocumentTypeOptions.Add(new HeaderDocumentTypeOptionViewModel(item.Value, item.Label));
            }

            foreach (var item in lookups.PrintModes) PrintModes.Add(item);

            var config = await _api.GetConfigurationAsync();
            Logos.Clear();
            foreach (var logo in config.Logos)
            {
                Logos.Add(new BrandingLogoItemViewModel(logo, _pathResolver));
            }

            Headers.Clear();
            foreach (var header in config.Headers)
            {
                Headers.Add(new BrandingHeaderItemViewModel(header, _pathResolver));
            }

            Signatures.Clear();
            foreach (var signature in config.Signatures)
            {
                Signatures.Add(new BrandingSignatureItemViewModel(signature, _pathResolver));
            }

            Stamps.Clear();
            foreach (var stamp in config.Stamps)
            {
                Stamps.Add(new BrandingStampItemViewModel(stamp, _pathResolver));
            }

            if (config.Footer is not null)
            {
                FooterAddress = config.Footer.Address;
                FooterPhone = config.Footer.Phone;
                FooterEmail = config.Footer.Email;
                FooterWebsite = config.Footer.Website;
                FooterPoBox = config.Footer.PoBox;
                FooterSchoolMotto = config.Footer.SchoolMotto;
                FooterFreeText = config.Footer.FreeText;
            }

            StatusMessage = "Configuration chargée.";
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private void NewLogo() { SelectedLogo = null; ClearLogoForm(); }
    [RelayCommand] private void NewHeader() { SelectedHeader = null; ClearHeaderForm(); }
    [RelayCommand] private void NewSignature() { SelectedSignature = null; ClearSignatureForm(); }
    [RelayCommand] private void NewStamp() { SelectedStamp = null; ClearStampForm(); }

    [RelayCommand]
    private void PickLogoImage() => PickImage(path => { LogoPendingImagePath = path; LogoPreviewPath = path; });

    [RelayCommand]
    private void PickHeaderImage() => PickImage(path => { HeaderPendingImagePath = path; HeaderPreviewPath = path; });

    [RelayCommand]
    private void PickSignatureImage() => PickImage(path => { SignaturePendingImagePath = path; SignaturePreviewPath = path; });

    [RelayCommand]
    private void PickStampImage() => PickImage(path => { StampPendingImagePath = path; StampPreviewPath = path; });

    public void ImportDroppedImage(string filePath, int tabIndex)
    {
        switch (tabIndex)
        {
            case 0: LogoPendingImagePath = filePath; LogoPreviewPath = filePath; break;
            case 1: HeaderPendingImagePath = filePath; HeaderPreviewPath = filePath; break;
            case 2: SignaturePendingImagePath = filePath; SignaturePreviewPath = filePath; break;
            case 3: StampPendingImagePath = filePath; StampPreviewPath = filePath; break;
        }
    }

    [RelayCommand]
    private async Task SaveLogoAsync()
    {
        if (string.IsNullOrWhiteSpace(LogoName))
        {
            ValidationMessage = "Le nom du logo est obligatoire.";
            return;
        }

        var request = new SaveSchoolLogoRequest(LogoName.Trim(), LogoIsPrimary, LogoIsActive);
        IsBusy = true;
        ValidationMessage = null;
        try
        {
            if (SelectedLogo is null || SelectedLogo.Id == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(LogoPendingImagePath))
                {
                    ValidationMessage = "Importez une image de logo.";
                    return;
                }

                await _api.CreateLogoAsync(request, LogoPendingImagePath);
            }
            else
            {
                await _api.UpdateLogoAsync(SelectedLogo.Id, request, LogoPendingImagePath);
            }

            StatusMessage = "Logo enregistré.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ValidationMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteLogoAsync()
    {
        if (SelectedLogo is null || SelectedLogo.Id == Guid.Empty) return;
        IsBusy = true;
        try
        {
            await _api.DeleteLogoAsync(SelectedLogo.Id);
            StatusMessage = "Logo supprimé.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SetPrimaryLogoAsync()
    {
        if (SelectedLogo is null || SelectedLogo.Id == Guid.Empty) return;
        IsBusy = true;
        try
        {
            await _api.SetPrimaryLogoAsync(SelectedLogo.Id);
            StatusMessage = "Logo principal défini.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveHeaderAsync()
    {
        if (string.IsNullOrWhiteSpace(HeaderName))
        {
            ValidationMessage = "Le nom de l'en-tête est obligatoire.";
            return;
        }

        var selectedTypes = GetSelectedHeaderDocumentTypes();
        if (selectedTypes.Count == 0)
        {
            ValidationMessage = "Sélectionnez au moins un type de document.";
            return;
        }

        var request = new SaveSchoolDocumentHeaderRequest(
            HeaderName.Trim(),
            selectedTypes[0],
            HeaderPrintMode,
            null,
            null,
            null,
            HeaderIsActive,
            DocumentBrandingTypeCodec.Serialize(selectedTypes),
            (decimal)HeaderMarginLeftMm,
            (decimal)HeaderMarginRightMm,
            (decimal)HeaderMaxHeightMm);
        IsBusy = true;
        try
        {
            if (SelectedHeader is null || SelectedHeader.Id == Guid.Empty)
            {
                if (HeaderPrintMode == HeaderPrintMode.FullImage && string.IsNullOrWhiteSpace(HeaderPendingImagePath))
                {
                    ValidationMessage = "Importez l'image complète de l'en-tête.";
                    return;
                }

                await _api.CreateHeaderAsync(request, HeaderPendingImagePath);
            }
            else
            {
                await _api.UpdateHeaderAsync(SelectedHeader.Id, request, HeaderPendingImagePath);
            }

            StatusMessage = "En-tête enregistré.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteHeaderAsync()
    {
        if (SelectedHeader is null || SelectedHeader.Id == Guid.Empty) return;
        IsBusy = true;
        try
        {
            await _api.DeleteHeaderAsync(SelectedHeader.Id);
            StatusMessage = "En-tête supprimé.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveSignatureAsync()
    {
        if (string.IsNullOrWhiteSpace(SignatureName) || string.IsNullOrWhiteSpace(SignatureFunction))
        {
            ValidationMessage = "Nom et fonction obligatoires.";
            return;
        }

        var selectedTypes = GetSelectedSignatureDocumentTypes();
        if (selectedTypes.Count == 0)
        {
            ValidationMessage = "Sélectionnez au moins un type de document.";
            return;
        }

        var request = new SaveSchoolSignatureRequest(
            SignatureName.Trim(),
            SignatureFunction.Trim(),
            SignatureIsActive,
            selectedTypes[0],
            DocumentBrandingTypeCodec.Serialize(selectedTypes));
        IsBusy = true;
        try
        {
            if (SelectedSignature is null || SelectedSignature.Id == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(SignaturePendingImagePath))
                {
                    ValidationMessage = "Importez l'image de signature.";
                    return;
                }

                await _api.CreateSignatureAsync(request, SignaturePendingImagePath);
            }
            else
            {
                await _api.UpdateSignatureAsync(SelectedSignature.Id, request, SignaturePendingImagePath);
            }

            StatusMessage = "Signature enregistrée.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSignatureAsync()
    {
        if (SelectedSignature is null || SelectedSignature.Id == Guid.Empty) return;
        IsBusy = true;
        try
        {
            await _api.DeleteSignatureAsync(SelectedSignature.Id);
            StatusMessage = "Signature supprimée.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveStampAsync()
    {
        if (string.IsNullOrWhiteSpace(StampName))
        {
            ValidationMessage = "Le nom du cachet est obligatoire.";
            return;
        }

        var request = new SaveSchoolStampRequest(StampName.Trim(), StampIsActive);
        IsBusy = true;
        try
        {
            if (SelectedStamp is null || SelectedStamp.Id == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(StampPendingImagePath))
                {
                    ValidationMessage = "Importez l'image du cachet.";
                    return;
                }

                await _api.CreateStampAsync(request, StampPendingImagePath);
            }
            else
            {
                await _api.UpdateStampAsync(SelectedStamp.Id, request, StampPendingImagePath);
            }

            StatusMessage = "Cachet enregistré.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteStampAsync()
    {
        if (SelectedStamp is null || SelectedStamp.Id == Guid.Empty) return;
        IsBusy = true;
        try
        {
            await _api.DeleteStampAsync(SelectedStamp.Id);
            StatusMessage = "Cachet supprimé.";
            await LoadAsync();
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveFooterAsync()
    {
        IsBusy = true;
        try
        {
            await _api.SaveFooterAsync(new SaveSchoolDocumentFooterRequest(
                FooterAddress, FooterPhone, FooterEmail, FooterWebsite, FooterPoBox, FooterSchoolMotto, FooterFreeText));
            StatusMessage = "Pied de page enregistré.";
        }
        catch (Exception ex) { ValidationMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ApplySignatureTemplate(string role) => SignatureFunction = role;

    private static void PickImage(Action<string> assign)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|Tous les fichiers|*.*"
        };
        if (ErpFileDialog.ShowOpen(dialog, ErpFileDialog.ResolveOwnerWindow()) == true)
        {
            assign(dialog.FileName);
        }
    }

    private void ClearLogoForm()
    {
        LogoName = string.Empty;
        LogoIsPrimary = false;
        LogoIsActive = true;
        LogoPendingImagePath = null;
        LogoPreviewPath = null;
    }

    private void ClearHeaderForm()
    {
        HeaderName = string.Empty;
        SetHeaderDocumentTypeSelection([DocumentBrandingType.FicheInscription]);
        HeaderPrintMode = HeaderPrintMode.FullImage;
        HeaderIsActive = true;
        HeaderMarginLeftMm = 0;
        HeaderMarginRightMm = 0;
        HeaderMaxHeightMm = 20;
        HeaderPendingImagePath = null;
        HeaderPreviewPath = null;
        RefreshHeaderPreviewLayout();
    }

    partial void OnHeaderMarginLeftMmChanged(double value) => RefreshHeaderPreviewLayout();

    partial void OnHeaderMarginRightMmChanged(double value) => RefreshHeaderPreviewLayout();

    partial void OnHeaderMaxHeightMmChanged(double value) => RefreshHeaderPreviewLayout();

    public Thickness HeaderPreviewPadding { get; private set; } = new(0);

    public double HeaderPreviewHeight { get; private set; } = 72;

    private void RefreshHeaderPreviewLayout()
    {
        var left = Math.Clamp(HeaderMarginLeftMm, 0, 30) * 2.5;
        var right = Math.Clamp(HeaderMarginRightMm, 0, 30) * 2.5;
        HeaderPreviewPadding = new Thickness(left, 8, right, 8);
        HeaderPreviewHeight = Math.Clamp(HeaderMaxHeightMm, 8, 50) * 3.2;
        OnPropertyChanged(nameof(HeaderPreviewPadding));
        OnPropertyChanged(nameof(HeaderPreviewHeight));
    }

    private void SetHeaderDocumentTypeSelection(IEnumerable<DocumentBrandingType> selectedTypes)
    {
        var selected = selectedTypes.ToHashSet();
        foreach (var option in HeaderDocumentTypeOptions)
        {
            option.IsSelected = selected.Contains(option.Value);
        }
    }

    private List<DocumentBrandingType> GetSelectedHeaderDocumentTypes() =>
        HeaderDocumentTypeOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .OrderBy(type => (int)type)
            .ToList();

    private void ClearSignatureForm()
    {
        SignatureName = string.Empty;
        SignatureFunction = string.Empty;
        SetSignatureDocumentTypeSelection([DocumentBrandingType.FicheInscription]);
        SignatureIsActive = true;
        SignaturePendingImagePath = null;
        SignaturePreviewPath = null;
    }

    private void SetSignatureDocumentTypeSelection(IEnumerable<DocumentBrandingType> selectedTypes)
    {
        var selected = selectedTypes.ToHashSet();
        foreach (var option in SignatureDocumentTypeOptions)
        {
            option.IsSelected = selected.Contains(option.Value);
        }
    }

    private List<DocumentBrandingType> GetSelectedSignatureDocumentTypes() =>
        SignatureDocumentTypeOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Value)
            .OrderBy(type => (int)type)
            .ToList();

    private void ClearStampForm()
    {
        StampName = string.Empty;
        StampIsActive = true;
        StampPendingImagePath = null;
        StampPreviewPath = null;
    }
}

public sealed class BrandingLogoItemViewModel
{
    public BrandingLogoItemViewModel(SchoolLogoDto dto, IDocumentBrandingPathResolver resolver)
    {
        Id = dto.Id;
        Name = dto.Name;
        ImagePath = dto.ImagePath;
        IsPrimary = dto.IsPrimary;
        IsActive = dto.IsActive;
        PreviewPath = resolver.ResolveAbsolutePath(dto.ImagePath);
    }

    public Guid Id { get; }
    public string Name { get; }
    public string ImagePath { get; }
    public bool IsPrimary { get; }
    public bool IsActive { get; }
    public string? PreviewPath { get; }
}

public sealed class BrandingHeaderItemViewModel
{
    public BrandingHeaderItemViewModel(SchoolDocumentHeaderDto dto, IDocumentBrandingPathResolver resolver)
    {
        Id = dto.Id;
        Name = dto.Name;
        DocumentType = dto.DocumentType;
        DocumentTypeLabel = dto.DocumentTypeLabel;
        ApplicableDocumentTypes = dto.ApplicableDocumentTypes;
        ApplicableDocumentTypesLabel = dto.ApplicableDocumentTypesLabel;
        PrintMode = dto.PrintMode;
        PrintModeLabel = dto.PrintModeLabel;
        IsActive = dto.IsActive;
        MarginLeftMm = dto.MarginLeftMm;
        MarginRightMm = dto.MarginRightMm;
        MaxHeightMm = dto.MaxHeightMm;
        PreviewPath = resolver.ResolveAbsolutePath(dto.ImagePath);
    }

    public Guid Id { get; }
    public string Name { get; }
    public DocumentBrandingType DocumentType { get; }
    public string DocumentTypeLabel { get; }
    public string ApplicableDocumentTypesLabel { get; }
    public HeaderPrintMode PrintMode { get; }
    public IReadOnlyList<DocumentBrandingType> ApplicableDocumentTypes { get; }
    public string PrintModeLabel { get; }
    public bool IsActive { get; }
    public decimal MarginLeftMm { get; }
    public decimal MarginRightMm { get; }
    public decimal? MaxHeightMm { get; }
    public string? PreviewPath { get; }
}

public sealed class BrandingSignatureItemViewModel
{
    public BrandingSignatureItemViewModel(SchoolSignatureDto dto, IDocumentBrandingPathResolver resolver)
    {
        Id = dto.Id;
        SignatoryName = dto.SignatoryName;
        Function = dto.Function;
        ApplicableDocumentTypes = dto.ApplicableDocumentTypes;
        ApplicableDocumentTypesLabel = dto.ApplicableDocumentTypesLabel;
        IsActive = dto.IsActive;
        PreviewPath = resolver.ResolveAbsolutePath(dto.ImagePath);
    }

    public Guid Id { get; }
    public string SignatoryName { get; }
    public string Function { get; }
    public string ApplicableDocumentTypesLabel { get; }
    public IReadOnlyList<DocumentBrandingType> ApplicableDocumentTypes { get; }
    public bool IsActive { get; }
    public string? PreviewPath { get; }
}

public sealed class BrandingStampItemViewModel
{
    public BrandingStampItemViewModel(SchoolStampDto dto, IDocumentBrandingPathResolver resolver)
    {
        Id = dto.Id;
        Name = dto.Name;
        IsActive = dto.IsActive;
        PreviewPath = resolver.ResolveAbsolutePath(dto.ImagePath);
    }

    public Guid Id { get; }
    public string Name { get; }
    public bool IsActive { get; }
    public string? PreviewPath { get; }
}

public partial class HeaderDocumentTypeOptionViewModel : ObservableObject
{
    public HeaderDocumentTypeOptionViewModel(DocumentBrandingType value, string label)
    {
        Value = value;
        Label = label;
    }

    public DocumentBrandingType Value { get; }
    public string Label { get; }

    [ObservableProperty] private bool _isSelected;
}
