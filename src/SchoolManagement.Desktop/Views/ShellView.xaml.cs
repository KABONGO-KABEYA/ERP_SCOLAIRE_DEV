using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class ShellView : UserControl
{
    private readonly Dictionary<string, Button> _settingsSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _financeSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _personnelSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _resultsSubNavButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ToggleButton> _mainNavButtons = new();
    private Expander? _settingsExpander;
    private Expander? _financeExpander;
    private Expander? _personnelExpander;
    private Expander? _resultsExpander;
    private string? _selectedSettingsKey;
    private string? _selectedFinanceKey;
    private string? _selectedPersonnelKey;
    private string? _selectedResultsKey;
    private bool _isBuildingNavigation;

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

        BuildNavigation(shellViewModel);
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
        _mainNavButtons.Clear();

        foreach (var module in shellViewModel.Modules)
        {
            if (module.ViewModelType == typeof(SettingsViewModel))
            {
                _settingsExpander = CreateSettingsExpander(shellViewModel, module);
                NavigationPanel.Children.Add(_settingsExpander);
                continue;
            }

            if (module.ViewModelType == typeof(FinanceHubViewModel))
            {
                _financeExpander = CreateFinanceExpander(shellViewModel, module);
                NavigationPanel.Children.Add(_financeExpander);
                continue;
            }

            if (module.ViewModelType == typeof(PersonnelHubViewModel))
            {
                _personnelExpander = CreatePersonnelExpander(shellViewModel, module);
                NavigationPanel.Children.Add(_personnelExpander);
                continue;
            }

            if (module.ViewModelType == typeof(ResultsHubViewModel))
            {
                _resultsExpander = CreateResultsExpander(shellViewModel, module);
                NavigationPanel.Children.Add(_resultsExpander);
                continue;
            }

            var button = CreateMainNavButton(module.Title, module.IconKind);
            button.Click += (_, _) => NavigateToModule(shellViewModel, module.ViewModelType!, null);
            NavigationPanel.Children.Add(button);
            _mainNavButtons[module.ViewModelType!] = button;
        }

        _isBuildingNavigation = false;
    }

    private Expander CreateSettingsExpander(ShellViewModel shellViewModel, ModuleNavItem module)
    {
        var expander = new Expander
        {
            Style = (Style)FindResource("ErpSidebarSettingsExpander"),
            IsExpanded = false
        };

        expander.Header = CreateExpanderHeader(module.Title, PackIconKind.Cog);
        var content = new StackPanel();
        foreach (var group in SettingsNavCatalog.Groups)
        {
            content.Children.Add(new TextBlock
            {
                Text = group.Title,
                Style = (Style)FindResource("ErpSidebarSubNavGroupTitle")
            });

            foreach (var item in group.Items)
            {
                var subButton = CreateSubNavButton(item.Key, item.Title, item.IconKind);
                subButton.Click += (_, _) => NavigateToSettingsSection(shellViewModel, item);
                content.Children.Add(subButton);
                _settingsSubNavButtons[item.Key] = subButton;
            }
        }

        expander.Content = content;
        return expander;
    }

    private Expander CreateFinanceExpander(ShellViewModel shellViewModel, ModuleNavItem module)
    {
        var expander = new Expander
        {
            Style = (Style)FindResource("ErpSidebarSettingsExpander"),
            IsExpanded = false
        };

        expander.Header = CreateExpanderHeader(module.Title, PackIconKind.Cash);
        var content = new StackPanel();
        foreach (var group in FinanceNavCatalog.Groups)
        {
            content.Children.Add(new TextBlock
            {
                Text = group.Title,
                Style = (Style)FindResource("ErpSidebarSubNavGroupTitle")
            });

            foreach (var item in group.Items)
            {
                var subButton = CreateSubNavButton(item.Key, item.Title, item.IconKind);
                subButton.Click += (_, _) => NavigateToFinanceSection(shellViewModel, item);
                content.Children.Add(subButton);
                _financeSubNavButtons[item.Key] = subButton;
            }
        }

        expander.Content = content;
        return expander;
    }

    private Expander CreatePersonnelExpander(ShellViewModel shellViewModel, ModuleNavItem module)
    {
        var expander = new Expander
        {
            Style = (Style)FindResource("ErpSidebarSettingsExpander"),
            IsExpanded = false
        };

        expander.Header = CreateExpanderHeader(module.Title, PackIconKind.AccountTie);
        var content = new StackPanel();
        foreach (var group in PersonnelNavCatalog.Groups)
        {
            content.Children.Add(new TextBlock
            {
                Text = group.Title,
                Style = (Style)FindResource("ErpSidebarSubNavGroupTitle")
            });

            foreach (var item in group.Items)
            {
                var subButton = CreateSubNavButton(item.Key, item.Title, item.IconKind);
                subButton.Click += (_, _) => NavigateToPersonnelSection(shellViewModel, item);
                content.Children.Add(subButton);
                _personnelSubNavButtons[item.Key] = subButton;
            }
        }

        expander.Content = content;
        return expander;
    }

    private Expander CreateResultsExpander(ShellViewModel shellViewModel, ModuleNavItem module)
    {
        var expander = new Expander
        {
            Style = (Style)FindResource("ErpSidebarSettingsExpander"),
            IsExpanded = false
        };

        expander.Header = CreateExpanderHeader(module.Title, PackIconKind.SchoolOutline);
        var content = new StackPanel();
        foreach (var group in ResultsNavCatalog.Groups)
        {
            content.Children.Add(new TextBlock
            {
                Text = group.Title,
                Style = (Style)FindResource("ErpSidebarSubNavGroupTitle")
            });

            foreach (var item in group.Items)
            {
                var subButton = CreateSubNavButton(item.Key, item.Title, item.IconKind);
                subButton.Click += (_, _) => NavigateToResultsSection(shellViewModel, item);
                content.Children.Add(subButton);
                _resultsSubNavButtons[item.Key] = subButton;
            }
        }

        expander.Content = content;
        return expander;
    }

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

        shellViewModel.SelectedModule = module;

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
        var settingsModule = shellViewModel.Modules.First(module => module.ViewModelType == typeof(SettingsViewModel));
        shellViewModel.SelectedModule = settingsModule;
        _settingsExpander!.IsExpanded = true;
        _selectedSettingsKey = item.Key;
        _selectedFinanceKey = null;
        _selectedPersonnelKey = null;
        _selectedResultsKey = null;
        UpdateSettingsSubNavSelection(item.Key);
        ClearFinanceSubNavSelection();
        ClearPersonnelSubNavSelection();
        ClearResultsSubNavSelection();
        HighlightFinanceHeader(false);
        HighlightPersonnelHeader(false);
        HighlightResultsHeader(false);

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
        var financeModule = shellViewModel.Modules.First(module => module.ViewModelType == typeof(FinanceHubViewModel));
        shellViewModel.SelectedModule = financeModule;
        _financeExpander!.IsExpanded = true;
        _selectedFinanceKey = item.Key;
        _selectedSettingsKey = null;
        _selectedPersonnelKey = null;
        _selectedResultsKey = null;
        UpdateFinanceSubNavSelection(item.Key);
        ClearSettingsSubNavSelection();
        ClearPersonnelSubNavSelection();
        ClearResultsSubNavSelection();
        HighlightSettingsHeader(false);
        HighlightPersonnelHeader(false);
        HighlightResultsHeader(false);

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
        var personnelModule = shellViewModel.Modules.First(module => module.ViewModelType == typeof(PersonnelHubViewModel));
        shellViewModel.SelectedModule = personnelModule;
        _personnelExpander!.IsExpanded = true;
        _selectedPersonnelKey = item.Key;
        _selectedSettingsKey = null;
        _selectedFinanceKey = null;
        _selectedResultsKey = null;
        UpdatePersonnelSubNavSelection(item.Key);
        ClearSettingsSubNavSelection();
        ClearFinanceSubNavSelection();
        ClearResultsSubNavSelection();
        HighlightSettingsHeader(false);
        HighlightFinanceHeader(false);
        HighlightResultsHeader(false);
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
        var resultsModule = shellViewModel.Modules.First(module => module.ViewModelType == typeof(ResultsHubViewModel));
        shellViewModel.SelectedModule = resultsModule;
        _resultsExpander!.IsExpanded = true;
        _selectedResultsKey = item.Key;
        _selectedSettingsKey = null;
        _selectedFinanceKey = null;
        _selectedPersonnelKey = null;
        UpdateResultsSubNavSelection(item.Key);
        ClearSettingsSubNavSelection();
        ClearFinanceSubNavSelection();
        ClearPersonnelSubNavSelection();
        HighlightSettingsHeader(false);
        HighlightFinanceHeader(false);
        HighlightPersonnelHeader(false);
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

    private void UpdatePersonnelSubNavSelection(string selectedKey) =>
        UpdateSubNavSelection(_personnelSubNavButtons, selectedKey);

    private void ClearPersonnelSubNavSelection() =>
        UpdateSubNavSelection(_personnelSubNavButtons, null);

    private void UpdateResultsSubNavSelection(string selectedKey) =>
        UpdateSubNavSelection(_resultsSubNavButtons, selectedKey);

    private void ClearResultsSubNavSelection() =>
        UpdateSubNavSelection(_resultsSubNavButtons, null);

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
