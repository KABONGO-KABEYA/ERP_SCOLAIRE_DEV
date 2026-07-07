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
    private readonly Dictionary<Type, ToggleButton> _mainNavButtons = new();
    private Expander? _settingsExpander;
    private string? _selectedSettingsKey;
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
                UpdatePageTitle(shellViewModel);
            }
        };

        SyncMainNavSelection(shellViewModel);
        UpdatePageTitle(shellViewModel);
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
        _mainNavButtons.Clear();

        foreach (var module in shellViewModel.Modules)
        {
            if (module.ViewModelType == typeof(SettingsViewModel))
            {
                _settingsExpander = CreateSettingsExpander(shellViewModel, module);
                NavigationPanel.Children.Add(_settingsExpander);
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

        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new PackIcon
        {
            Kind = PackIconKind.Cog,
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
            Text = module.Title
        });
        expander.Header = header;

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
                var subButton = CreateSettingsSubNavButton(item);
                subButton.Click += (_, _) => NavigateToSettingsSection(shellViewModel, item);
                content.Children.Add(subButton);
                _settingsSubNavButtons[item.Key] = subButton;
            }
        }

        expander.Content = content;
        return expander;
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

    private Button CreateSettingsSubNavButton(SettingsNavItem item)
    {
        var button = new Button
        {
            Style = (Style)FindResource("ErpSidebarSubNavButton"),
            Tag = item.Key
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new PackIcon
        {
            Kind = Enum.TryParse<PackIconKind>(item.IconKind, out var kind) ? kind : PackIconKind.Circle,
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
            Text = item.Title,
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
        UpdateSettingsSubNavSelection(item.Key);
        PageTitleText.Text = item.Title;
        PageSubtitleText.Text = GetSettingsSubtitle(item);
        SettingsNavigationBridge.Select(item);
    }

    private void SyncMainNavSelection(ShellViewModel shellViewModel)
    {
        foreach (var pair in _mainNavButtons)
        {
            pair.Value.IsChecked = shellViewModel.SelectedModule?.ViewModelType == pair.Key;
        }

        var isSettings = shellViewModel.SelectedModule?.ViewModelType == typeof(SettingsViewModel);
        if (_settingsExpander is not null)
        {
            if (isSettings)
            {
                HighlightSettingsHeader(true);
            }
            else
            {
                HighlightSettingsHeader(false);
            }
        }
    }

    private void HighlightSettingsHeader(bool active)
    {
        if (_settingsExpander?.Header is not StackPanel header)
        {
            return;
        }

        var background = active ? new SolidColorBrush(Color.FromRgb(30, 94, 255)) : Brushes.Transparent;
        _settingsExpander.Background = background;
    }

    private void UpdateSettingsSubNavSelection(string selectedKey)
    {
        foreach (var pair in _settingsSubNavButtons)
        {
            var isSelected = pair.Key == selectedKey;
            pair.Value.Tag = isSelected ? "Selected" : null;

            if (pair.Value.Content is StackPanel panel && panel.Children.Count >= 2)
            {
                if (panel.Children[0] is PackIcon icon)
                {
                    icon.Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(30, 94, 255) : Color.FromRgb(148, 163, 184));
                }

                if (panel.Children[1] is TextBlock text)
                {
                    text.Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(30, 94, 255) : Color.FromRgb(203, 213, 225));
                    text.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
                }
            }
        }
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
            "etablissement" => "Informations générales de l'établissement.",
            "structure-pedagogique" => "Activez uniquement les classes réellement organisées dans l'établissement.",
            "annees-scolaires" => "Créez les années scolaires et définissez l'année courante.",
            "matieres" => "Gérez les matières rattachées aux classes actives.",
            "utilisateurs" => "Gérez les comptes utilisateurs et l'affectation des rôles.",
            "frais-scolaires" => "Consultez les types de frais disponibles pour les paiements scolaires.",
            "reglement" => "Rédigez et enregistrez le règlement d'ordre intérieur.",
            _ => "Configuration de l'établissement scolaire."
        };
    }
}
