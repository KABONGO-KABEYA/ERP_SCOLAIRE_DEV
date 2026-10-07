using SchoolManagement.Desktop.Printing;
namespace SchoolManagement.Desktop.UI;
public static class ErpPdfPrintPrompt
{
    public static bool PrintOrPreview(string path) { DocumentPreview.ShowPdf(path, "Document PDF"); return false; }
    public static bool AskAndPrintIfRequested(string path, string? title = null) { DocumentPreview.ShowPdf(path, title ?? "Document PDF"); return false; }
}
