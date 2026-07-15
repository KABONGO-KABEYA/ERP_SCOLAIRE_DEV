using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpAddressControl : UserControl
{
    public static readonly DependencyProperty EditorProperty =
        DependencyProperty.Register(nameof(Editor), typeof(AddressEditorViewModel), typeof(ErpAddressControl),
            new PropertyMetadata(null));

    public AddressEditorViewModel? Editor
    {
        get => (AddressEditorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public ErpAddressControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ProvinceField.PreparingDropDownAsync += OnPrepareProvinceDropDownAsync;
        CityField.PreparingDropDownAsync += OnPrepareCityDropDownAsync;
        CommuneField.PreparingDropDownAsync += OnPrepareCommuneDropDownAsync;

        ProvinceField.DropDownOpened += OnProvinceDropDownOpened;
        CityField.DropDownOpened += OnCityDropDownOpened;
        CommuneField.DropDownOpened += OnCommuneDropDownOpened;
    }

    private async void OnProvinceDropDownOpened(object? sender, EventArgs e)
    {
        if (Editor is not null)
        {
            await Editor.EnsureProvincesLoadedAsync();
        }
    }

    private async void OnCityDropDownOpened(object? sender, EventArgs e)
    {
        if (Editor is not null)
        {
            await Editor.EnsureCitiesLoadedAsync();
        }
    }

    private async void OnCommuneDropDownOpened(object? sender, EventArgs e)
    {
        if (Editor is not null)
        {
            await Editor.EnsureCommunesLoadedAsync();
        }
    }

    private async Task OnPrepareProvinceDropDownAsync(EventArgs e)
    {
        if (Editor is null)
        {
            return;
        }

        await Editor.EnsureProvincesLoadedAsync();
    }

    private async Task OnPrepareCityDropDownAsync(EventArgs e)
    {
        if (Editor is null)
        {
            return;
        }

        await Editor.EnsureCitiesLoadedAsync();
    }

    private async Task OnPrepareCommuneDropDownAsync(EventArgs e)
    {
        if (Editor is null)
        {
            return;
        }

        await Editor.EnsureCommunesLoadedAsync();
    }
}
