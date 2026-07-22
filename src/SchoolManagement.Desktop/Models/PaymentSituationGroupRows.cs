using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace SchoolManagement.Desktop.Models;

/// <summary>En-tête de colonne tranche pour le tableau croisé.</summary>
public sealed class PaymentSituationColumnHeader
{
    public required string Name { get; init; }
}

/// <summary>Cellule montant payé d'une tranche (ou non concernée).</summary>
public sealed class PaymentSituationAmountCell
{
    public required decimal Amount { get; init; }

    public required bool IsApplicable { get; init; }

    public string Display => IsApplicable ? Amount.ToString("N0") : string.Empty;

    public string ToolTip => IsApplicable ? Amount.ToString("N0") : "Tranche non applicable à cet élève";
}

/// <summary>Ligne élève du tableau croisé.</summary>
public sealed class PaymentSituationStudentRow
{
    public required string FullName { get; init; }

    public ObservableCollection<PaymentSituationAmountCell> InstallmentCells { get; init; } = [];

    public decimal AmountExpected { get; init; }

    public decimal Remaining { get; init; }

    public string ExpectedDisplay => AmountExpected.ToString("N0");

    public string RemainingDisplay => Remaining.ToString("N0");
}

/// <summary>Rupture de section pour le tableau croisé Situation des paiements.</summary>
public partial class PaymentSituationSectionGroup : ObservableObject
{
    public required string SectionName { get; init; }

    public ObservableCollection<PaymentSituationClassGroup> Classes { get; } = [];

    public decimal SectionRemaining { get; init; }

    public string SectionRemainingDisplay => SectionRemaining.ToString("N0");

    public int StudentCount { get; init; }

    [ObservableProperty] private bool _isExpanded = true;

    public PackIconKind ExpandIconKind => IsExpanded ? PackIconKind.ChevronDown : PackIconKind.ChevronRight;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandIconKind));

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}

/// <summary>Rupture de classe avec lignes élèves.</summary>
public partial class PaymentSituationClassGroup : ObservableObject
{
    public required string ClassName { get; init; }

    public ObservableCollection<PaymentSituationStudentRow> Students { get; } = [];

    public decimal ClassRemaining { get; init; }

    public string ClassRemainingDisplay => ClassRemaining.ToString("N0");

    public int StudentCount { get; init; }

    [ObservableProperty] private bool _isExpanded = true;

    public PackIconKind ExpandIconKind => IsExpanded ? PackIconKind.ChevronDown : PackIconKind.ChevronRight;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandIconKind));

    [RelayCommand]
    private void ToggleExpand() => IsExpanded = !IsExpanded;
}
