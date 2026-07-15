using System.IO;
using System.Windows;
using System.Windows.Input;
using SchoolManagement.Desktop.ViewModels;

namespace SchoolManagement.Desktop.Controls;

public partial class DocumentBrandingControl
{
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    public DocumentBrandingControl()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not DocumentBrandingViewModel viewModel)
        {
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var file = files.FirstOrDefault(f =>
            AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));
        if (file is null)
        {
            viewModel.ValidationMessage = "Format non autorisé. Utilisez PNG, JPG, JPEG ou BMP.";
            return;
        }

        viewModel.ImportDroppedImage(file, viewModel.SelectedTabIndex);
        e.Handled = true;
    }
}
