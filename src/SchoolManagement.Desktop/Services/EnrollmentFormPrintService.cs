using System.Printing;
using System.Windows.Controls;
using System.Windows.Documents;
using SchoolManagement.Desktop.Printing;
using SchoolManagement.Desktop.Services;

namespace SchoolManagement.Desktop.Services;

public sealed class EnrollmentFormPrintService : IEnrollmentFormPrintService
{
    private readonly IEnrollmentWizardApiService _wizardApi;
    private readonly IDocumentBrandingPathResolver _brandingPathResolver;
    private readonly IStudentDossierPathResolver _dossierPathResolver;

    public EnrollmentFormPrintService(
        IEnrollmentWizardApiService wizardApi,
        IDocumentBrandingPathResolver brandingPathResolver,
        IStudentDossierPathResolver dossierPathResolver)
    {
        _wizardApi = wizardApi;
        _brandingPathResolver = brandingPathResolver;
        _dossierPathResolver = dossierPathResolver;
    }

    public async Task PrintAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
    {
        var form = await _wizardApi.GetEnrollmentFormAsync(enrollmentId, cancellationToken);
        var document = EnrollmentFormDocumentBuilder.Build(form, _brandingPathResolver, _dossierPathResolver);

        var printDialog = new PrintDialog();
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;

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

        printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Fiche d'inscription");
    }
}

public interface IEnrollmentFormPrintService
{
    Task PrintAsync(Guid enrollmentId, CancellationToken cancellationToken = default);
}
