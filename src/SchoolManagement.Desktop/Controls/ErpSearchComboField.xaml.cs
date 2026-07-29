using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpSearchComboField : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(ErpSearchComboField),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(ErpSearchComboField),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(nameof(DisplayMemberPath), typeof(string), typeof(ErpSearchComboField),
            new PropertyMetadata(string.Empty, OnDisplayMemberPathChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ErpSearchComboField),
            new PropertyMetadata("Rechercher..."));

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(ErpSearchComboField),
            new PropertyMetadata(false));

    public static readonly DependencyProperty FilteredItemsProperty =
        DependencyProperty.Register(nameof(FilteredItems), typeof(IEnumerable), typeof(ErpSearchComboField),
            new PropertyMetadata(null));

    private INotifyCollectionChanged? _itemsSourceCollection;
    private bool _isUpdatingText;
    private bool _suppressSelectionSync;

    public ErpSearchComboField()
    {
        InitializeComponent();
        FilteredItems = Array.Empty<object>();
        Loaded += (_, _) => RefreshItems();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                RefreshItems();
            }
        };
        Unloaded += (_, _) => UnsubscribeCollectionChanged();
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public IEnumerable? FilteredItems
    {
        get => (IEnumerable?)GetValue(FilteredItemsProperty);
        private set => SetValue(FilteredItemsProperty, value);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpSearchComboField control)
        {
            control.UnsubscribeCollectionChanged();
            control._itemsSourceCollection = e.NewValue as INotifyCollectionChanged;
            control._itemsSourceCollection?.CollectionChanged += control.OnItemsCollectionChanged;
            control.ApplyFilter(string.Empty);
            control.EnsureSelectedItemInList();
            control.SyncSearchTextFromSelection();
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
        EnsureSelectedItemInList();
        SyncSearchTextFromSelection();
    }

    private void UnsubscribeCollectionChanged()
    {
        if (_itemsSourceCollection is not null)
        {
            _itemsSourceCollection.CollectionChanged -= OnItemsCollectionChanged;
            _itemsSourceCollection = null;
        }
    }

    private void EnsureSelectedItemInList()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        if (items.Contains(SelectedItem))
        {
            return;
        }

        var selectedId = GetItemId(SelectedItem);
        if (selectedId is null)
        {
            var unassigned = items.FirstOrDefault(item => GetItemId(item) is null);
            if (unassigned is not null)
            {
                _suppressSelectionSync = true;
                SelectedItem = unassigned;
                _suppressSelectionSync = false;
            }

            return;
        }

        var match = items.FirstOrDefault(item => GetItemId(item) == selectedId);
        if (match is not null)
        {
            _suppressSelectionSync = true;
            SelectedItem = match;
            _suppressSelectionSync = false;
        }
    }

    private void RefreshItems()
    {
        if (_itemsSourceCollection is null && ItemsSource is INotifyCollectionChanged collection)
        {
            _itemsSourceCollection = collection;
            _itemsSourceCollection.CollectionChanged += OnItemsCollectionChanged;
        }

        ApplyFilter(SearchBox.Text);
        EnsureSelectedItemInList();
        SyncSearchTextFromSelection();
    }

    private static Guid? GetItemId(object item)
    {
        var property = TypeDescriptor.GetProperties(item).Find("Id", ignoreCase: true);
        return property?.GetValue(item) as Guid?;
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpSearchComboField control && !control._suppressSelectionSync)
        {
            control.SyncSearchTextFromSelection();
        }
    }

    private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ErpSearchComboField control)
        {
            control.ApplyFilter(control.SearchBox.Text);
            control.SyncSearchTextFromSelection();
        }
    }

    private void SyncSearchTextFromSelection()
    {
        _isUpdatingText = true;
        SearchBox.Text = GetDisplayText(SelectedItem);
        _isUpdatingText = false;
    }

    private void ApplyFilter(string search)
    {
        var items = ItemsSource?.Cast<object>().ToList() ?? [];
        if (string.IsNullOrWhiteSpace(search))
        {
            FilteredItems = items;
            return;
        }

        FilteredItems = items
            .Where(item => GetDisplayText(item).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private string GetDisplayText(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
        {
            var property = TypeDescriptor.GetProperties(item).Find(DisplayMemberPath, ignoreCase: true);
            if (property?.GetValue(item) is { } value)
            {
                return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
            }
        }

        return item.ToString() ?? string.Empty;
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingText)
        {
            return;
        }

        ApplyFilter(SearchBox.Text);
        IsDropDownOpen = true;
    }

    private void SearchBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        ApplyFilter(SearchBox.Text);
        IsDropDownOpen = true;
    }

    private void SearchBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (!ItemsList.IsKeyboardFocusWithin && !DropDownPopup.IsMouseOver)
        {
            SyncSearchTextFromSelection();
        }
    }

    private void ToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        IsDropDownOpen = !IsDropDownOpen;
        if (IsDropDownOpen)
        {
            SearchBox.Focus();
            ApplyFilter(SearchBox.Text);
        }
    }

    private void ItemsList_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        CommitSelection();
    }

    private void ItemsList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            IsDropDownOpen = false;
            SyncSearchTextFromSelection();
            e.Handled = true;
        }
    }

    private void SearchBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && IsDropDownOpen)
        {
            ItemsList.Focus();
            if (ItemsList.Items.Count > 0)
            {
                ItemsList.SelectedIndex = 0;
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && IsDropDownOpen)
        {
            var first = FilteredItems?.Cast<object>().FirstOrDefault();
            if (first is not null)
            {
                _suppressSelectionSync = true;
                SelectedItem = first;
                _suppressSelectionSync = false;
                CommitSelection();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            IsDropDownOpen = false;
            SyncSearchTextFromSelection();
            e.Handled = true;
        }
    }

    private void CommitSelection()
    {
        SyncSearchTextFromSelection();
        IsDropDownOpen = false;
    }
}
