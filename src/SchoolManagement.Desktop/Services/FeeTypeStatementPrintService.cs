using System.Diagnostics;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using SchoolManagement.Application.Payments.DTOs;
using SchoolManagement.Desktop.Printing;
using SchoolManagement.Desktop.Views.Encaissements;

namespace SchoolManagement.Desktop.Services;

public interface IFeeTypeStatementPrintService
{
    Task PrintAsync(Guid paymentId, Guid? feeTypeId = null, CancellationToken cancellationToken = default);

    Task PreviewForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task PrintForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task ExportPdfAsync(Guid paymentId, Guid? feeTypeId = null, CancellationToken cancellationToken = default);

    Task ExportPdfForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);

    Task<FeeTypeStatementDto> LoadAsync(Guid paymentId, Guid? feeTypeId = null, CancellationToken cancellationToken = default);

    Task<FeeTypeStatementDto> LoadForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default);
}

public sealed class FeeTypeStatementPrintService : IFeeTypeStatementPrintService
{
    private readonly IPaymentApiService _paymentApi;
    private readonly IDocumentBrandingPathResolver _brandingPathResolver;

    public FeeTypeStatementPrintService(
        IPaymentApiService paymentApi,
        IDocumentBrandingPathResolver brandingPathResolver)
    {
        _paymentApi = paymentApi;
        _brandingPathResolver = brandingPathResolver;
    }

    public Task<FeeTypeStatementDto> LoadAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default) =>
        _paymentApi.GetFeeTypeStatementAsync(paymentId, feeTypeId, cancellationToken);

    public Task<FeeTypeStatementDto> LoadForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default) =>
        _paymentApi.GetFeeTypeStatementForStudentAsync(studentId, academicYearId, feeTypeId, cancellationToken);

    public async Task PreviewForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var statement = await LoadForStudentAsync(studentId, academicYearId, feeTypeId, cancellationToken);
        var document = FeeTypeStatementDocumentBuilder.Build(statement, _brandingPathResolver);

        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Application WPF indisponible.");

        await dispatcher.InvokeAsync(() =>
        {
            var preview = new FeeTypeStatementPreviewWindow(document, statement, PrintDocument, ExportDocumentPdf)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            preview.ShowDialog();
        });
    }

    public async Task PrintForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var statement = await LoadForStudentAsync(studentId, academicYearId, feeTypeId, cancellationToken);
        PrintDocument(statement);
    }

    public async Task PrintAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var statement = await LoadAsync(paymentId, feeTypeId, cancellationToken);
        PrintDocument(statement);
    }

    public async Task ExportPdfAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var statement = await LoadAsync(paymentId, feeTypeId, cancellationToken);
        await ExportDocumentPdfAsync(statement, cancellationToken);
    }

    public async Task ExportPdfForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var statement = await LoadForStudentAsync(studentId, academicYearId, feeTypeId, cancellationToken);
        await ExportDocumentPdfAsync(statement, cancellationToken);
    }

    private void PrintDocument(FeeTypeStatementDto statement)
    {
        var printDialog = new PrintDialog();
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA5);
        printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;

        if (printDialog.ShowDialog() != true)
        {
            return;
        }

        var document = FeeTypeStatementDocumentBuilder.Build(
            statement,
            _brandingPathResolver,
            printDialog.PrintableAreaWidth,
            printDialog.PrintableAreaHeight);

        printDialog.PrintDocument(
            ((IDocumentPaginatorSource)document).DocumentPaginator,
            $"Relevé {statement.FeeTypeName} — {statement.StatementNumber}");
    }

    private void ExportDocumentPdf(FeeTypeStatementDto statement) =>
        _ = ExportDocumentPdfAsync(statement);

    private async Task ExportDocumentPdfAsync(
        FeeTypeStatementDto statement,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        if (statement.PaymentId != Guid.Empty)
        {
            bytes = await _paymentApi.ExportFeeTypeStatementPdfAsync(
                statement.PaymentId, statement.FeeTypeId, cancellationToken);
        }
        else
        {
            bytes = await _paymentApi.ExportFeeTypeStatementPdfForStudentAsync(
                statement.StudentId, statement.AcademicYearId, statement.FeeTypeId, cancellationToken);
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"releve-{SanitizeFileName(statement.FeeTypeName)}-{statement.StatementNumber}.pdf",
            DefaultExt = ".pdf"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await File.WriteAllBytesAsync(dialog.FileName, bytes, cancellationToken);

        try
        {
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch
        {
            MessageBox.Show(
                $"PDF enregistré :\n{dialog.FileName}",
                "Export PDF",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '-');
        }

        return value.Trim();
    }
}
