using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SchoolManagement.Application.Payments.DTOs;

namespace SchoolManagement.Desktop.Views.Encaissements;

/// <summary>Aperçu imprimable du relevé (état de sortie) — sans sélection de paiement.</summary>
public partial class FeeTypeStatementPreviewWindow : Window
{
    private readonly FeeTypeStatementDto _statement;
    private readonly Action<FeeTypeStatementDto> _print;
    private readonly Action<FeeTypeStatementDto> _exportPdf;

    public FeeTypeStatementPreviewWindow(
        FlowDocument document,
        FeeTypeStatementDto statement,
        Action<FeeTypeStatementDto> print,
        Action<FeeTypeStatementDto> exportPdf)
    {
        InitializeComponent();
        _statement = statement;
        _print = print;
        _exportPdf = exportPdf;

        Title = $"RELEVÉ DE {statement.FeeTypeName.Trim().ToUpperInvariant()} — {statement.StatementNumber}";
        SubtitleText.Text = $"{statement.StudentName} · {statement.ClassName} · {statement.AcademicYearLabel}";
        DocumentViewer.Document = document;
    }

    private void PrintButton_OnClick(object sender, RoutedEventArgs e) => _print(_statement);

    private void ExportPdfButton_OnClick(object sender, RoutedEventArgs e) => _exportPdf(_statement);

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
