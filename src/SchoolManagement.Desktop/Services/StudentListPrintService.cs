using System.Printing;
using System.Windows.Controls;
using System.Windows.Documents;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Desktop.Printing;

namespace SchoolManagement.Desktop.Services;

public interface IStudentListPrintService
{
    void Print(IReadOnlyList<StudentDto> students, string title, string subtitle);
}

public sealed class StudentListPrintService : IStudentListPrintService
{
    public void Print(IReadOnlyList<StudentDto> students, string title, string subtitle)
    {
        if (students.Count == 0)
        {
            return;
        }

        var document = StudentListDocumentBuilder.Build(title, subtitle, students);
        var printDialog = new PrintDialog();
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;

        try
        {
            var defaultQueue = LocalPrintServer.GetDefaultPrintQueue();
            if (defaultQueue is not null)
            {
                printDialog.PrintQueue = defaultQueue;
            }
        }
        catch
        {
            if (printDialog.ShowDialog() != true)
            {
                return;
            }
        }

        printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, title);
    }
}
