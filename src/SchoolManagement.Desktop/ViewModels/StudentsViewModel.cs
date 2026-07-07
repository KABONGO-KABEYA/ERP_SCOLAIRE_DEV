using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class StudentsViewModel : ViewModelBase
{
    private readonly IStudentApiService _studentApiService;
    private readonly INavigationService _navigationService;

    public StudentsViewModel(IStudentApiService studentApiService, INavigationService navigationService)
    {
        _studentApiService = studentApiService;
        _navigationService = navigationService;
        _ = SearchAsync();
    }

    public ObservableCollection<StudentDto> Students { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private StudentDto? _selectedStudent;

    [RelayCommand]
    private async Task SearchAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _studentApiService.SearchAsync(new StudentSearchRequest(SearchText, null, 1, 50));
            Students.Clear();
            foreach (var student in result.Items)
            {
                Students.Add(student);
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

    [RelayCommand]
    private void OpenEnrollmentWizard()
    {
        EnrollmentWizardNavigationBridge.Request(EnrollmentWizardEntryMode.NouvelleInscription);
        _navigationService.NavigateTo<EnrollmentWizardViewModel>();
    }

    [RelayCommand]
    private void OpenReinscriptionWizard()
    {
        EnrollmentWizardNavigationBridge.Request(EnrollmentWizardEntryMode.Reinscription);
        _navigationService.NavigateTo<EnrollmentWizardViewModel>();
    }

    [RelayCommand]
    private async Task ArchiveSelectedAsync()
    {
        if (SelectedStudent is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _studentApiService.ArchiveAsync(SelectedStudent.Id);
            StatusMessage = $"Élève {SelectedStudent.LastName} archivé.";
            await SearchAsync();
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
}
