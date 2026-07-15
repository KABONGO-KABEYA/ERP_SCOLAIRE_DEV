using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Documents.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class DocumentsViewModel : ViewModelBase
{
    private readonly IDocumentApiService _documentApiService;
    private readonly IStudentApiService _studentApiService;

    public DocumentsViewModel(IDocumentApiService documentApiService, IStudentApiService studentApiService)
    {
        _documentApiService = documentApiService;
        _studentApiService = studentApiService;
        _ = InitializeAsync();
    }

    public ObservableCollection<StudentDocumentDto> Documents { get; } = [];
    public ObservableCollection<StudentDto> Students { get; } = [];

    [ObservableProperty] private StudentDto? _selectedStudent;
    [ObservableProperty] private StudentDocumentDto? _selectedDocument;
    [ObservableProperty] private string _documentType = "Pièce d'identité";
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    partial void OnSelectedStudentChanged(StudentDto? value) => _ = LoadDocumentsAsync();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _studentApiService.SearchAsync(new StudentSearchRequest(
                null, null, null, null, null, null,
                ApplyFilters: false, IncludeAll: true, Page: 1, PageSize: 200));
            Students.Clear();
            foreach (var s in result.Items) Students.Add(s);
            SelectedStudent = Students.FirstOrDefault();
            await LoadDocumentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadDocumentsAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _documentApiService.ListAsync(SelectedStudent?.Id);
            Documents.Clear();
            foreach (var d in items) Documents.Add(d);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task UploadAsync()
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Sélectionnez un élève.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Sélectionner un document",
            Filter = "Tous les fichiers|*.*"
        };
        if (ErpFileDialog.ShowOpen(dialog, ErpFileDialog.ResolveOwnerWindow()) != true)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _documentApiService.UploadAsync(SelectedStudent.Id, DocumentType, dialog.FileName);
            StatusMessage = "Document téléversé.";
            await LoadDocumentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedDocument is null) return;
        IsBusy = true;
        try
        {
            await _documentApiService.DeleteAsync(SelectedDocument.Id);
            StatusMessage = "Document supprimé.";
            await LoadDocumentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        if (SelectedDocument is null) return;

        var dialog = new SaveFileDialog
        {
            FileName = SelectedDocument.FileName,
            Title = "Enregistrer le document"
        };
        if (ErpFileDialog.ShowSave(dialog, ErpFileDialog.ResolveOwnerWindow()) != true) return;

        IsBusy = true;
        try
        {
            await _documentApiService.DownloadAsync(SelectedDocument.Id, dialog.FileName);
            StatusMessage = "Document téléchargé.";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
