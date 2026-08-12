using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Desktop.Navigation;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ShellView : UserControl
{
    private readonly Dictionary<string, Button> _settingsSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _financeSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _personnelSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _resultsSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _documentsSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ToggleButton> _mainNavButtons = new();
    private Expander? _settingsExpander;
    private Expander? _financeExpander;
    private Expander? _personnelExpander;
    private Expander? _resultsExpander;
    private Expander? _documentsExpander;
    private string? _selectedSettingsKey;
    private string? _selectedFinanceKey;
    private string? _selectedPersonnelKey;
    private string? _selectedResultsKey;
    private string? _selectedDocumentsKey;
    private bool _isBuildingNavigation;
    private IDesktopViewRegistry? _viewRegistry;

    public ShellView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shellViewModel)
        {
            return;
        }

        _viewRegistry = App.Services?.GetService(typeof(IDesktopViewRegistry)) as IDesktopViewRegistry
            ?? new DesktopViewRegistry();

        BuildNavigation(shellViewModel);
        shellViewModel.Modules.CollectionChanged += (_, _) => BuildNavigation(shellViewModel);
        shellViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ShellViewModel.SelectedModule))
            {
                SyncMainNavSelection(shellViewModel);
            }
            else if (args.PropertyName == nameof(ShellViewModel.CurrentViewModel))
            {
                ApplyPendingSettingsSelection(shellViewModel);
                ApplyPendingFinanceSelection(shellViewModel);
                ApplyPendingPersonnelSelection(shellViewModel);
                ApplyPendingResultsSelection(shellViewModel);
                UpdatePageTitle(shellViewModel);
            }
        };

        SyncMainNavSelection(shellViewModel);
        UpdatePageTitle(shellViewModel);

        ResultsNavigationBridge.SectionSelected += item =>
        {
            if (shellViewModel.CurrentViewModel is not ResultsHubViewModel)
            {
                return;
            }

            _selectedResultsKey = item.Key;
            UpdateResultsSubNavSelection(item.Key);
            PageTitleText.Text = item.Title;
            PageSubtitleText.Text = item.Subtitle;
            if (_resultsExpander is not null)
            {
                _resultsExpander.IsExpanded = true;
            }

            HighlightResultsHeader(true);
        };
    }

    private void BuildNavigation(ShellViewModel shellViewModel)
    {
        if (_isBuildingNavigation)
        {
            return;
        }

        _isBuildingNavigation = true;
        NavigationPanel.Children.Clear();
        _settingsSubNavButtons.Clear();
        _financeSubNavButtons.Clear();
        _personnelSubNavButtons.Clear();
        _resultsSubNavButtons.Clear();
        _documentsSubNavButtons.Clear();
        _mainNavButtons.Clear();

        foreach (var module in shellViewModel.Modules)
        {
            if (module.IsHub && module.ViewModelType == typeof(SettingsViewModel))
            {
                _settingsExpander = CreateDynamicHubExpander(
                    shellViewModel,
                    module,
                    PackIconKind.Cog,
                    _settingsSubNavButtons,
                    (svm, page) => NavigateByDesktopViewKey(svm, page));
                NavigationPanel.Children.Add(_settingsExpander);
                continue;
            }

            if (module.IsHub && module.ViewModelType == typeof(FinanceHubViewModel))
            {
                _financeExpander = CreateDynamicHubExpander(
                    shellViewModel,
                    module,
                    PackIconKind.Cash,
                    _financeSubNavButtons,
                    (svm, page) => NavigateByDesktopViewKey(svm, page));
                NavigationPanel.Children.Add(_financeExpander);
                continue;
            }

            if (module.IsHub && module.ViewModelType == typeof(PersonnelHubViewModel))
            {
                _personnelExpander = CreateDynamicHubExpander(
                    shellViewModel,
                    module,
                    PackIconKind.AccountTie,
                    _personnelSubNavButtons,
                    (svm, page) => NavigateByDesktopViewKey(svm, page));
                NavigationPanel.Children.Add(_personnelExpander);
                continue;
            }

            if (module.IsHub && module.ViewModelType == typeof(ResultsHubViewModel))
            {
                _resultsExpander = CreateDynamicHubExpander(
                    shellViewModel,
                    module,
                    PackIconKind.SchoolOutline,
                    _resultsSubNavButtons,
                    (svm, page) => NavigateByDesktopViewKey(svm, page));
                NavigationPanel.Children.Add(_resultsExpander);
                continue;
            }

            if (module.IsHub && module.ViewModelType == typeof(DocumentsHubViewModel))
            {
                _documentsExpander = CreateDynamicHubExpander(
                    shellViewModel,
                    module,
                    PackIconKind.FileDocument,
                    _documentsSubNavButtons,
                    (svm, page) => NavigateByDesktopViewKey(svm, page));
                NavigationPanel.Children.Add(_documentsExpander);
                continue;
            }

            if (module.ViewModelType is null)
            {
                continue;
            }

            var button = CreateMainNavButton(module.Title, module.IconKind);
            button.Click += (_, _) => NavigateToModule(shellViewModel, module.ViewModelType, null);
            NavigationPanel.Children.Add(button);
            _mainNavButtons[module.ViewModelType] = button;
        }

        _isBuildingNavigation = false;
    }

    private Expander CreateDynamicHubExpander(
        ShellViewModel shellViewModel,
        ModuleNavItem module,
        PackIconKind icon,
        Dictionary<string, Button> buttonMap,
        Action<ShellViewModel, ModuleNavPageItem> onClick)
    {
        var expander = new Expander
        {
            Style = (Style)FindResource("ErpSidebarSettingsExpander"),
            IsExpanded = false
        };

        expander.Header = CreateExpanderHeader(module.Title, icon);
        var content = new StackPanel();
        var groups = module.Pages.GroupBy(p => p.FunctionName).ToList();
        foreach (var group in groups)
        {
            if (groups.Count > 1)
            {
                content.Children.Add(new TextBlock
                {
                    Text = group.Key,
                    Style = (Style)FindResource("ErpSidebarSubNavGroupTitle")
                });
            }

            foreach (var page in group.OrderBy(p => p.SortOrder))
            {
                var iconKind = "CircleSmall";
                var buttonKey = page.DesktopViewKey;
                if (_viewRegistry?.TryResolve(page.DesktopViewKey, out var target) == true)
                {
                    iconKind = target switch
                    {
                        SettingsDesktopViewTarget s => s.Item.IconKind,
                        FinanceDesktopViewTarget f => f.Item.IconKind,
                        PersonnelDesktopViewTarget p => p.Item.IconKind,
                        ResultsDesktopViewTarget r => r.Item.IconKind,
                        DirectDesktopViewTarget => ResolveDirectPageIcon(page.DesktopViewKey),
                        _ => iconKind
                    };
                    buttonKey = target switch
                    {
                        SettingsDesktopViewTarget s => s.Item.Key,
                        FinanceDesktopViewTarget f => f.Item.Key,
                        PersonnelDesktopViewTarget p => p.Item.Key,
                        ResultsDesktopViewTarget r => r.Item.Key,
                        _ => page.DesktopViewKey
                    };
                }

                var subButton = CreateSubNavButton(buttonKey, page.Title, iconKind);
                subButton.Click += (_, _) => onClick(shellViewModel, page);
                content.Children.Add(subButton);
                buttonMap[buttonKey] = subButton;
            }
        }

        expander.Content = content;
        return expander;
    }

    private static string ResolveDirectPageIcon(string desktopViewKey) =>
        desktopViewKey switch
        {
            "Security.Users" => "AccountCog",
            "Security.Roles" => "ShieldAccount",
            "Security.Audit" => "ClipboardTextClock",
            "Security.Exceptions" => "ShieldKeyOutline",
            "Platform.Catalog" => "CloudCog",
            "Documents.Main" => "FileDocumentOutline",
            "StudentCards.Main" => "CardAccountDetails",
            _ => "CircleSmall"
        };

    private void NavigateByDesktopViewKey(ShellViewModel shellViewModel, ModuleNavPageItem page)
    {
        var desktopViewKey = page.DesktopViewKey;
        if (_viewRegistry is null || !_viewRegistry.TryResolve(desktopViewKey, out var target))
        {
            return;
        }

        switch (target)
        {
            case DirectDesktopViewTarget direct:
                NavigateToDirectCatalogPage(shellViewModel, direct.ViewModelType, page);
                break;
            case SettingsDesktopViewTarget settings:
                NavigateToSettingsSection(shellViewModel, settings.Item);
                break;
            case FinanceDesktopViewTarget finance:
                NavigateToFinanceSection(shellViewModel, finance.Item);
                break;
            case PersonnelDesktopViewTarget personnel:
                NavigateToPersonnelSection(shellViewModel, personnel.Item);
                break;
            case ResultsDesktopViewTarget results:
                NavigateToResultsSection(shellViewModel, results.Item);
                break;
        }
    }

    private void NavigateToDirectCatalogPage(
        ShellViewModel shellViewModel,
        Type viewModelType,
        ModuleNavPageItem page)
    {
        var owner = shellViewModel.Modules.FirstOrDefault(m =>
            m.Pages.Any(p => string.Equals(p.DesktopViewKey, page.DesktopViewKey, StringComparison.OrdinalIgnoreCase)));

        if (owner is not null && owner.ViewModelType == viewModelType && !owner.IsHub)
        {
            NavigateToModule(shellViewModel, viewModelType, null);
            PageTitleText.Text = page.Title;
            PageSubtitleText.Text = owner.Title;
            return;
        }

        shellViewModel.NavigateToDirectCatalogPage(viewModelType, owner);

        if (owner?.ViewModelType == typeof(SettingsViewModel))
        {
            if (_settingsExpander is not null)
            {
                _settingsExpander.IsExpanded = true;
            }

            _selectedSettingsKey = page.DesktopViewKey;
            _selectedFinanceKey = null;
            _selectedPersonnelKey = null;
            _selectedResultsKey = null;
            _selectedDocumentsKey = null;
            UpdateSettingsSubNavSelection(page.DesktopViewKey);
            ClearFinanceSubNavSelection();
            ClearPersonnelSubNavSelection();
            ClearResultsSubNavSelection();
            ClearDocumentsSubNavSelection();
            HighlightFinanceHeader(false);
            HighlightPersonnelHeader(false);
            HighlightResultsHeader(false);
            HighlightDocumentsHeader(false);
            PageTitleText.Text = page.Title;
            PageSubtitleText.Text = GetDirectPageSubtitle(page.DesktopViewKey);
            return;
        }

        if (owner?.ViewModelType == typeof(ResultsHubViewModel))
        {
            if (_resultsExpander is not null)
            {
                _resultsExpander.IsExpanded = true;
            }

            _selectedResultsKey = page.DesktopViewKey;
            _selectedSettingsKey = null;
            _selectedFinanceKey = null;
            _selectedPersonnelKey = null;
            _selectedDocumentsKey = null;
            UpdateResultsSubNavSelection(page.DesktopViewKey);
            ClearSettingsSubNavSelection();
            ClearFinanceSubNavSelection();
            ClearPersonnelSubNavSelection();
            ClearDocumentsSubNavSelection();
            HighlightSettingsHeader(false);
            HighlightFinanceHeader(false);
            HighlightPersonnelHeader(false);
            HighlightDocumentsHeader(false);
            PageTitleText.Text = page.Title;
            PageSubtitleText.Text = "Résultats scolaires";
            return;
        }

        if (owner?.ViewModelType == typeof(DocumentsHubViewModel))
        {
            if (_documentsExpander is not null)
            {
                _documentsExpander.IsExpanded = true;
            }

            _selectedDocumentsKey = page.DesktopViewKey;
            _selectedSettingsKey = null;
            _selectedFinanceKey = null;
            _selectedPersonnelKey = null;
            _selectedResultsKey = null;
            UpdateDocumentsSubNavSelection(page.DesktopViewKey);
            ClearSettingsSubNavSelection();
            ClearFinanceSubNavSelection();
            ClearPersonnelSubNavSelection();
            ClearResultsSubNavSelection();
            HighlightSettingsHeader(false);
            HighlightFinanceHeader(false);
            HighlightPersonnelHeader(false);
            HighlightResultsHeader(false);
            HighlightDocumentsHeader(true);
            PageTitleText.Text = page.Title;
            PageSubtitleText.Text = GetDocumentsPageSubtitle(page.DesktopViewKey);
            return;
        }

        PageTitleText.Text = page.Title;
        PageSubtitleText.Text = owner?.Title ?? "Gestion scolaire — République Démocratique du Congo";
    }

    private static string GetDocumentsPageSubtitle(string desktopViewKey) =>
        desktopViewKey switch
        {
            "Documents.Main" => "Gestion des documents élèves.",
            "StudentCards.Main" => "Émission et suivi des cartes élèves.",
            _ => "Documents"
        };

    private static string GetDirectPageSubtitle(string desktopViewKey) =>
        desktopViewKey switch
        {
            "Security.Users" => "Comptes, rôles assignés et permissions effectives.",
            "Security.Roles" => "Rôles applicatifs, matrice des permissions et dépendances.",
            "Security.Exceptions" => "Octrois et refus exceptionnels par utilisateur.",
            "Security.Audit" => "Journal des événements de sécurité et de gouvernance.",
            "Platform.Catalog" => "Catalogue plateforme — modules, fonctions et permissions.",
            _ => "Administration et configuration."
        };

    private static StackPanel CreateExpanderHeader(string title, PackIconKind iconKind)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new PackIcon
        {
            Kind = iconKind,
            Width = 20,
            Height = 20,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            Text = title
        });
        return header;
    }

    private static ToggleButton CreateMainNavButton(string title, string iconKind)
    {
        var button = new ToggleButton
        {
            Style = (Style)System.Windows.Application.Current.FindResource("ErpSidebarMainNavButton")
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new PackIcon
        {
            Kind = Enum.TryParse<PackIconKind>(iconKind, out var kind) ? kind : PackIconKind.Circle,
            Width = 20,
            Height = 20,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.Medium,
            Foreground = Brushes.White,
            Text = title
        });
        button.Content = panel;
        return button;
    }

    private Button CreateSubNavButton(string key, string title, string iconKind)
    {
        var button = new Button
        {
            Style = (Style)FindResource("ErpSidebarSubNavButton"),
            Tag = key
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new PackIcon
        {
            Kind = Enum.TryParse<PackIconKind>(iconKind, out var kind) ? kind : PackIconKind.Circle,
            Width = 16,
            Height = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            Text = title,
            TextWrapping = TextWrapping.Wrap
        });
        button.Content = panel;
        return button;
    }

    private void NavigateToModule(ShellViewModel shellViewModel, Type viewModelType, string? settingsKey)
    {
        var module = shellViewModel.Modules.FirstOrDefault(item => item.ViewModelType == viewModelType);
        if (module is null)
        {
            return;
        }

        if (viewModelType != typeof(SettingsViewModel))
        {
            _selectedSettingsKey = null;
            ClearSettingsSubNavSelection();
            HighlightSettingsHeader(false);
        }

        if (viewModelType != typeof(FinanceHubViewModel))
        {
            _selectedFinanceKey = null;
            ClearFinanceSubNavSelection();
            HighlightFinanceHeader(false);
        }

        if (viewModelType != typeof(PersonnelHubViewModel))
        {
            _selectedPersonnelKey = null;
            ClearPersonnelSubNavSelection();
            HighlightPersonnelHeader(false);
        }

        if (viewModelType != typeof(ResultsHubViewModel))
        {
            _selectedResultsKey = null;
            ClearResultsSubNavSelection();
            HighlightResultsHeader(false);
        }

        if (viewModelType != typeof(DocumentsHubViewModel))
        {
            _selectedDocumentsKey = null;
            ClearDocumentsSubNavSelection();
            HighlightDocumentsHeader(false);
        }

        shellViewModel.SelectedModule = module;
        shellViewModel.NavigateToViewModelType(viewModelType);

        if (viewModelType == typeof(SettingsViewModel) && !string.IsNullOrWhiteSpace(settingsKey))
        {
            var item = SettingsNavCatalog.FindByKey(settingsKey);
            if (item is not null)
            {
                NavigateToSettingsSection(shellViewModel, item);
            }
        }
    }

    private void NavigateToSettingsSection(ShellViewModel shellViewModel, SettingsNavItem item)
    {
        var settingsModule = shellViewModel.Modules.FirstOrDefault(module => module.ViewModelType == typeof(SettingsViewModel));
        if (settingsModule is null || _settingsExpander is null)
        {
            return;
        }

        shellViewModel.NavigateToViewModelType(typeof(SettingsViewModel));
        shellViewModel.SelectedModule = settingsModule;
        _settingsExpander.IsExpanded = true;
        _selectedSettingsKey = item.Key;
        _selectedFinanceKey = null;
        _selectedPersonnelKey = null;
        _selectedResultsKey = null;
        _selectedDocumentsKey = null;
        UpdateSettingsSubNavSelection(item.Key);
        ClearFinanceSubNavSelection();
        ClearPersonnelSubNavSelection();
        ClearResultsSubNavSelection();
        ClearDocumentsSubNavSelection();
        HighlightFinanceHeader(false);
        HighlightPersonnelHeader(false);
        HighlightResultsHeader(false);
        HighlightDocumentsHeader(false);

        if (shellViewModel.CurrentViewModel is SettingsViewModel settingsViewModel)
        {
            SettingsNavigationBridge.ApplyToViewModel(settingsViewModel, item);
        }

        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = GetSettingsSubtitle(item);
        SettingsNavigationBridge.Select(item);
    }

    private void NavigateToFinanceSection(ShellViewModel shellViewModel, FinanceNavItem item)
    {
        var financeModule = shellViewModel.Modules.FirstOrDefault(module => module.ViewModelType == typeof(FinanceHubViewModel));
        if (financeModule is null || _financeExpander is null)
        {
            return;
        }

        shellViewModel.NavigateToViewModelType(typeof(FinanceHubViewModel));
        shellViewModel.SelectedModule = financeModule;
        _financeExpander.IsExpanded = true;
        _selectedFinanceKey = item.Key;
        _selectedSettingsKey = null;
        _selectedPersonnelKey = null;
        _selectedResultsKey = null;
        _selectedDocumentsKey = null;
        UpdateFinanceSubNavSelection(item.Key);
        ClearSettingsSubNavSelection();
        ClearPersonnelSubNavSelection();
        ClearResultsSubNavSelection();
        ClearDocumentsSubNavSelection();
        HighlightSettingsHeader(false);
        HighlightPersonnelHeader(false);
        HighlightResultsHeader(false);
        HighlightDocumentsHeader(false);

        if (shellViewModel.CurrentViewModel is FinanceHubViewModel financeViewModel)
        {
            FinanceNavigationBridge.ApplyToViewModel(financeViewModel, item);
        }

        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = item.Subtitle;
        FinanceNavigationBridge.Select(item);
    }

    private void NavigateToPersonnelSection(ShellViewModel shellViewModel, PersonnelNavItem item)
    {
        var personnelModule = shellViewModel.Modules.FirstOrDefault(module => module.ViewModelType == typeof(PersonnelHubViewModel));
        if (personnelModule is null || _personnelExpander is null)
        {
            return;
        }

        shellViewModel.NavigateToViewModelType(typeof(PersonnelHubViewModel));
        shellViewModel.SelectedModule = personnelModule;
        _personnelExpander.IsExpanded = true;
        _selectedPersonnelKey = item.Key;
        _selectedSettingsKey = null;
        _selectedFinanceKey = null;
        _selectedResultsKey = null;
        _selectedDocumentsKey = null;
        UpdatePersonnelSubNavSelection(item.Key);
        ClearSettingsSubNavSelection();
        ClearFinanceSubNavSelection();
        ClearResultsSubNavSelection();
        ClearDocumentsSubNavSelection();
        HighlightSettingsHeader(false);
        HighlightFinanceHeader(false);
        HighlightResultsHeader(false);
        HighlightDocumentsHeader(false);
        HighlightPersonnelHeader(true);

        if (shellViewModel.CurrentViewModel is PersonnelHubViewModel personnelViewModel)
        {
            PersonnelNavigationBridge.ApplyToViewModel(personnelViewModel, item);
        }

        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = item.Subtitle;
        PersonnelNavigationBridge.Select(item);
    }

    private void NavigateToResultsSection(ShellViewModel shellViewModel, ResultsNavItem item)
    {
        var resultsModule = shellViewModel.Modules.FirstOrDefault(module => module.ViewModelType == typeof(ResultsHubViewModel));
        if (resultsModule is null || _resultsExpander is null)
        {
            return;
        }

        shellViewModel.NavigateToViewModelType(typeof(ResultsHubViewModel));
        shellViewModel.SelectedModule = resultsModule;
        _resultsExpander.IsExpanded = true;
        _selectedResultsKey = item.Key;
        _selectedSettingsKey = null;
        _selectedFinanceKey = null;
        _selectedPersonnelKey = null;
        _selectedDocumentsKey = null;
        UpdateResultsSubNavSelection(item.Key);
        ClearSettingsSubNavSelection();
        ClearFinanceSubNavSelection();
        ClearPersonnelSubNavSelection();
        ClearDocumentsSubNavSelection();
        HighlightSettingsHeader(false);
        HighlightFinanceHeader(false);
        HighlightPersonnelHeader(false);
        HighlightDocumentsHeader(false);
        HighlightResultsHeader(true);

        if (shellViewModel.CurrentViewModel is ResultsHubViewModel resultsViewModel)
        {
            ResultsNavigationBridge.ApplyToViewModel(resultsViewModel, item);
        }

        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = item.Subtitle;
        ResultsNavigationBridge.Select(item);
    }

    private void SyncMainNavSelection(ShellViewModel shellViewModel)
    {
        foreach (var pair in _mainNavButtons)
        {
            pair.Value.IsChecked = shellViewModel.SelectedModule?.ViewModelType == pair.Key;
        }

        var isSettings = shellViewModel.SelectedModule?.ViewModelType == typeof(SettingsViewModel);
        var isFinance = shellViewModel.SelectedModule?.ViewModelType == typeof(FinanceHubViewModel);
        var isPersonnel = shellViewModel.SelectedModule?.ViewModelType == typeof(PersonnelHubViewModel);
        var isResults = shellViewModel.SelectedModule?.ViewModelType == typeof(ResultsHubViewModel);
        var isDocuments = shellViewModel.SelectedModule?.ViewModelType == typeof(DocumentsHubViewModel);

        if (_settingsExpander is not null)
        {
            HighlightSettingsHeader(isSettings);
        }

        if (_financeExpander is not null)
        {
            HighlightFinanceHeader(isFinance);
            if (isFinance && string.IsNullOrWhiteSpace(_selectedFinanceKey))
            {
                NavigateToFinanceSection(shellViewModel, FinanceNavCatalog.DefaultItem);
            }
        }

        if (_personnelExpander is not null)
        {
            HighlightPersonnelHeader(isPersonnel);
            if (isPersonnel && string.IsNullOrWhiteSpace(_selectedPersonnelKey))
            {
                NavigateToPersonnelSection(shellViewModel, PersonnelNavCatalog.DefaultItem);
            }
        }

        if (_resultsExpander is not null)
        {
            HighlightResultsHeader(isResults);
            if (isResults && string.IsNullOrWhiteSpace(_selectedResultsKey))
            {
                NavigateToResultsSection(shellViewModel, ResultsNavCatalog.DefaultItem);
            }
        }

        if (_documentsExpander is not null)
        {
            HighlightDocumentsHeader(isDocuments);
            if (isDocuments && !string.IsNullOrWhiteSpace(_selectedDocumentsKey))
            {
                UpdateDocumentsSubNavSelection(_selectedDocumentsKey);
            }
        }
    }

    private void HighlightPersonnelHeader(bool active)
    {
        if (_personnelExpander is null)
        {
            return;
        }

        _personnelExpander.Background = active
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : Brushes.Transparent;
    }

    private void HighlightResultsHeader(bool active)
    {
        if (_resultsExpander is null)
        {
            return;
        }

        _resultsExpander.Background = active
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : Brushes.Transparent;
    }

    private void HighlightDocumentsHeader(bool active)
    {
        if (_documentsExpander is null)
        {
            return;
        }

        _documentsExpander.Background = active
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : Brushes.Transparent;
    }

    private void UpdatePersonnelSubNavSelection(string selectedKey) =>
        UpdateSubNavSelection(_personnelSubNavButtons, selectedKey);

    private void ClearPersonnelSubNavSelection() =>
        UpdateSubNavSelection(_personnelSubNavButtons, null);

    private void UpdateResultsSubNavSelection(string selectedKey) =>
        UpdateSubNavSelection(_resultsSubNavButtons, selectedKey);

    private void ClearResultsSubNavSelection() =>
        UpdateSubNavSelection(_resultsSubNavButtons, null);

    private void UpdateDocumentsSubNavSelection(string selectedKey) =>
        UpdateSubNavSelection(_documentsSubNavButtons, selectedKey);

    private void ClearDocumentsSubNavSelection() =>
        UpdateSubNavSelection(_documentsSubNavButtons, null);

    private void HighlightSettingsHeader(bool active)
    {
        if (_settingsExpander is null)
        {
            return;
        }

        _settingsExpander.Background = active
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : Brushes.Transparent;
    }

    private void HighlightFinanceHeader(bool active)
    {
        if (_financeExpander is null)
        {
            return;
        }

        _financeExpander.Background = active
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : Brushes.Transparent;
    }

    private void UpdateSettingsSubNavSelection(string selectedKey)
    {
        UpdateSubNavSelection(_settingsSubNavButtons, selectedKey);
    }

    private void UpdateFinanceSubNavSelection(string selectedKey)
    {
        UpdateSubNavSelection(_financeSubNavButtons, selectedKey);
    }

    private void ClearSettingsSubNavSelection() => UpdateSubNavSelection(_settingsSubNavButtons, null);

    private void ClearFinanceSubNavSelection() => UpdateSubNavSelection(_financeSubNavButtons, null);

    private static void UpdateSubNavSelection(Dictionary<string, Button> buttons, string? selectedKey)
    {
        foreach (var pair in buttons)
        {
            var isSelected = selectedKey is not null && pair.Key == selectedKey;
            pair.Value.Tag = isSelected ? "Selected" : null;

            if (pair.Value.Content is not StackPanel panel || panel.Children.Count < 2)
            {
                continue;
            }

            if (panel.Children[0] is PackIcon icon)
            {
                icon.Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(37, 99, 235) : Color.FromRgb(148, 163, 184));
            }

            if (panel.Children[1] is TextBlock text)
            {
                text.Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(255, 255, 255) : Color.FromRgb(203, 213, 225));
                text.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }
    }

    private void ApplyPendingSettingsSelection(ShellViewModel shellViewModel)
    {
        if (string.IsNullOrWhiteSpace(_selectedSettingsKey)
            || shellViewModel.CurrentViewModel is not SettingsViewModel settingsViewModel)
        {
            return;
        }

        var item = SettingsNavCatalog.FindByKey(_selectedSettingsKey);
        if (item is null)
        {
            return;
        }

        SettingsNavigationBridge.ApplyToViewModel(settingsViewModel, item);
        SettingsNavigationBridge.Select(item);
    }

    private void ApplyPendingFinanceSelection(ShellViewModel shellViewModel)
    {
        if (shellViewModel.CurrentViewModel is not FinanceHubViewModel financeViewModel)
        {
            return;
        }

        var key = _selectedFinanceKey ?? FinanceNavCatalog.DefaultItem.Key;
        var item = FinanceNavCatalog.FindByKey(key) ?? FinanceNavCatalog.DefaultItem;
        _selectedFinanceKey = item.Key;
        if (_financeExpander is not null)
        {
            _financeExpander.IsExpanded = true;
        }

        UpdateFinanceSubNavSelection(item.Key);
        FinanceNavigationBridge.ApplyToViewModel(financeViewModel, item);
        FinanceNavigationBridge.Select(item);
        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = item.Subtitle;
    }

    private void ApplyPendingPersonnelSelection(ShellViewModel shellViewModel)
    {
        if (shellViewModel.CurrentViewModel is not PersonnelHubViewModel personnelViewModel)
        {
            return;
        }

        var key = _selectedPersonnelKey ?? PersonnelNavCatalog.DefaultItem.Key;
        var item = PersonnelNavCatalog.FindByKey(key) ?? PersonnelNavCatalog.DefaultItem;
        _selectedPersonnelKey = item.Key;
        if (_personnelExpander is not null)
        {
            _personnelExpander.IsExpanded = true;
        }

        UpdatePersonnelSubNavSelection(item.Key);
        PersonnelNavigationBridge.ApplyToViewModel(personnelViewModel, item);
        PersonnelNavigationBridge.Select(item);
        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = item.Subtitle;
    }

    private void ApplyPendingResultsSelection(ShellViewModel shellViewModel)
    {
        if (shellViewModel.CurrentViewModel is not ResultsHubViewModel resultsViewModel)
        {
            return;
        }

        var key = _selectedResultsKey ?? ResultsNavCatalog.DefaultItem.Key;
        var item = ResultsNavCatalog.FindByKey(key) ?? ResultsNavCatalog.DefaultItem;
        _selectedResultsKey = item.Key;
        if (_resultsExpander is not null)
        {
            _resultsExpander.IsExpanded = true;
        }

        UpdateResultsSubNavSelection(item.Key);
        ResultsNavigationBridge.ApplyToViewModel(resultsViewModel, item);
        ResultsNavigationBridge.Select(item);
        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = item.Subtitle;
    }

    private void UpdatePageTitle(ShellViewModel shellViewModel)
    {
        if (shellViewModel.CurrentViewModel is SettingsViewModel settingsViewModel &&
            settingsViewModel.SelectedSettingsNode?.Section is not null)
        {
            PageTitleText.Text = settingsViewModel.SelectedSectionTitle;
            PageSubtitleText.Text = settingsViewModel.SelectedSectionDescription;
            return;
        }

        if (shellViewModel.CurrentViewModel is FinanceHubViewModel financeViewModel)
        {
            PageTitleText.Text = financeViewModel.SelectedSectionTitle;
            PageSubtitleText.Text = financeViewModel.SelectedSectionDescription;
            return;
        }

        if (shellViewModel.CurrentViewModel is PersonnelHubViewModel personnelViewModel)
        {
            PageTitleText.Text = personnelViewModel.SelectedSectionTitle;
            PageSubtitleText.Text = personnelViewModel.SelectedSectionDescription;
            return;
        }

        if (shellViewModel.CurrentViewModel is ResultsHubViewModel resultsViewModel)
        {
            PageTitleText.Text = resultsViewModel.SelectedSectionTitle;
            PageSubtitleText.Text = resultsViewModel.SelectedSectionDescription;
            return;
        }

        if (shellViewModel.CurrentViewModel is EnrollmentWizardViewModel wizardViewModel)
        {
            PageTitleText.Text = "Assistant d'inscription";
            PageSubtitleText.Text = wizardViewModel.IsReinscriptionMode
                ? "Réinscription d'un élève pour la nouvelle année scolaire"
                : "Création du dossier scolaire d'un nouvel élève";
            return;
        }

        if (shellViewModel.CurrentViewModel is GradesViewModel)
        {
            PageTitleText.Text = "Cotation des élèves";
            PageSubtitleText.Text = "Saisie des notes par évaluation";
            return;
        }

        if (shellViewModel.CurrentViewModel is SecurityUsersViewModel)
        {
            PageTitleText.Text = "Utilisateurs";
            PageSubtitleText.Text = GetDirectPageSubtitle("Security.Users");
            return;
        }

        if (shellViewModel.CurrentViewModel is SecurityRolesViewModel)
        {
            PageTitleText.Text = "Rôles";
            PageSubtitleText.Text = GetDirectPageSubtitle("Security.Roles");
            return;
        }

        if (shellViewModel.CurrentViewModel is SecurityExceptionsViewModel)
        {
            PageTitleText.Text = "Exceptions";
            PageSubtitleText.Text = GetDirectPageSubtitle("Security.Exceptions");
            return;
        }

        if (shellViewModel.CurrentViewModel is SecurityAuditViewModel)
        {
            PageTitleText.Text = "Audit sécurité";
            PageSubtitleText.Text = GetDirectPageSubtitle("Security.Audit");
            return;
        }

        if (shellViewModel.CurrentViewModel is PlatformCatalogViewModel)
        {
            PageTitleText.Text = "Catalogue de sécurité";
            PageSubtitleText.Text = GetDirectPageSubtitle("Platform.Catalog");
            return;
        }

        if (shellViewModel.CurrentViewModel is DocumentsViewModel)
        {
            PageTitleText.Text = "Documents élèves";
            PageSubtitleText.Text = GetDocumentsPageSubtitle("Documents.Main");
            return;
        }

        if (shellViewModel.CurrentViewModel is StudentCardsViewModel)
        {
            PageTitleText.Text = "Cartes élèves";
            PageSubtitleText.Text = GetDocumentsPageSubtitle("StudentCards.Main");
            return;
        }

        PageTitleText.Text = shellViewModel.SelectedModule?.Title ?? "Tableau de bord";
        PageSubtitleText.Text = "Gestion scolaire — République Démocratique du Congo";
    }

    private static string GetSettingsSubtitle(SettingsNavItem item)
    {
        if (item.IsPlaceholder)
        {
            return "Module en cours de préparation — disponible prochainement.";
        }

        return item.Key switch
        {
            "etablissement" => "Informations générales, logos, en-têtes, signatures et identité documentaire.",
            "structure-pedagogique" => "Activez uniquement les classes réellement organisées dans l'établissement.",
            "annees-scolaires" => "Créez les années scolaires et définissez l'année courante.",
            "matieres" => "Configurez les cours retenus par année, classe et salle, avec affectation des enseignants.",
            "utilisateurs" => "Gérez les comptes utilisateurs et l'affectation des rôles.",
            "enseignants" => "Gérez le personnel enseignant et leurs adresses.",
            "frais-scolaires" => "Configuration des frais par année, classe et type de frais.",
            "repartition-recettes" => "Destinations et clés de répartition des recettes.",
            "retenues" => "Configuration des retenues appliquées aux encaissements.",
            "sync-cloud" => "État et pilotage de la synchronisation Local → Cloud.",
            "reglement" => "Rédigez et enregistrez le règlement d'ordre intérieur.",
            _ => "Configuration de l'établissement scolaire."
        };
    }
}
