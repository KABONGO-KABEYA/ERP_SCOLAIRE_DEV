using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.Printing;

public static class StudentListDocumentBuilder
{
    private const double PageWidth = 794;
    private const double PageHeight = 1123;
    private const double PageMargin = 40;
    private static readonly FontFamily UiFont = new("Segoe UI");
    private static readonly Brush HeaderBg = new SolidColorBrush(Color.FromRgb(30, 94, 255));
    private static readonly Brush HeaderFg = Brushes.White;
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));

    public static FlowDocument Build(
        string title,
        string subtitle,
        IReadOnlyList<StudentDto> students,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("fr-FR");
        var document = new FlowDocument
        {
            PageWidth = PageWidth,
            PageHeight = PageHeight,
            PagePadding = new Thickness(PageMargin),
            FontFamily = UiFont,
            FontSize = 10,
            ColumnWidth = double.PositiveInfinity
        };

        document.Blocks.Add(new Paragraph(new Run(title))
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        });

        document.Blocks.Add(new Paragraph(new Run(subtitle))
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        var columns = new[] { 32d, 90d, 120d, 120d, 130d, 70d, 72d, 80d, 1.6 };
        var total = columns.Sum();
        foreach (var weight in columns)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(weight / total, GridUnitType.Star) });
        }

        var headerGroup = new TableRowGroup();
        headerGroup.Rows.Add(CreateHeaderRow(
            "#", "Matricule", "Nom", "Prénom", "Classe", "Sexe", "Naissance", "État", "Raison"));
        table.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        var index = 1;
        foreach (var student in students)
        {
            bodyGroup.Rows.Add(CreateDataRow(
                index.ToString(culture),
                student.RegistrationNumber,
                student.LastName,
                student.FirstName,
                student.CurrentYearClassName ?? "—",
                FormatGender(student.Gender),
                student.DateOfBirth.ToString("dd/MM/yyyy", culture),
                FormatStatus(student),
                student.WithdrawalReason ?? "—"));
            index++;
        }

        table.RowGroups.Add(bodyGroup);
        document.Blocks.Add(table);

        document.Blocks.Add(new Paragraph(new Run($"Total : {students.Count} élève(s) — Imprimé le {DateTime.Now:dd/MM/yyyy HH:mm}"))
        {
            FontSize = 9,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 12, 0, 0)
        });

        return document;
    }

    private static TableRow CreateHeaderRow(params string[] headers)
    {
        var row = new TableRow { Background = HeaderBg, FontWeight = FontWeights.SemiBold };
        foreach (var header in headers)
        {
            row.Cells.Add(CreateCell(header, HeaderFg, isHeader: true));
        }

        return row;
    }

    private static TableRow CreateDataRow(params string[] values)
    {
        var row = new TableRow();
        foreach (var value in values)
        {
            row.Cells.Add(CreateCell(value));
        }

        return row;
    }

    private static TableCell CreateCell(string text, Brush? foreground = null, bool isHeader = false)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            Margin = new Thickness(4, 3, 4, 3),
            Foreground = foreground ?? Brushes.Black,
            FontSize = isHeader ? 9 : 8.5
        };

        return new TableCell(paragraph)
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
    }

    private static string FormatGender(Gender gender) =>
        gender switch
        {
            Gender.Masculin => "M",
            Gender.Feminin => "F",
            _ => "—"
        };

    private static string FormatStatus(StudentDto student)
    {
        if (student.IsArchived)
        {
            return "Archivé";
        }

        if (student.CurrentYearStatus == EnrollmentStatus.Exclusion)
        {
            return "Exclu";
        }

        if (student.CurrentYearStatus == EnrollmentStatus.Abandon)
        {
            return "Abandonné";
        }

        return student.IsEnrolledCurrentYear ? "Inscrit" : "Non inscrit";
    }
}
