using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Views;

public partial class PedagogicalStructureWizardControl : UserControl
{
    private SettingsViewModel? _viewModel;
    private string? _selectedSectionKey;
    private string? _selectedOptionKey;
    private StructureDisplayFilter _displayFilter = StructureDisplayFilter.All;
    private string _searchText = string.Empty;
    private readonly Dictionary<string, Border> _sectionCards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Border> _optionCards = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, Border> _classRowCards = new();

    public PedagogicalStructureWizardControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel();
        FilterAllChip.IsChecked = true;
        PreventAutoScroll(SectionsScrollViewer);
        PreventAutoScroll(MiddleScrollViewer);
        PreventAutoScroll(RightScrollViewer);
        PreventAutoScroll(LocalsListBox);
        EnsureAllClassesLoaded();
        RefreshAll();
    }

    private static void PreventAutoScroll(FrameworkElement element)
    {
        element.AddHandler(FrameworkElement.RequestBringIntoViewEvent,
            new RequestBringIntoViewEventHandler((_, args) => args.Handled = true));
    }

    private void AttachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PedagogicalClasses.CollectionChanged -= OnClassesChanged;
            _viewModel.ClassLocals.CollectionChanged -= OnClassLocalsChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PedagogicalClasses.CollectionChanged += OnClassesChanged;
        _viewModel.ClassLocals.CollectionChanged += OnClassLocalsChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _searchText = _viewModel.ClassSearch?.Trim() ?? string.Empty;

        EnsureAllClassesLoaded();
        RefreshAll();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SettingsViewModel.SelectedPedagogicalClass))
        {
            UpdateClassRowHighlights();
            UpdateRightPanel();
            return;
        }

        if (args.PropertyName is nameof(SettingsViewModel.ClassSearch))
        {
            _searchText = _viewModel?.ClassSearch?.Trim() ?? string.Empty;
            var sectionsOffset = SectionsScrollViewer.VerticalOffset;
            RenderSections();
            SectionsScrollViewer.ScrollToVerticalOffset(sectionsOffset);

            var middleOffset = MiddleScrollViewer.VerticalOffset;
            RenderMiddlePanel();
            MiddleScrollViewer.ScrollToVerticalOffset(middleOffset);
        }
    }

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshAll();

    private void OnClassLocalsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateRightPanel();

    private void EnsureAllClassesLoaded()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.SelectedProgramFilter = _viewModel.ProgramFilters.FirstOrDefault();
        _viewModel.ClassSearch = string.Empty;
        if (_viewModel.SearchClassesCommand.CanExecute(null))
        {
            _viewModel.SearchClassesCommand.Execute(null);
        }
    }

    private void RefreshAll()
    {
        if (_viewModel is null)
        {
            return;
        }

        RenderStats();
        RenderSections();
        RenderMiddlePanel();
        UpdateRightPanel();
    }


    private void RenderStats()
    {
        StatsPanel.Children.Clear();
        if (_viewModel is null)
        {
            return;
        }

        var classes = _viewModel.PedagogicalClasses.ToList();
        var options = classes
            .Select(c => c.StudyOption)
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        AddStatCard("Sections", StructureUiCatalog.Sections.Count.ToString(), "#1E5EFF");
        AddStatCard("Options", options.ToString(), "#8B5CF6");
        AddStatCard("Classes RDC", classes.Count.ToString(), "#6B7280");
        AddStatCard("Activées", classes.Count(c => c.IsEnabled).ToString(), "#22C55E");
        AddStatCard("Locaux", classes.Sum(c => c.LocalCount).ToString(), "#F59E0B");
        var progress = classes.Count == 0
            ? 0
            : (int)Math.Round(classes.Count(c => c.IsEnabled) * 100d / classes.Count);
        AddStatCard("Progression", $"{progress}%", "#14B8A6");
    }

    private void AddStatCard(string label, string value, string accent)
    {
        var card = new Border { Style = (Style)FindResource("ErpWizardStatCard") };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
        });
        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            Text = value,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)new BrushConverter().ConvertFromString(accent)!
        });
        card.Child = stack;
        StatsPanel.Children.Add(card);
    }

    private void RenderSections()
    {
        var scrollOffset = SectionsScrollViewer.VerticalOffset;
        SectionsPanel.Children.Clear();
        _sectionCards.Clear();

        foreach (var section in StructureUiCatalog.Sections)
        {
            var classList = GetSectionClasses(section.Key).ToList();
            if (!MatchesSearch(section, classList))
            {
                continue;
            }

            var enabled = classList.Count(c => c.IsEnabled);
            var card = CreateSectionCard(section, classList.Count, enabled);
            card.MouseLeftButtonUp += (_, _) => SelectSection(section.Key);
            SectionsPanel.Children.Add(card);
            _sectionCards[section.Key] = card;
        }

        HighlightSectionCard(_selectedSectionKey);
        SectionsScrollViewer.ScrollToVerticalOffset(scrollOffset);
    }

    private Border CreateSectionCard(StructureUiSection section, int totalClasses, int enabledClasses)
    {
        var accent = (Color)ColorConverter.ConvertFromString(section.AccentColor)!;
        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            Background = Brushes.White,
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconHost = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B)),
            Child = new PackIcon
            {
                Kind = ParseIcon(section.IconKind),
                Width = 22,
                Height = 22,
                Foreground = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var textStack = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        textStack.Children.Add(new TextBlock
        {
            Text = section.Title,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55))
        });
        textStack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 0),
            Text = section.Description,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            TextWrapping = TextWrapping.Wrap
        });
        textStack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 0),
            Text = $"{totalClasses} classes",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = new SolidColorBrush(accent)
        });
        textStack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 0),
            Text = enabledClasses > 0 ? $"✔ {enabledClasses} classes activées" : "Aucune classe activée",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94))
        });

        Grid.SetColumn(iconHost, 0);
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(iconHost);
        grid.Children.Add(textStack);
        card.Child = grid;
        return card;
    }

    private void SelectSection(string sectionKey)
    {
        _selectedSectionKey = sectionKey;
        _selectedOptionKey = null;
        _viewModel!.SelectedPedagogicalClass = null;
        HighlightSectionCard(sectionKey);

        var middleOffset = MiddleScrollViewer.VerticalOffset;
        RenderMiddlePanel();
        MiddleScrollViewer.ScrollToVerticalOffset(middleOffset);
        UpdateRightPanel();
    }

    private void HighlightSectionCard(string? sectionKey)
    {
        foreach (var pair in _sectionCards)
        {
            pair.Value.BorderBrush = pair.Key == sectionKey
                ? new SolidColorBrush(Color.FromRgb(30, 94, 255))
                : new SolidColorBrush(Color.FromRgb(229, 231, 235));
            pair.Value.Background = pair.Key == sectionKey
                ? new SolidColorBrush(Color.FromRgb(232, 239, 255))
                : Brushes.White;
        }
    }

    private void RenderMiddlePanel()
    {
        var scrollOffset = MiddleScrollViewer.VerticalOffset;
        MiddlePanel.Children.Clear();
        _optionCards.Clear();
        _classRowCards.Clear();

        if (_viewModel is null || string.IsNullOrWhiteSpace(_selectedSectionKey))
        {
            MiddleTitleText.Text = "Options et classes";
            MiddleSubtitleText.Text = "Sélectionnez une section pour commencer.";
            MiddleScrollViewer.ScrollToVerticalOffset(scrollOffset);
            return;
        }

        var section = StructureUiCatalog.FindSection(_selectedSectionKey);
        if (section is null)
        {
            MiddleScrollViewer.ScrollToVerticalOffset(scrollOffset);
            return;
        }

        var classes = ApplyDisplayFilter(GetSectionClasses(section.Key)).ToList();
        MiddleTitleText.Text = section.Title;
        MiddleSubtitleText.Text = section.Description;

        var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var sectionToggle = CreateSectionMasterToggle(classes, section.Title);
        DockPanel.SetDock(sectionToggle, Dock.Right);
        headerRow.Children.Add(sectionToggle);
        headerRow.Children.Add(new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Text = "Activer toute la section",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
        });
        MiddlePanel.Children.Add(headerRow);

        if (StructureUiCatalog.SectionHasOptions(classes, section.Key))
        {
            foreach (var group in StructureUiCatalog.GroupByOption(classes, section.Key))
            {
                if (!MatchesSearch(group.Key, group))
                {
                    continue;
                }

                var optionCard = CreateOptionCard(section, group.Key, group.ToList());
                optionCard.MouseLeftButtonUp += (_, _) => SelectOption(group.Key);
                MiddlePanel.Children.Add(optionCard);
                _optionCards[group.Key] = optionCard;
            }

            if (!string.IsNullOrWhiteSpace(_selectedOptionKey))
            {
                RenderClassesForOption(classes.Where(c =>
                    StructureUiCatalog.GetOptionGroupKey(c, section.Key) == _selectedOptionKey));
            }
        }
        else
        {
            RenderClassesForOption(classes);
        }

        HighlightOptionCard(_selectedOptionKey);
        UpdateClassRowHighlights();
        MiddleScrollViewer.ScrollToVerticalOffset(scrollOffset);
    }

    private void SelectOption(string optionKey)
    {
        _selectedOptionKey = optionKey;
        HighlightOptionCard(optionKey);

        if (_viewModel is null || string.IsNullOrWhiteSpace(_selectedSectionKey))
        {
            return;
        }

        var scrollOffset = MiddleScrollViewer.VerticalOffset;
        var classes = ApplyDisplayFilter(GetSectionClasses(_selectedSectionKey))
            .Where(c => StructureUiCatalog.GetOptionGroupKey(c, _selectedSectionKey) == optionKey)
            .ToList();

        RemoveClassRowsFromMiddlePanel();
        RenderClassesForOption(classes);
        UpdateClassRowHighlights();
        MiddleScrollViewer.ScrollToVerticalOffset(scrollOffset);
    }

    private void RemoveClassRowsFromMiddlePanel()
    {
        var rowsToRemove = MiddlePanel.Children
            .OfType<Border>()
            .Where(b => b.Tag is PedagogicalClassItemViewModel)
            .ToList();

        foreach (var row in rowsToRemove)
        {
            MiddlePanel.Children.Remove(row);
        }

        var separatorsToRemove = MiddlePanel.Children
            .OfType<Border>()
            .Where(b => b.Height == 1)
            .ToList();

        foreach (var separator in separatorsToRemove)
        {
            MiddlePanel.Children.Remove(separator);
        }

        _classRowCards.Clear();
    }

    private Border CreateOptionCard(StructureUiSection section, string optionName, IReadOnlyList<PedagogicalClassItemViewModel> classes)
    {
        var accent = (Color)ColorConverter.ConvertFromString(section.AccentColor)!;
        var enabled = classes.Count(c => c.IsEnabled);
        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            Background = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
            Cursor = Cursors.Hand
        };

        var dock = new DockPanel();
        var toggle = CreateOptionMasterToggle(classes, optionName);
        DockPanel.SetDock(toggle, Dock.Right);
        dock.Children.Add(toggle);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = optionName,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55))
        });
        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 4, 0, 0),
            Text = $"{classes.Count} classes • {enabled} activées",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = new SolidColorBrush(accent)
        });
        dock.Children.Add(stack);
        card.Child = dock;
        return card;
    }

    private void HighlightOptionCard(string? optionKey)
    {
        foreach (var pair in _optionCards)
        {
            pair.Value.BorderBrush = pair.Key == optionKey
                ? new SolidColorBrush(Color.FromRgb(30, 94, 255))
                : new SolidColorBrush(Color.FromRgb(229, 231, 235));
            pair.Value.Background = pair.Key == optionKey
                ? new SolidColorBrush(Color.FromRgb(232, 239, 255))
                : new SolidColorBrush(Color.FromRgb(249, 250, 251));
        }
    }

    private void RenderClassesForOption(IEnumerable<PedagogicalClassItemViewModel> classes)
    {
        var list = classes.OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count == 0)
        {
            return;
        }

        MiddlePanel.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(229, 231, 235))
        });

        foreach (var item in list)
        {
            var row = CreateClassRow(item);
            MiddlePanel.Children.Add(row);
            _classRowCards[item.Id] = row;
        }
    }

    private Border CreateClassRow(PedagogicalClassItemViewModel item)
    {
        var row = new Border
        {
            Tag = item,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };

        ApplyClassRowHighlight(row, _viewModel?.SelectedPedagogicalClass?.Id == item.Id);

        row.MouseLeftButtonUp += (_, _) =>
        {
            if (_viewModel is null)
            {
                return;
            }

            _viewModel.SelectedPedagogicalClass = item;
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = item.DisplayName,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55)),
            TextWrapping = TextWrapping.Wrap
        });
        stack.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 0),
            Text = item.LocalCount > 0 ? $"{item.LocalCount} local(aux)" : "Aucun local",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128))
        });

        var toggle = new ToggleButton { Style = (Style)FindResource("ErpToggleSwitch") };
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(PedagogicalClassItemViewModel.IsEnabled))
        {
            Source = item,
            Mode = BindingMode.TwoWay
        });
        toggle.Click += (_, args) => args.Handled = true;

        Grid.SetColumn(stack, 0);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(stack);
        grid.Children.Add(toggle);
        row.Child = grid;
        return row;
    }

    private void UpdateClassRowHighlights()
    {
        var selectedId = _viewModel?.SelectedPedagogicalClass?.Id;
        foreach (var pair in _classRowCards)
        {
            ApplyClassRowHighlight(pair.Value, pair.Key == selectedId);
        }
    }

    private static void ApplyClassRowHighlight(Border row, bool isSelected)
    {
        row.BorderBrush = isSelected
            ? new SolidColorBrush(Color.FromRgb(30, 94, 255))
            : new SolidColorBrush(Color.FromRgb(229, 231, 235));
        row.Background = isSelected
            ? new SolidColorBrush(Color.FromRgb(232, 239, 255))
            : Brushes.White;
    }

    private ToggleButton CreateSectionMasterToggle(IReadOnlyList<PedagogicalClassItemViewModel> classes, string label)
    {
        var toggle = new ToggleButton { Style = (Style)FindResource("ErpToggleSwitch") };
        toggle.IsChecked = classes.Count > 0 && classes.All(c => c.IsEnabled);
        toggle.Checked += (_, _) => SetClassesEnabled(classes, true);
        toggle.Unchecked += (_, _) => SetClassesEnabled(classes, false);
        toggle.ToolTip = $"Activer ou désactiver toute la section {label}";
        return toggle;
    }

    private ToggleButton CreateOptionMasterToggle(IReadOnlyList<PedagogicalClassItemViewModel> classes, string label)
    {
        var toggle = new ToggleButton { Style = (Style)FindResource("ErpToggleSwitch") };
        toggle.IsChecked = classes.Count > 0 && classes.All(c => c.IsEnabled);
        toggle.Checked += (_, _) => SetClassesEnabled(classes, true);
        toggle.Unchecked += (_, _) => SetClassesEnabled(classes, false);
        toggle.Click += (_, args) => args.Handled = true;
        toggle.ToolTip = $"Activer ou désactiver l'option {label}";
        return toggle;
    }

    private static void SetClassesEnabled(IEnumerable<PedagogicalClassItemViewModel> classes, bool enabled)
    {
        foreach (var item in classes)
        {
            item.IsEnabled = enabled;
        }
    }

    private void UpdateRightPanel()
    {
        var selectedClass = _viewModel?.SelectedPedagogicalClass;
        if (selectedClass is null)
        {
            RightTitleText.Text = "Locaux";
            RightSubtitleText.Text = "Sélectionnez une classe pour gérer ses locaux.";
            RightEmptyText.Visibility = Visibility.Collapsed;
            LocalFormCard.Visibility = Visibility.Collapsed;
            return;
        }

        RightTitleText.Text = selectedClass.DisplayName;
        RightSubtitleText.Text = "Créez les locaux A, B, C, Salle 1… pour cette classe.";
        LocalFormCard.Visibility = Visibility.Visible;
        RightEmptyText.Visibility = _viewModel!.ClassLocals.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private IEnumerable<PedagogicalClassItemViewModel> GetSectionClasses(string sectionKey)
    {
        var section = StructureUiCatalog.FindSection(sectionKey);
        if (_viewModel is null || section is null)
        {
            return [];
        }

        return _viewModel.PedagogicalClasses.Where(section.Matches);
    }

    private IEnumerable<PedagogicalClassItemViewModel> ApplyDisplayFilter(IEnumerable<PedagogicalClassItemViewModel> classes) =>
        _displayFilter switch
        {
            StructureDisplayFilter.Enabled => classes.Where(c => c.IsEnabled),
            StructureDisplayFilter.Disabled => classes.Where(c => !c.IsEnabled),
            StructureDisplayFilter.WithoutLocals => classes.Where(c => c.LocalCount == 0),
            _ => classes
        };

    private bool MatchesSearch(StructureUiSection section, IEnumerable<PedagogicalClassItemViewModel> classes)
    {
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return classes.Any();
        }

        if (section.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || section.Description.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return classes.Any(c =>
            c.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            || (c.StudyOption?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false)
            || (c.HumanitiesSection?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static bool MatchesSearch(string optionName, IEnumerable<PedagogicalClassItemViewModel> classes) => true;

    private void FilterChip_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not ToggleButton clicked || clicked.IsChecked != true)
        {
            return;
        }

        if (ReferenceEquals(clicked, FilterAllChip))
        {
            _displayFilter = StructureDisplayFilter.All;
        }
        else if (ReferenceEquals(clicked, FilterEnabledChip))
        {
            _displayFilter = StructureDisplayFilter.Enabled;
        }
        else if (ReferenceEquals(clicked, FilterDisabledChip))
        {
            _displayFilter = StructureDisplayFilter.Disabled;
        }
        else if (ReferenceEquals(clicked, FilterNoLocalsChip))
        {
            _displayFilter = StructureDisplayFilter.WithoutLocals;
        }

        foreach (var chip in new[] { FilterAllChip, FilterEnabledChip, FilterDisabledChip, FilterNoLocalsChip })
        {
            if (chip is null || ReferenceEquals(chip, clicked))
            {
                continue;
            }

            chip.IsChecked = false;
        }

        var middleOffset = MiddleScrollViewer.VerticalOffset;
        RenderMiddlePanel();
        MiddleScrollViewer.ScrollToVerticalOffset(middleOffset);
    }

    private static PackIconKind ParseIcon(string iconKind) =>
        Enum.TryParse<PackIconKind>(iconKind, out var kind) ? kind : PackIconKind.Circle;
}
