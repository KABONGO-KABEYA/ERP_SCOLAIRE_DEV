using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SchoolManagement.Application.Parent;

public static class ParentBulletinPdfGenerator
{
    public static byte[] Generate(
        string schoolName,
        string studentName,
        string registrationNumber,
        string className,
        string periodName,
        decimal average,
        decimal percentage,
        int rank,
        int classSize,
        string mention,
        string decision,
        string? appreciation)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text(schoolName).Bold().FontSize(16).AlignCenter();
                    col.Item().PaddingTop(4).Text("BULLETIN SCOLAIRE").SemiBold().FontSize(14).AlignCenter();
                    col.Item().PaddingTop(2).Text(periodName).AlignCenter().FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Élève : {studentName}");
                    col.Item().Text($"Matricule : {registrationNumber}");
                    col.Item().Text($"Classe : {className}");
                    col.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    col.Item().PaddingTop(12).Row(row =>
                    {
                        row.RelativeItem().Text($"Moyenne : {average:0.##}");
                        row.RelativeItem().Text($"Pourcentage : {percentage:0.##} %");
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Rang : {rank}/{Math.Max(classSize, 1)}");
                        row.RelativeItem().Text($"Mention : {mention}");
                    });
                    col.Item().Text($"Décision du conseil : {decision}");

                    if (!string.IsNullOrWhiteSpace(appreciation))
                    {
                        col.Item().PaddingTop(10).Text("Appréciation").SemiBold();
                        col.Item().Text(appreciation);
                    }
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("Document généré le ");
                    txt.Span($"{DateTime.Now:dd/MM/yyyy HH:mm}");
                });
            });
        }).GeneratePdf();
    }
}
