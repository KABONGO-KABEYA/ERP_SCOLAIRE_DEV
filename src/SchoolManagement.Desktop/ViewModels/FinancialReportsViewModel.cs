using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolManagement.Desktop.ViewModels;

/// <summary>Rapports financiers — écran placeholder (phase UI).</summary>
public partial class FinancialReportsViewModel : ViewModelBase
{
    [ObservableProperty] private string _statusMessage =
        "Les rapports financiers (encaissements, soldes, répartitions, retenues) seront développés dans une prochaine étape.";
}
