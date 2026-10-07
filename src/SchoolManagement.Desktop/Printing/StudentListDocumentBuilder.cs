using System.Globalization;
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
    private const int ColumnCount = 8;
    private const string UnassignedClass = "Classe non affectée";

    private static readonly FontFamily UiFont = new("Segoe UI");
    private static readonly Brush Navy = BrushFrom("#0B3D91");
    private static readonly Brush HeaderBg = BrushFrom("#123A7A");
    private static readonly Brush HeaderFg = Brushes.White;
    private static readonly Brush ClassBg = BrushFrom("#EAF2FF");
    private static readonly Brush SoftBlue = BrushFrom("#F5F8FF");
    private static readonly Brush Zebra = BrushFrom("#F8FAFC");
    private static readonly Brush BorderBrush = BrushFrom("#D8E2F2");
    private static readonly Brush TextDark = BrushFrom("#0F172A");
    private static readonly Brush TextMuted = BrushFrom("#64748B");
    private static readonly Brush Success = BrushFrom("#15803D");
    private static readonly Brush Warning = BrushFrom("#B45309");
    private static readonly Brush Danger = BrushFrom("#B91C1C");

    public static FlowDocument Build(
        string title,
        string subtitle,
        IReadOnlyList<StudentDto> students,
        CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("fr-FR");
        var groups = students
            .GroupBy(student => NormalizeClassName(student.CurrentYearClassName))
            .OrderBy(group => group.Key == UnassignedClass ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => new
            {
                ClassName = group.Key,
                Students = group
                    .OrderBy(student => student.LastName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(student => student.MiddleName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(student => student.FirstName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList()
            })
            .ToList();

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
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = Navy,
            Margin = new Thickness(0, 0, 0, 3)
        });

        document.Blocks.Add(new Paragraph(new Run(subtitle))
        {
            FontSize = 10.5,
            Foreground = TextMuted,
            Margin = new Thickness(0, 0, 0, 10)
        });

        document.Blocks.Add(CreateSummary(students, groups.Count, culture));

        var table = new Table { CellSpacing = 0 };
        var columns = new[] { 30d, 96d, 145d, 88d, 44d, 82d, 74d, 115d };
        var total = columns.Sum();
        foreach (var weight in columns)
        {
            table.Columns.Add(new TableColumn
            {
                Width = new GridLength(weight / total, GridUnitType.Star)
            });
        }

        var rows = new TableRowGroup();
        table.RowGroups.Add(rows);

        foreach (var group in groups)
        {
            rows.Rows.Add(CreateClassHeaderRow(group.ClassName, group.Students.Count));
            rows.Rows.Add(CreateHeaderRow(
                "N°", "MATRICULE", "NOM ET POSTNOM", "PRÉNOM", "SEXE", "NAISSANCE", "STATUT", "OBSERVATION"));

            for (var index = 0; index < group.Students.Count; index++)
            {
                var student = group.Students[index];
                rows.Rows.Add(CreateDataRow(
                    alternate: index % 2 == 1,
                    index: (index + 1).ToString(culture),
                    registrationNumber: student.RegistrationNumber,
                    name: FormatName(student.LastName, student.MiddleName),
                    firstName: student.FirstName,
                    gender: FormatGender(student.Gender),
                    birthDate: student.DateOfBirth.ToString("dd/MM/yyyy", culture),
                    status: FormatStatus(student),
                    observation: student.WithdrawalReason ?? "—"));
            }

            rows.Rows.Add(CreateSubtotalRow(group.Students.Count));
        }

        document.Blocks.Add(table);
        document.Blocks.Add(new Paragraph(new Run(
            $"Total général : {students.Count} élève(s) dans {groups.Count} classe(s)  •  Imprimé le {DateTime.Now:dd/MM/yyyy à HH:mm}"))
        {
            FontSize = 9.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextMuted,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        });

        return document;
    }

    private static Block CreateSummary(IReadOnlyList<StudentDto> students, int classCount, CultureInfo culture)
    {
        var boys = students.Count(student => student.Gender == Gender.Masculin);
        var girls = students.Count(student => student.Gender == Gender.Feminin);
        var contentWidth = PageWidth - PageMargin * 2;

        var text = new TextBlock
        {
            FontFamily = UiFont,
            FontSize = 10,
            Foreground = TextDark
        };
        text.Inlines.Add(new Run($"{students.Count.ToString(culture)} élève(s)") { FontWeight = FontWeights.Bold, Foreground = Navy });
        text.Inlines.Add(new Run($"   •   {classCount.ToString(culture)} classe(s)   •   "));
        text.Inlines.Add(new Run($"{boys.ToString(culture)} garçon(s)") { FontWeight = FontWeights.SemiBold });
        text.Inlines.Add(new Run("   •   "));
        text.Inlines.Add(new Run($"{girls.ToString(culture)} fille(s)") { FontWeight = FontWeights.SemiBold });

        return new BlockUIContainer(new Border
        {
            Width = contentWidth,
            Background = SoftBlue,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 12),
            Child = text
        });
    }

    private static TableRow CreateClassHeaderRow(string className, int count)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(9, 10, 9, 7),
            FontSize = 11.5,
            Foreground = Navy
        };
        paragraph.Inlines.Add(new Run(className.ToUpperInvariant()) { FontWeight = FontWeights.Bold });
        paragraph.Inlines.Add(new Run($"   —   {count} élève(s)") { FontSize = 10, Foreground = TextMuted });

        return new TableRow
        {
            Cells =
            {
                new TableCell(paragraph)
                {
                    ColumnSpan = ColumnCount,
                    Background = ClassBg,
                    BorderBrush = BorderBrush,
                    BorderThickness = new Thickness(1)
                }
            }
        };
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

    private static TableRow CreateDataRow(
        bool alternate,
        string index,
        string registrationNumber,
        string name,
        string firstName,
        string gender,
        string birthDate,
        string status,
        string observation)
    {
        var row = new TableRow { Background = alternate ? Zebra : Brushes.White };
        row.Cells.Add(CreateCell(index, TextMuted));
        row.Cells.Add(CreateCell(registrationNumber, Navy));
        row.Cells.Add(CreateCell(name, TextDark, bold: true));
        row.Cells.Add(CreateCell(firstName));
        row.Cells.Add(CreateCell(gender, alignment: TextAlignment.Center));
        row.Cells.Add(CreateCell(birthDate, alignment: TextAlignment.Center));
        row.Cells.Add(CreateCell(status, StatusBrush(status), bold: true));
        row.Cells.Add(CreateCell(observation, TextMuted));
        return row;
    }

    private static TableRow CreateSubtotalRow(int count)
    {
        return new TableRow
        {
            Cells =
            {
                new TableCell(new Paragraph(new Run($"Sous-total de la classe : {count} élève(s)"))
                {
                    Margin = new Thickness(6, 5, 8, 7),
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Navy,
                    TextAlignment = TextAlignment.Right
                })
                {
                    ColumnSpan = ColumnCount,
                    Background = SoftBlue,
                    BorderBrush = BorderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1)
                }
            }
        };
    }

    private static TableCell CreateCell(
        string text,
        Brush? foreground = null,
        bool isHeader = false,
        bool bold = false,
        TextAlignment alignment = TextAlignment.Left)
    {
        var paragraph = new Paragraph(new Run(text))
        {
            Margin = new Thickness(4, isHeader ? 5 : 4, 4, isHeader ? 5 : 4),
            Foreground = foreground ?? TextDark,
            FontSize = isHeader ? 8.5 : 8.8,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextAlignment = alignment
        };

        return new TableCell(paragraph)
        {
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(0, 0, 1, 1)
        };
    }

    private static string NormalizeClassName(string? className) =>
        string.IsNullOrWhiteSpace(className) ? UnassignedClass : className.Trim();

    private static string FormatName(string lastName, string? middleName) =>
        string.Join(" ", new[] { lastName, middleName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToUpperInvariant()));

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

    private static Brush StatusBrush(string status) =>
        status switch
        {
            "Inscrit" => Success,
            "Non inscrit" => Warning,
            _ => Danger
        };

    private static SolidColorBrush BrushFrom(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
        brush.Freeze();
        return brush;
    }
}
