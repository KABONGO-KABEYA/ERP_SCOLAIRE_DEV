namespace SchoolManagement.Desktop.Models;

public sealed record AllocationCashFlowDailyRow(
    DateOnly Date,
    string DestinationCode,
    string DestinationName,
    decimal PeriodJ1,
    decimal Encaissement,
    decimal DepenseP,
    decimal PeriodeP);
