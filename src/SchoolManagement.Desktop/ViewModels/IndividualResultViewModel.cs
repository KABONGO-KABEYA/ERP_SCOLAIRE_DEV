using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SchoolManagement.Application.Grades.DTOs;
using SchoolManagement.Desktop.Services;
using SchoolManagement.Desktop.UI;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.ViewModels;

public partial class IndividualResultViewModel : ViewModelBase
{
    private readonly IGradeApiService _gradeApi;
    private readonly IStudentDossierPathResolver _dossierPathResolver;

    public IndividualResultViewModel(
        IGradeApiService gradeApi,
        IStudentDossierPathResolver dossierPathResolver)
    {
        _gradeApi = gradeApi;
        _dossierPathResolver = dossierPathResolver;
    }

    public ObservableCollection<IndividualResultCourseRowDto> Courses { get; } = [];

    [ObservableProperty] private bool _hasData;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _emptyHint =
        "Double-cliquez un élève dans « Résultats par classe » ou utilisez Consulter.";

    [ObservableProperty] private string _studentName = string.Empty;
    [ObservableProperty] private string _registrationNumber = string.Empty;
    [ObservableProperty] private string _classDisplayName = string.Empty;
    [ObservableProperty] private string _periodLabel = string.Empty;
    [ObservableProperty] private string _academicYearLabel = string.Empty;
    [ObservableProperty] private BitmapImage? _photoSource;
    [ObservableProperty] private bool _hasPhoto;

    [ObservableProperty] private string _averageDisplay = "—";
    [ObservableProperty] private string _percentageDisplay = "—";
    [ObservableProperty] private string? _mention;
    [ObservableProperty] private string _decisionLabel = "—";
    [ObservableProperty] private string _rankDisplay = "—";
    [ObservableProperty] private ClassCouncilDecision _decision = ClassCouncilDecision.EnAttente;

    public Brush DecisionBrush => Decision switch
    {
        ClassCouncilDecision.Admis => new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)),
        ClassCouncilDecision.Ajourne => new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)),
        ClassCouncilDecision.Exclu => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
        _ => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80))
    };

    public void ShowEmptyHint()
    {
        HasData = false;
        StatusMessage = null;
        Courses.Clear();
        PhotoSource = null;
        HasPhoto = false;
    }

    public async Task LoadAsync(IndividualResultNavRequest request)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var dto = await _gradeApi.GetIndividualResultAsync(
                request.AcademicYearId,
                request.ClassRoomId,
                request.StudentId,
                request.Mode,
                request.PeriodId);

            ApplyDto(dto);
            HasData = true;
        }
        catch (Exception ex)
        {
            HasData = false;
            StatusMessage = ex.Message;
            Courses.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        var item = ResultsNavCatalog.FindByKey("par-classe") ?? ResultsNavCatalog.DefaultItem;
        ResultsNavigationBridge.Select(item);
    }

    [RelayCommand]
    private void Print()
    {
        if (!HasData)
        {
            StatusMessage = "Aucun résultat à imprimer.";
            return;
        }

        try
        {
            var document = BuildPrintDocument();
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return;
            }

            printDialog.PrintDocument(
                ((IDocumentPaginatorSource)document).DocumentPaginator,
                "Résultat individuel");
            StatusMessage = "Impression envoyée.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ExportPdf()
    {
        if (!HasData)
        {
            StatusMessage = "Aucun résultat à exporter.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "HTML pour PDF (*.html)|*.html",
            FileName = $"Resultat_{RegistrationNumber}_{StudentName}.html".Replace(' ', '_')
        };
        ErpFileDialog.PrepareSave(dialog);
        if (ErpFileDialog.ShowSave(dialog) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, BuildHtmlDocument(), Encoding.UTF8);
            StatusMessage = "Fichier HTML exporté — ouvrez-le et imprimez en PDF.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void ApplyDto(IndividualResultDto dto)
    {
        StudentName = dto.StudentName;
        RegistrationNumber = dto.RegistrationNumber;
        ClassDisplayName = dto.ClassDisplayName;
        PeriodLabel = dto.SelectedPeriodLabel;
        AcademicYearLabel = dto.AcademicYearLabel;
        AverageDisplay = dto.AverageDisplay;
        PercentageDisplay = dto.PercentageDisplay;
        Mention = dto.Mention;
        Decision = dto.Decision;
        DecisionLabel = dto.DecisionLabel;
        RankDisplay = dto.RankDisplay;
        OnPropertyChanged(nameof(DecisionBrush));

        Courses.Clear();
        foreach (var course in dto.Courses)
        {
            Courses.Add(course);
        }

        PhotoSource = ResolvePhoto(dto.PhotoPath);
        HasPhoto = PhotoSource is not null;
    }

    private BitmapImage? ResolvePhoto(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return null;
        }

        try
        {
            var absolute = Path.IsPathRooted(photoPath) && File.Exists(photoPath)
                ? photoPath
                : _dossierPathResolver.ResolveAbsolutePath(photoPath);

            if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private FlowDocument BuildPrintDocument()
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 11
        };

        doc.Blocks.Add(new Paragraph(new Run("Résultat individuel"))
        {
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        doc.Blocks.Add(new Paragraph(new Run(
            $"{StudentName} — {RegistrationNumber} | {ClassDisplayName} | {PeriodLabel} ({AcademicYearLabel})"))
        {
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        for (var i = 0; i < 6; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        table.RowGroups.Add(new TableRowGroup());
        var header = new TableRow();
        foreach (var h in new[] { "Cours", "Maximum", "Total obtenu", "Résultat", "Mention", "Observation" })
        {
            header.Cells.Add(new TableCell(new Paragraph(new Run(h)))
            {
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(4),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(0, 0, 0, 1)
            });
        }

        table.RowGroups[0].Rows.Add(header);

        foreach (var course in Courses)
        {
            var tr = new TableRow();
            foreach (var text in new[]
                     {
                         course.CourseName,
                         course.MaximumDisplay,
                         course.TotalObtainedDisplay,
                         course.ResultDisplay,
                         course.Mention ?? "—",
                         string.IsNullOrWhiteSpace(course.Observation) ? "—" : course.Observation
                     })
            {
                tr.Cells.Add(new TableCell(new Paragraph(new Run(text)))
                {
                    Padding = new Thickness(4),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });
            }

            table.RowGroups[0].Rows.Add(tr);
        }

        doc.Blocks.Add(table);
        doc.Blocks.Add(new Paragraph(new Run(
            $"Moyenne : {AverageDisplay}  |  % : {PercentageDisplay}  |  Mention : {Mention ?? "—"}  |  Décision : {DecisionLabel}  |  Rang : {RankDisplay}"))
        {
            Margin = new Thickness(0, 16, 0, 0),
            FontWeight = FontWeights.SemiBold
        });

        return doc;
    }

    private string BuildHtmlDocument()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>Résultat individuel</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;padding:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #cbd5e1;padding:8px;text-align:left}th{background:#0f1f4a;color:#fff}.kpi{display:flex;gap:16px;margin-top:16px}.kpi div{background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:12px}</style></head><body>");
        sb.AppendLine("<h1>Résultat individuel</h1>");
        sb.Append("<p><strong>").Append(System.Net.WebUtility.HtmlEncode(StudentName)).Append("</strong> — ")
            .Append(System.Net.WebUtility.HtmlEncode(RegistrationNumber)).Append("<br/>")
            .Append(System.Net.WebUtility.HtmlEncode(ClassDisplayName)).Append(" · ")
            .Append(System.Net.WebUtility.HtmlEncode(PeriodLabel)).Append(" (")
            .Append(System.Net.WebUtility.HtmlEncode(AcademicYearLabel)).AppendLine(")</p>");
        sb.AppendLine("<table><thead><tr><th>Cours</th><th>Maximum</th><th>Total obtenu</th><th>Résultat</th><th>Mention</th><th>Observation</th></tr></thead><tbody>");
        foreach (var course in Courses)
        {
            sb.Append("<tr>");
            void Td(string? v) => sb.Append("<td>").Append(System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(v) ? "—" : v)).Append("</td>");
            Td(course.CourseName);
            Td(course.MaximumDisplay);
            Td(course.TotalObtainedDisplay);
            Td(course.ResultDisplay);
            Td(course.Mention);
            Td(course.Observation);
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<div class=\"kpi\">");
        sb.Append("<div>Moyenne<br/><strong>").Append(System.Net.WebUtility.HtmlEncode(AverageDisplay)).Append("</strong></div>");
        sb.Append("<div>Pourcentage<br/><strong>").Append(System.Net.WebUtility.HtmlEncode(PercentageDisplay)).Append("</strong></div>");
        sb.Append("<div>Mention<br/><strong>").Append(System.Net.WebUtility.HtmlEncode(Mention ?? "—")).Append("</strong></div>");
        sb.Append("<div>Décision<br/><strong>").Append(System.Net.WebUtility.HtmlEncode(DecisionLabel)).Append("</strong></div>");
        sb.Append("<div>Rang<br/><strong>").Append(System.Net.WebUtility.HtmlEncode(RankDisplay)).Append("</strong></div>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    partial void OnDecisionChanged(ClassCouncilDecision value) => OnPropertyChanged(nameof(DecisionBrush));
}
