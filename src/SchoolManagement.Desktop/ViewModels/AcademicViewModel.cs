using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolManagement.Application.Academic.DTOs;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;

namespace SchoolManagement.Desktop.ViewModels;

public partial class AcademicViewModel : ViewModelBase
{
    private readonly IAcademicApiService _academicApiService;
    private readonly ISchoolApiService _schoolApiService;
    private readonly IStudentApiService _studentApiService;

    public AcademicViewModel(
        IAcademicApiService academicApiService,
        ISchoolApiService schoolApiService,
        IStudentApiService studentApiService)
    {
        _academicApiService = academicApiService;
        _schoolApiService = schoolApiService;
        _studentApiService = studentApiService;
        AcademicYearRefreshBridge.CurrentYearChanged += OnGlobalAcademicYearChanged;
        _ = InitializeAsync();
    }

    private void OnGlobalAcademicYearChanged() => _ = LoadClassRoomsAsync();

    public ObservableCollection<ClassRoomDto> ClassRooms { get; } = [];
    public ObservableCollection<CourseDto> Courses { get; } = [];
    public ObservableCollection<EnrollmentDto> Enrollments { get; } = [];
    public ObservableCollection<StudentDto> Students { get; } = [];

    [ObservableProperty] private IReadOnlyList<SectionDto> _sections = [];
    [ObservableProperty] private SectionDto? _selectedSection;
    [ObservableProperty] private ClassRoomDto? _selectedClass;
    [ObservableProperty] private StudentDto? _selectedStudent;
    [ObservableProperty] private string _newClassCode = string.Empty;
    [ObservableProperty] private string _newClassName = string.Empty;
    [ObservableProperty] private int _newClassLevel = 1;
    [ObservableProperty] private string _newCourseCode = string.Empty;
    [ObservableProperty] private string _newCourseName = string.Empty;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    partial void OnSelectedClassChanged(ClassRoomDto? value) => _ = LoadCoursesAndEnrollmentsAsync();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            Sections = await _academicApiService.GetSectionsAsync();
            SelectedSection = Sections.FirstOrDefault();

            var students = await _studentApiService.SearchAsync(new StudentSearchRequest(
                null, null, null, null, null, null,
                ApplyFilters: false, IncludeAll: true, Page: 1, PageSize: 200));
            Students.Clear();
            foreach (var s in students.Items) Students.Add(s);
            SelectedStudent = Students.FirstOrDefault();

            await LoadClassRoomsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadClassRoomsAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null)
        {
            ClassRooms.Clear();
            Courses.Clear();
            Enrollments.Clear();
            StatusMessage = "Aucune année scolaire sélectionnée (barre du haut).";
            return;
        }

        IsBusy = true;
        try
        {
            var items = await _academicApiService.GetClassRoomsAsync(yearId.Value);
            ClassRooms.Clear();
            foreach (var c in items) ClassRooms.Add(c);
            SelectedClass = ClassRooms.FirstOrDefault();
            if (SelectedClass is null)
            {
                Courses.Clear();
                Enrollments.Clear();
            }
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadCoursesAndEnrollmentsAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (SelectedClass is null || yearId is null) return;
        IsBusy = true;
        try
        {
            var courses = await _academicApiService.GetCoursesAsync(SelectedClass.Id);
            Courses.Clear();
            foreach (var c in courses) Courses.Add(c);

            var enrollments = await _academicApiService.GetEnrollmentsAsync(SelectedClass.Id, yearId.Value);
            Enrollments.Clear();
            foreach (var e in enrollments) Enrollments.Add(e);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateClassRoomAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (yearId is null || SelectedSection is null || string.IsNullOrWhiteSpace(NewClassCode) || string.IsNullOrWhiteSpace(NewClassName))
        {
            StatusMessage = "Complétez l'année (barre du haut), la section, le code et le nom de la classe.";
            return;
        }

        IsBusy = true;
        try
        {
            await _academicApiService.CreateClassRoomAsync(new CreateClassRoomRequest(
                yearId.Value, SelectedSection.Id, NewClassCode, NewClassName, NewClassLevel, 40));
            NewClassCode = string.Empty;
            NewClassName = string.Empty;
            StatusMessage = "Classe créée.";
            await LoadClassRoomsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateCourseAsync()
    {
        if (SelectedClass is null || string.IsNullOrWhiteSpace(NewCourseCode) || string.IsNullOrWhiteSpace(NewCourseName))
        {
            StatusMessage = "Sélectionnez une classe et renseignez le cours.";
            return;
        }

        IsBusy = true;
        try
        {
            await _academicApiService.CreateCourseAsync(new CreateCourseRequest(
                SelectedClass.Id, NewCourseCode, NewCourseName, 1, 20));
            NewCourseCode = string.Empty;
            NewCourseName = string.Empty;
            StatusMessage = "Cours créé.";
            await LoadCoursesAndEnrollmentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateEnrollmentAsync()
    {
        var yearId = AcademicYearRefreshBridge.SelectedYearId;
        if (SelectedStudent is null || SelectedClass is null || yearId is null)
        {
            StatusMessage = "Sélectionnez élève et classe (année via la barre du haut).";
            return;
        }

        IsBusy = true;
        try
        {
            await _academicApiService.CreateEnrollmentAsync(new CreateEnrollmentRequest(
                SelectedStudent.Id, yearId.Value, SelectedClass.Id, DateOnly.FromDateTime(DateTime.Today)));
            StatusMessage = "Inscription créée.";
            await LoadCoursesAndEnrollmentsAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}
