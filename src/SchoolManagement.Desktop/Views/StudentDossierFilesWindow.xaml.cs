using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Views;

public partial class StudentDossierFilesWindow : Window
{
    private readonly IStudentDossierPathResolver _pathResolver;

    public StudentDossierFilesWindow(
        string studentFullName,
        IEnumerable<StudentDossierFileDto> files,
        IStudentDossierPathResolver pathResolver)
    {
        InitializeComponent();
        _pathResolver = pathResolver;
        Title = $"Dossier élève — {studentFullName}";
        StudentNameText.Text = studentFullName;
        FilesList.ItemsSource = new ObservableCollection<StudentDossierFileDto>(files);
    }

    private void FilesList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FilesList.SelectedItem is StudentDossierFileDto file)
        {
            OpenFile(file);
        }
    }

    private void OpenSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (FilesList.SelectedItem is StudentDossierFileDto file)
        {
            OpenFile(file);
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void OpenFile(StudentDossierFileDto file)
    {
        var absolutePath = _pathResolver.ResolveAbsolutePath(file.StoragePath);
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            MessageBox.Show(
                $"Fichier introuvable :\n{file.FileName}",
                "Dossier élève",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo(absolutePath) { UseShellExecute = true });
    }
}
