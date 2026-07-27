using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.StudentCards.DTOs;
using SchoolManagement.Desktop.Printing.CardLayout;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public sealed record DesignerToolboxItem(CardElementKind Kind, string Label, string IconKind);

public sealed record DataFieldOption(CardDataField Field, string Label);

/// <summary>Concepteur graphique de modèles de cartes (drag & drop, grille, recto/verso).</summary>
public partial class CardTemplateDesignerViewModel : ViewModelBase
{
    public const double EditorZoom = 3.0;
    public const double GridMm = 1.0;

    private readonly IStudentCardApiService _cardApi;
    private readonly ISchoolApiService _schoolApi;
    private readonly IDocumentBrandingApiService _brandingApi;
    private readonly IDocumentBrandingPathResolver _brandingPathResolver;
    private CardLayoutDocument _front = CardLayoutDefaults.CreateProfessionalFront();
    private CardLayoutDocument _back = CardLayoutDefaults.CreateProfessionalBack();

    private string _previewSchoolName = "Collège Saint Benoît";
    private string? _previewMotto = "Savoir · Discipline · Excellence";
    private string? _previewAddress = "Avenue de l'École, Kinshasa";
    private string? _previewPhone = "+243 800 000 000";
    private string? _previewEmail = "contact@college-saint-benoit.cd";
    private string? _previewWebsite;
    private string? _previewLogoPath;

    public CardTemplateDesignerViewModel(
        IStudentCardApiService cardApi,
        ISchoolApiService schoolApi,
        IDocumentBrandingApiService brandingApi,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        _cardApi = cardApi;
        _schoolApi = schoolApi;
        _brandingApi = brandingApi;
        _brandingPathResolver = brandingPathResolver;
        ToolboxItems =
        [
            new DesignerToolboxItem(CardElementKind.Text, "Texte", "FormatText"),
            new DesignerToolboxItem(CardElementKind.Photo, "Photo", "AccountBox"),
            new DesignerToolboxItem(CardElementKind.Logo, "Logo", "Image"),
            new DesignerToolboxItem(CardElementKind.QrCode, "QR Code", "Qrcode"),
            new DesignerToolboxItem(CardElementKind.Rectangle, "Rectangle", "RectangleOutline"),
            new DesignerToolboxItem(CardElementKind.Ellipse, "Cercle", "CircleOutline"),
            new DesignerToolboxItem(CardElementKind.Line, "Ligne", "Minus"),
            new DesignerToolboxItem(CardElementKind.Barcode, "Code-barres", "Barcode")
        ];

        DataFields =
        [
            new DataFieldOption(CardDataField.None, "(texte libre)"),
            new DataFieldOption(CardDataField.SchoolName, "Nom école"),
            new DataFieldOption(CardDataField.FullName, "Nom complet"),
            new DataFieldOption(CardDataField.LastName, "Nom"),
            new DataFieldOption(CardDataField.FirstName, "Prénom"),
            new DataFieldOption(CardDataField.MiddleName, "Postnom"),
            new DataFieldOption(CardDataField.Gender, "Sexe"),
            new DataFieldOption(CardDataField.DateOfBirth, "Date naissance"),
            new DataFieldOption(CardDataField.ClassName, "Classe"),
            new DataFieldOption(CardDataField.StudyOption, "Option"),
            new DataFieldOption(CardDataField.RegistrationNumber, "Matricule"),
            new DataFieldOption(CardDataField.CardNumber, "N° carte"),
            new DataFieldOption(CardDataField.AcademicYear, "Année scolaire"),
            new DataFieldOption(CardDataField.ExpiresAt, "Expiration"),
            new DataFieldOption(CardDataField.Motto, "Devise"),
            new DataFieldOption(CardDataField.Address, "Adresse"),
            new DataFieldOption(CardDataField.Phone, "Téléphone"),
            new DataFieldOption(CardDataField.Email, "Email"),
            new DataFieldOption(CardDataField.Website, "Site web"),
            new DataFieldOption(CardDataField.EmergencyContact, "Contact urgence")
        ];
    }

    public IReadOnlyList<DesignerToolboxItem> ToolboxItems { get; }
    public IReadOnlyList<DataFieldOption> DataFields { get; }
    public ObservableCollection<CardLayoutElement> Elements { get; } = [];

    public event Action? CanvasInvalidated;
    public event Action? SelectionChromeChanged;

    [ObservableProperty] private Guid? _templateId;
    [ObservableProperty] private string _templateName = "Carte Élève";
    [ObservableProperty] private string? _templateDescription;
    [ObservableProperty] private double _widthMm = 85.6;
    [ObservableProperty] private double _heightMm = 53.98;
    [ObservableProperty] private string _backgroundColor = "#F8FAFC";
    [ObservableProperty] private bool _snapToGrid = true;
    [ObservableProperty] private bool _isFrontSide = true;
    [ObservableProperty] private CardLayoutElement? _selectedElement;
    [ObservableProperty] private DataFieldOption? _selectedDataField;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isNewTemplate = true;

    public double CanvasWidthDip => CardLayoutUnits.MmToDip(WidthMm) * EditorZoom;
    public double CanvasHeightDip => CardLayoutUnits.MmToDip(HeightMm) * EditorZoom;
    public string SideLabel => IsFrontSide ? "Recto" : "Verso";

    partial void OnWidthMmChanged(double value) => NotifyCanvasSize();
    partial void OnHeightMmChanged(double value) => NotifyCanvasSize();
    partial void OnBackgroundColorChanged(string value) => CanvasInvalidated?.Invoke();

    partial void OnIsFrontSideChanging(bool value)
    {
        // Sauvegarde le côté courant avant bascule.
        var doc = IsFrontSide ? _front : _back;
        doc.BackgroundColor = BackgroundColor;
        doc.WidthMm = WidthMm;
        doc.HeightMm = HeightMm;
        doc.Elements = Elements.ToList();
    }

    partial void OnIsFrontSideChanged(bool value)
    {
        LoadSideIntoEditor();
        OnPropertyChanged(nameof(SideLabel));
        CanvasInvalidated?.Invoke();
    }

    partial void OnSelectedElementChanged(CardLayoutElement? value)
    {
        SelectedDataField = DataFields.FirstOrDefault(f => f.Field == (value?.DataField ?? CardDataField.None));
        // Pas de RebuildCanvas ici : ça cassait le drag. Seul le chrome de sélection est mis à jour.
        SelectionChromeChanged?.Invoke();
        NotifyPositionProperties();
    }

    partial void OnSelectedDataFieldChanged(DataFieldOption? value)
    {
        if (SelectedElement is null || value is null) return;
        SelectedElement.DataField = value.Field;
        if (value.Field != CardDataField.None && string.IsNullOrWhiteSpace(SelectedElement.Text))
            SelectedElement.Text = value.Label;
        CanvasInvalidated?.Invoke();
        OnPropertyChanged(nameof(SelectedElement));
    }

    public void LoadTemplate(CardTemplateDto template)
    {
        TemplateId = template.Id;
        IsNewTemplate = false;
        TemplateName = template.Name;
        TemplateDescription = template.Description;
        WidthMm = (double)template.WidthMm;
        HeightMm = (double)template.HeightMm;
        _front = CardLayoutSerializer.Deserialize(template.LayoutJsonFront, WidthMm, HeightMm);
        _back = string.IsNullOrWhiteSpace(template.LayoutJsonBack)
            ? new CardLayoutDocument { WidthMm = WidthMm, HeightMm = HeightMm, Elements = [] }
            : CardLayoutSerializer.Deserialize(template.LayoutJsonBack, WidthMm, HeightMm);
        BackgroundColor = _front.BackgroundColor;
        IsFrontSide = true;
        LoadSideIntoEditor();
        StatusMessage = $"Modèle « {template.Name} » chargé.";
        _ = LoadSchoolPreviewAsync();
    }

    public void LoadNew()
    {
        TemplateId = null;
        IsNewTemplate = true;
        TemplateName = "Carte Élève CR80";
        TemplateDescription = "Modèle professionnel CR80 — personnalisable";
        WidthMm = 85.6;
        HeightMm = 53.98;
        var pair = CardLayoutDefaults.CreateProfessionalPair(WidthMm, HeightMm);
        _front = pair.Front;
        _back = pair.Back;
        BackgroundColor = _front.BackgroundColor;
        IsFrontSide = true;
        LoadSideIntoEditor();
        StatusMessage = "Modèle professionnel CR80 chargé — personnalisez librement recto et verso.";
        _ = LoadSchoolPreviewAsync();
    }

    public async Task LoadSchoolPreviewAsync()
    {
        try
        {
            var school = await _schoolApi.GetCurrentSchoolAsync();
            if (school is not null)
            {
                _previewSchoolName = school.Name;
                _previewAddress = string.Join(", ",
                    new[] { school.Address, school.City, school.Province }
                        .Where(p => !string.IsNullOrWhiteSpace(p)));
                _previewPhone = school.Phone;
                _previewEmail = school.Email;
            }

            var branding = await _brandingApi.GetConfigurationAsync();
            var footer = branding.Footer;
            if (footer is not null)
            {
                if (!string.IsNullOrWhiteSpace(footer.SchoolMotto))
                    _previewMotto = footer.SchoolMotto;
                if (!string.IsNullOrWhiteSpace(footer.Address))
                    _previewAddress = footer.Address;
                if (!string.IsNullOrWhiteSpace(footer.Phone))
                    _previewPhone = footer.Phone;
                if (!string.IsNullOrWhiteSpace(footer.Email))
                    _previewEmail = footer.Email;
                _previewWebsite = footer.Website;
            }

            var logo = branding.Logos.FirstOrDefault(l => l.IsPrimary && l.IsActive)
                ?? branding.Logos.FirstOrDefault(l => l.IsActive);
            _previewLogoPath = _brandingPathResolver.ResolveAbsolutePath(logo?.ImagePath);
            CanvasInvalidated?.Invoke();
        }
        catch
        {
            // Aperçu avec données d'exemple si l'API branding est indisponible.
        }
    }

    [RelayCommand]
    private void AddElement(DesignerToolboxItem? item)
    {
        if (item is null) return;
        var element = CreateDefaultElement(item.Kind);
        Elements.Add(element);
        SyncElementsToDocument();
        SelectedElement = element;
        CanvasInvalidated?.Invoke();
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        if (SelectedElement is null) return;
        Elements.Remove(SelectedElement);
        SelectedElement = null;
        SyncElementsToDocument();
        CanvasInvalidated?.Invoke();
    }

    [RelayCommand]
    private void BringForward()
    {
        if (SelectedElement is null) return;
        SelectedElement.ZIndex++;
        CanvasInvalidated?.Invoke();
    }

    [RelayCommand]
    private void SendBackward()
    {
        if (SelectedElement is null) return;
        SelectedElement.ZIndex = Math.Max(0, SelectedElement.ZIndex - 1);
        CanvasInvalidated?.Invoke();
    }

    [RelayCommand]
    private void ApplyPropertyEdits()
    {
        SyncElementsToDocument();
        CanvasInvalidated?.Invoke();
        StatusMessage = "Propriétés appliquées.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            StatusMessage = "Le nom du modèle est obligatoire.";
            return;
        }

        PersistCurrentSide();
        IsBusy = true;
        try
        {
            _front.WidthMm = WidthMm;
            _front.HeightMm = HeightMm;
            _front.BackgroundColor = BackgroundColor;
            _back.WidthMm = WidthMm;
            _back.HeightMm = HeightMm;

            var request = new SaveCardTemplateRequest(
                TemplateName.Trim(),
                TemplateDescription?.Trim(),
                (decimal)WidthMm,
                (decimal)HeightMm,
                CardTemplateOrientation.Landscape,
                CardTemplateKind.Eleve,
                CardLayoutSerializer.Serialize(_front),
                _back.Elements.Count == 0 ? null : CardLayoutSerializer.Serialize(_back),
                IsActive: true);

            if (IsNewTemplate || TemplateId is null)
            {
                var created = await _cardApi.CreateTemplateAsync(request);
                TemplateId = created.Id;
                IsNewTemplate = false;
                StatusMessage = "Modèle créé.";
            }
            else
            {
                await _cardApi.UpdateTemplateAsync(TemplateId.Value, request);
                StatusMessage = "Modèle enregistré.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SelectElementById(string? id)
    {
        SelectedElement = Elements.FirstOrDefault(e => e.Id == id);
    }

    /// <summary>Met à jour X/Y pendant le drag (sans reconstruit le canevas).</summary>
    public void UpdateSelectedPositionLive(double leftDip, double topDip)
    {
        if (SelectedElement is null) return;
        var x = CardLayoutUnits.DipToMm(leftDip / EditorZoom);
        var y = CardLayoutUnits.DipToMm(topDip / EditorZoom);
        SelectedElement.X = Math.Clamp(x, 0, Math.Max(0, WidthMm - 0.5));
        SelectedElement.Y = Math.Clamp(y, 0, Math.Max(0, HeightMm - 0.5));
        StatusMessage = $"Position : {SelectedElement.X:0.#} × {SelectedElement.Y:0.#} mm";
        NotifyPositionProperties();
    }

    /// <summary>Valide la position après drop (snap + sync document).</summary>
    public void CommitSelectedPosition(double leftDip, double topDip)
    {
        if (SelectedElement is null) return;
        var x = CardLayoutUnits.DipToMm(leftDip / EditorZoom);
        var y = CardLayoutUnits.DipToMm(topDip / EditorZoom);
        if (SnapToGrid)
        {
            x = Math.Round(x / GridMm) * GridMm;
            y = Math.Round(y / GridMm) * GridMm;
        }

        SelectedElement.X = Math.Clamp(x, 0, Math.Max(0, WidthMm - 0.5));
        SelectedElement.Y = Math.Clamp(y, 0, Math.Max(0, HeightMm - 0.5));
        SyncElementsToDocument();
        StatusMessage = $"Déplacé à {SelectedElement.X:0.#} × {SelectedElement.Y:0.#} mm";
        NotifyPositionProperties();
        SelectionChromeChanged?.Invoke();
    }

    public void NudgeSelected(double deltaXMm, double deltaYMm)
    {
        if (SelectedElement is null) return;
        SelectedElement.X = Math.Clamp(SelectedElement.X + deltaXMm, 0, Math.Max(0, WidthMm - 0.5));
        SelectedElement.Y = Math.Clamp(SelectedElement.Y + deltaYMm, 0, Math.Max(0, HeightMm - 0.5));
        if (SnapToGrid)
        {
            SelectedElement.X = Math.Round(SelectedElement.X / GridMm) * GridMm;
            SelectedElement.Y = Math.Round(SelectedElement.Y / GridMm) * GridMm;
        }

        SyncElementsToDocument();
        StatusMessage = $"Position : {SelectedElement.X:0.#} × {SelectedElement.Y:0.#} mm";
        NotifyPositionProperties();
        CanvasInvalidated?.Invoke();
    }

    public void MoveSelectedToDip(double leftDip, double topDip) =>
        CommitSelectedPosition(leftDip, topDip);

    private void NotifyPositionProperties()
    {
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(SelectedElementX));
        OnPropertyChanged(nameof(SelectedElementY));
    }

    public double SelectedElementX => SelectedElement?.X ?? 0;
    public double SelectedElementY => SelectedElement?.Y ?? 0;

    public void ResizeSelectedToDip(double widthDip, double heightDip)
    {
        if (SelectedElement is null) return;
        var w = CardLayoutUnits.DipToMm(widthDip / EditorZoom);
        var h = CardLayoutUnits.DipToMm(heightDip / EditorZoom);
        if (SnapToGrid)
        {
            w = Math.Max(GridMm, Math.Round(w / GridMm) * GridMm);
            h = Math.Max(GridMm, Math.Round(h / GridMm) * GridMm);
        }

        SelectedElement.Width = Math.Clamp(w, 1, WidthMm);
        SelectedElement.Height = Math.Clamp(h, 1, HeightMm);
        SyncElementsToDocument();
        OnPropertyChanged(nameof(SelectedElement));
        CanvasInvalidated?.Invoke();
    }

    public CardRenderContext PreviewContext() =>
        new()
        {
            FullName = "KABONGO Kabeya Christian",
            LastName = "KABONGO",
            FirstName = "CHRISTIAN",
            MiddleName = "KABEYA",
            Gender = "Masculin",
            DateOfBirth = "12/03/2012",
            ClassName = "5e Scientifique",
            StudyOption = "Math-Physique",
            RegistrationNumber = "2026-00125",
            CardNumber = "CSB-2026-000125",
            QrPayload = "ERP_CARD:PREVIEWTOKEN",
            SchoolName = _previewSchoolName,
            Motto = _previewMotto,
            Address = _previewAddress,
            Phone = _previewPhone,
            Email = _previewEmail,
            Website = _previewWebsite,
            AcademicYear = AcademicYearRefreshBridge.SelectedYear?.Label ?? "2025-2026",
            ExpiresAt = "31/08/2027",
            LogoAbsolutePath = _previewLogoPath
        };

    private void LoadSideIntoEditor()
    {
        var doc = IsFrontSide ? _front : _back;
        BackgroundColor = doc.BackgroundColor;
        Elements.Clear();
        foreach (var e in doc.Elements.OrderBy(e => e.ZIndex))
            Elements.Add(e);
        SelectedElement = null;
    }

    private void PersistCurrentSide()
    {
        SyncElementsToDocument();
    }

    private void SyncElementsToDocument()
    {
        var doc = IsFrontSide ? _front : _back;
        doc.Elements = Elements.ToList();
        doc.BackgroundColor = BackgroundColor;
        doc.WidthMm = WidthMm;
        doc.HeightMm = HeightMm;
    }

    private void NotifyCanvasSize()
    {
        OnPropertyChanged(nameof(CanvasWidthDip));
        OnPropertyChanged(nameof(CanvasHeightDip));
        CanvasInvalidated?.Invoke();
    }

    private static CardLayoutElement CreateDefaultElement(CardElementKind kind) =>
        kind switch
        {
            CardElementKind.Text => new CardLayoutElement
            {
                Kind = kind, X = 5, Y = 5, Width = 35, Height = 7,
                Text = "Texte", DataField = CardDataField.FullName, FontSizePt = 9, Bold = true
            },
            CardElementKind.Photo => new CardLayoutElement
            {
                Kind = kind, X = 5, Y = 15, Width = 20, Height = 25, BorderThickness = 0.4
            },
            CardElementKind.Logo => new CardLayoutElement
            {
                Kind = kind, X = 60, Y = 3, Width = 18, Height = 10,
                Background = "Transparent", BorderColor = "Transparent", BorderThickness = 0
            },
            CardElementKind.QrCode => new CardLayoutElement
            {
                Kind = kind, X = 65, Y = 30, Width = 15, Height = 15
            },
            CardElementKind.Rectangle => new CardLayoutElement
            {
                Kind = kind, X = 0, Y = 0, Width = 85.6, Height = 8,
                Background = "#1E5EFF", BorderThickness = 0
            },
            CardElementKind.Ellipse => new CardLayoutElement
            {
                Kind = kind, X = 35, Y = 20, Width = 12, Height = 12,
                Background = "#E8EFFF", BorderColor = "#1E5EFF", BorderThickness = 0.5
            },
            CardElementKind.Line => new CardLayoutElement
            {
                Kind = kind, X = 5, Y = 40, Width = 50, Height = 2,
                Foreground = "#94A3B8", BorderThickness = 1
            },
            _ => new CardLayoutElement
            {
                Kind = kind, X = 20, Y = 45, Width = 40, Height = 6, Text = "Code"
            }
        };
}
