using System.Windows;
using System.Windows.Controls;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Controls;

public partial class ErpAddressControl : UserControl
{
    private bool _dropDownHooksRegistered;

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
        if (_dropDownHooksRegistered)
        {
            return;
        }

        _dropDownHooksRegistered = true;
        ProvinceField.PreparingDropDownAsync += OnPrepareProvinceDropDownAsync;
        CityField.PreparingDropDownAsync += OnPrepareCityDropDownAsync;
        CommuneField.PreparingDropDownAsync += OnPrepareCommuneDropDownAsync;
    }

    private Task OnPrepareProvinceDropDownAsync(EventArgs e) =>
        Editor?.EnsureProvincesLoadedAsync() ?? Task.CompletedTask;

    private Task OnPrepareCityDropDownAsync(EventArgs e) =>
        Editor?.EnsureCitiesLoadedAsync() ?? Task.CompletedTask;

    private Task OnPrepareCommuneDropDownAsync(EventArgs e) =>
        Editor?.EnsureCommunesLoadedAsync() ?? Task.CompletedTask;
}
