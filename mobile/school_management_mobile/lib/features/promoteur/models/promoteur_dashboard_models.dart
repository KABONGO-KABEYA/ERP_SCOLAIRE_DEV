class PromoterDashboardOverview {
  const PromoterDashboardOverview({
    required this.schoolName,
    this.schoolLogoUrl,
    required this.currency,
    required this.period,
    required this.generatedAtUtc,
    required this.selectedFeeTypeId,
    required this.selectedFeeTypeName,
    required this.availableFeeTypes,
    required this.kpis,
    required this.dailyRevenueLast30Days,
    required this.monthlyRevenueSchoolYear,
    required this.expenses,
    required this.fundAllocations,
    required this.withholdings,
    required this.situation,
    required this.receivables,
    required this.alerts,
    required this.summary,
    required this.revenueSeries,
    required this.feeTypeShares,
    required this.recentActivities,
    required this.topClasses,
    required this.topFeeTypes,
    required this.quickStats,
  });

  final String schoolName;
  final String? schoolLogoUrl;
  final String currency;
  final String period;
  final DateTime generatedAtUtc;
  final String? selectedFeeTypeId;
  final String selectedFeeTypeName;
  final List<DashboardFeeTypeOption> availableFeeTypes;
  final PromoterKpiBoard kpis;
  final List<RevenuePoint> dailyRevenueLast30Days;
  final List<RevenuePoint> monthlyRevenueSchoolYear;
  final PromoterExpensesBoard expenses;
  final List<FundAllocationShare> fundAllocations;
  final List<PromoterWithholdingShare> withholdings;
  final PromoterSituation situation;
  final PromoterReceivables receivables;
  final List<DashboardAlert> alerts;
  final PromoterFinancialSummary summary;
  final List<RevenuePoint> revenueSeries;
  final List<NamedAmountShare> feeTypeShares;
  final List<DashboardActivity> recentActivities;
  final List<ClassRevenueRank> topClasses;
  final List<NamedAmountShare> topFeeTypes;
  final PromoterQuickStats quickStats;

  factory PromoterDashboardOverview.fromJson(Map<String, dynamic> json) =>
      PromoterDashboardOverview(
        schoolName: json['schoolName'] as String? ?? 'Établissement',
        schoolLogoUrl: json['schoolLogoUrl'] as String?,
        currency: json['currency'] as String? ?? 'CDF',
        period: json['period'] as String? ?? 'Month',
        generatedAtUtc: DateTime.tryParse(json['generatedAtUtc']?.toString() ?? '') ?? DateTime.now().toUtc(),
        selectedFeeTypeId: json['selectedFeeTypeId']?.toString(),
        selectedFeeTypeName: json['selectedFeeTypeName'] as String? ?? 'Frais',
        availableFeeTypes: _mapList(json['availableFeeTypes'], DashboardFeeTypeOption.fromJson),
        kpis: PromoterKpiBoard.fromJson(Map<String, dynamic>.from(json['kpis'] as Map? ?? {})),
        dailyRevenueLast30Days: _mapList(json['dailyRevenueLast30Days'], RevenuePoint.fromJson),
        monthlyRevenueSchoolYear: _mapList(json['monthlyRevenueSchoolYear'], RevenuePoint.fromJson),
        expenses: PromoterExpensesBoard.fromJson(Map<String, dynamic>.from(json['expenses'] as Map? ?? {})),
        fundAllocations: _mapList(json['fundAllocations'], FundAllocationShare.fromJson),
        withholdings: _mapList(json['withholdings'], PromoterWithholdingShare.fromJson),
        situation: PromoterSituation.fromJson(Map<String, dynamic>.from(json['situation'] as Map? ?? {})),
        receivables: PromoterReceivables.fromJson(Map<String, dynamic>.from(json['receivables'] as Map? ?? {})),
        alerts: _mapList(json['alerts'], DashboardAlert.fromJson),
        summary: PromoterFinancialSummary.fromJson(Map<String, dynamic>.from(json['summary'] as Map? ?? {})),
        revenueSeries: _mapList(json['revenueSeries'], RevenuePoint.fromJson),
        feeTypeShares: _mapList(json['feeTypeShares'], NamedAmountShare.fromJson),
        recentActivities: _mapList(json['recentActivities'], DashboardActivity.fromJson),
        topClasses: _mapList(json['topClasses'], ClassRevenueRank.fromJson),
        topFeeTypes: _mapList(json['topFeeTypes'], NamedAmountShare.fromJson),
        quickStats: PromoterQuickStats.fromJson(Map<String, dynamic>.from(json['quickStats'] as Map? ?? {})),
      );
}

class DashboardFeeTypeOption {
  const DashboardFeeTypeOption({
    required this.id,
    required this.name,
    required this.currency,
  });

  final String id;
  final String name;
  final String currency;

  factory DashboardFeeTypeOption.fromJson(Map<String, dynamic> json) => DashboardFeeTypeOption(
        id: json['id']?.toString() ?? '',
        name: json['name'] as String? ?? '',
        currency: json['currency'] as String? ?? 'CDF',
      );
}

class PromoterKpiBoard {
  const PromoterKpiBoard({
    required this.todayRevenue,
    required this.monthRevenue,
    required this.yearRevenue,
    required this.students,
  });

  final PromoterMoneyKpi todayRevenue;
  final PromoterMoneyKpi monthRevenue;
  final PromoterMoneyKpi yearRevenue;
  final PromoterStudentsKpi students;

  factory PromoterKpiBoard.fromJson(Map<String, dynamic> json) => PromoterKpiBoard(
        todayRevenue: PromoterMoneyKpi.fromJson(Map<String, dynamic>.from(json['todayRevenue'] as Map? ?? {})),
        monthRevenue: PromoterMoneyKpi.fromJson(Map<String, dynamic>.from(json['monthRevenue'] as Map? ?? {})),
        yearRevenue: PromoterMoneyKpi.fromJson(Map<String, dynamic>.from(json['yearRevenue'] as Map? ?? {})),
        students: PromoterStudentsKpi.fromJson(Map<String, dynamic>.from(json['students'] as Map? ?? {})),
      );
}

class PromoterMoneyKpi {
  const PromoterMoneyKpi({
    required this.label,
    required this.amount,
    required this.changePercent,
    required this.comparisonLabel,
  });

  final String label;
  final double amount;
  final double changePercent;
  final String comparisonLabel;

  factory PromoterMoneyKpi.fromJson(Map<String, dynamic> json) => PromoterMoneyKpi(
        label: json['label'] as String? ?? '',
        amount: _d(json['amount']),
        changePercent: _d(json['changePercent']),
        comparisonLabel: json['comparisonLabel'] as String? ?? '',
      );
}

class PromoterStudentsKpi {
  const PromoterStudentsKpi({
    required this.total,
    required this.boys,
    required this.girls,
    required this.newThisPeriod,
  });

  final int total;
  final int boys;
  final int girls;
  final int newThisPeriod;

  factory PromoterStudentsKpi.fromJson(Map<String, dynamic> json) => PromoterStudentsKpi(
        total: _i(json['total']),
        boys: _i(json['boys']),
        girls: _i(json['girls']),
        newThisPeriod: _i(json['newThisPeriod']),
      );
}

class PromoterExpensesBoard {
  const PromoterExpensesBoard({
    required this.today,
    required this.month,
    required this.year,
    required this.byCategory,
  });

  final double today;
  final double month;
  final double year;
  final List<NamedAmountShare> byCategory;

  factory PromoterExpensesBoard.fromJson(Map<String, dynamic> json) => PromoterExpensesBoard(
        today: _d(json['today']),
        month: _d(json['month']),
        year: _d(json['year']),
        byCategory: _mapList(json['byCategory'], NamedAmountShare.fromJson),
      );
}

class PromoterSituation {
  const PromoterSituation({
    required this.totalRevenue,
    required this.totalExpenses,
    required this.availableBalance,
  });

  final double totalRevenue;
  final double totalExpenses;
  final double availableBalance;

  factory PromoterSituation.fromJson(Map<String, dynamic> json) => PromoterSituation(
        totalRevenue: _d(json['totalRevenue']),
        totalExpenses: _d(json['totalExpenses']),
        availableBalance: _d(json['availableBalance']),
      );
}

class PromoterReceivables {
  const PromoterReceivables({
    required this.remainingToCollect,
    required this.debtorStudents,
    required this.fullyPaidStudents,
    required this.recoveryPercent,
  });

  final double remainingToCollect;
  final int debtorStudents;
  final int fullyPaidStudents;
  final double recoveryPercent;

  factory PromoterReceivables.fromJson(Map<String, dynamic> json) => PromoterReceivables(
        remainingToCollect: _d(json['remainingToCollect']),
        debtorStudents: _i(json['debtorStudents']),
        fullyPaidStudents: _i(json['fullyPaidStudents']),
        recoveryPercent: _d(json['recoveryPercent']),
      );
}

class PromoterFinancialSummary {
  const PromoterFinancialSummary({
    required this.periodRevenueLabel,
    required this.periodRevenue,
    required this.periodRevenueChangePercent,
    required this.secondaryRevenueLabel,
    required this.secondaryRevenue,
    required this.secondaryRevenueChangePercent,
    required this.newEnrollments,
    required this.activeStudents,
    required this.realizationRate,
    required this.expectedRevenue,
    required this.collectedRevenue,
  });

  final String periodRevenueLabel;
  final double periodRevenue;
  final double periodRevenueChangePercent;
  final String secondaryRevenueLabel;
  final double secondaryRevenue;
  final double secondaryRevenueChangePercent;
  final int newEnrollments;
  final int activeStudents;
  final double realizationRate;
  final double expectedRevenue;
  final double collectedRevenue;

  factory PromoterFinancialSummary.fromJson(Map<String, dynamic> json) =>
      PromoterFinancialSummary(
        periodRevenueLabel: json['periodRevenueLabel'] as String? ?? 'Recette',
        periodRevenue: _d(json['periodRevenue']),
        periodRevenueChangePercent: _d(json['periodRevenueChangePercent']),
        secondaryRevenueLabel: json['secondaryRevenueLabel'] as String? ?? 'Recette',
        secondaryRevenue: _d(json['secondaryRevenue']),
        secondaryRevenueChangePercent: _d(json['secondaryRevenueChangePercent']),
        newEnrollments: _i(json['newEnrollments']),
        activeStudents: _i(json['activeStudents']),
        realizationRate: _d(json['realizationRate']),
        expectedRevenue: _d(json['expectedRevenue']),
        collectedRevenue: _d(json['collectedRevenue']),
      );
}

class RevenuePoint {
  const RevenuePoint({required this.label, required this.periodStartUtc, required this.amount});

  final String label;
  final DateTime periodStartUtc;
  final double amount;

  factory RevenuePoint.fromJson(Map<String, dynamic> json) => RevenuePoint(
        label: json['label'] as String? ?? '',
        periodStartUtc: DateTime.tryParse(json['periodStartUtc']?.toString() ?? '') ?? DateTime.now().toUtc(),
        amount: _d(json['amount']),
      );
}

class NamedAmountShare {
  const NamedAmountShare({
    required this.name,
    required this.amount,
    required this.percentage,
    required this.colorHex,
  });

  final String name;
  final double amount;
  final double percentage;
  final String colorHex;

  factory NamedAmountShare.fromJson(Map<String, dynamic> json) => NamedAmountShare(
        name: json['name'] as String? ?? '',
        amount: _d(json['amount']),
        percentage: _d(json['percentage']),
        colorHex: json['colorHex'] as String? ?? '#1D4ED8',
      );
}

class FundAllocationShare {
  const FundAllocationShare({
    required this.destinationId,
    required this.code,
    required this.name,
    required this.periodJ1,
    required this.encaissementJ,
    required this.depenseJ,
    required this.solde,
    required this.percentage,
    required this.colorHex,
  });

  final String destinationId;
  final String code;
  final String name;
  final double periodJ1;
  final double encaissementJ;
  final double depenseJ;
  final double solde;
  final double percentage;
  final String colorHex;

  factory FundAllocationShare.fromJson(Map<String, dynamic> json) => FundAllocationShare(
        destinationId: json['destinationId']?.toString() ?? '',
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? '',
        periodJ1: _d(json['periodJ1']),
        encaissementJ: _d(json['encaissementJ']),
        depenseJ: _d(json['depenseJ']),
        solde: _d(json['solde']),
        percentage: _d(json['percentage']),
        colorHex: json['colorHex'] as String? ?? '#1D4ED8',
      );
}

class PromoterWithholdingShare {
  const PromoterWithholdingShare({
    required this.withholdingTypeId,
    required this.name,
    required this.amountToday,
    required this.amountMonth,
    required this.amountYear,
  });

  final String withholdingTypeId;
  final String name;
  final double amountToday;
  final double amountMonth;
  final double amountYear;

  factory PromoterWithholdingShare.fromJson(Map<String, dynamic> json) => PromoterWithholdingShare(
        withholdingTypeId: json['withholdingTypeId']?.toString() ?? '',
        name: json['name'] as String? ?? 'Retenue',
        amountToday: _d(json['amountToday']),
        amountMonth: _d(json['amountMonth']),
        amountYear: _d(json['amountYear']),
      );
}

class DashboardActivity {
  const DashboardActivity({
    required this.occurredAtUtc,
    required this.kind,
    required this.title,
    required this.subtitle,
    this.amount,
    this.currency,
  });

  final DateTime occurredAtUtc;
  final String kind;
  final String title;
  final String subtitle;
  final double? amount;
  final String? currency;

  factory DashboardActivity.fromJson(Map<String, dynamic> json) => DashboardActivity(
        occurredAtUtc: DateTime.tryParse(json['occurredAtUtc']?.toString() ?? '') ?? DateTime.now().toUtc(),
        kind: json['kind'] as String? ?? '',
        title: json['title'] as String? ?? '',
        subtitle: json['subtitle'] as String? ?? '',
        amount: json['amount'] == null ? null : _d(json['amount']),
        currency: json['currency'] as String?,
      );
}

class DashboardAlert {
  const DashboardAlert({
    required this.severity,
    required this.code,
    required this.title,
    required this.message,
    this.actionHint,
  });

  final String severity;
  final String code;
  final String title;
  final String message;
  final String? actionHint;

  factory DashboardAlert.fromJson(Map<String, dynamic> json) => DashboardAlert(
        severity: json['severity'] as String? ?? 'info',
        code: json['code'] as String? ?? '',
        title: json['title'] as String? ?? json['message'] as String? ?? '',
        message: json['message'] as String? ?? '',
        actionHint: json['actionHint'] as String?,
      );
}

class ClassRevenueRank {
  const ClassRevenueRank({required this.rank, required this.className, required this.amount});

  final int rank;
  final String className;
  final double amount;

  factory ClassRevenueRank.fromJson(Map<String, dynamic> json) => ClassRevenueRank(
        rank: _i(json['rank']),
        className: json['className'] as String? ?? '',
        amount: _d(json['amount']),
      );
}

class PromoterQuickStats {
  const PromoterQuickStats({
    required this.presentStudents,
    required this.absentStudents,
    required this.paymentsToday,
    required this.receiptsPrinted,
    required this.remainingToCollect,
    required this.totalAllocated,
  });

  final int presentStudents;
  final int absentStudents;
  final int paymentsToday;
  final int receiptsPrinted;
  final double remainingToCollect;
  final double totalAllocated;

  factory PromoterQuickStats.fromJson(Map<String, dynamic> json) => PromoterQuickStats(
        presentStudents: _i(json['presentStudents']),
        absentStudents: _i(json['absentStudents']),
        paymentsToday: _i(json['paymentsToday']),
        receiptsPrinted: _i(json['receiptsPrinted']),
        remainingToCollect: _d(json['remainingToCollect']),
        totalAllocated: _d(json['totalAllocated']),
      );
}

class DashboardPaymentLine {
  const DashboardPaymentLine({
    required this.id,
    required this.paymentDateUtc,
    required this.studentName,
    required this.reference,
    required this.amount,
    required this.currency,
    required this.method,
  });

  final String id;
  final DateTime paymentDateUtc;
  final String studentName;
  final String reference;
  final double amount;
  final String currency;
  final String method;

  factory DashboardPaymentLine.fromJson(Map<String, dynamic> json) => DashboardPaymentLine(
        id: json['id']?.toString() ?? '',
        paymentDateUtc: DateTime.tryParse(json['paymentDateUtc']?.toString() ?? '') ?? DateTime.now().toUtc(),
        studentName: json['studentName'] as String? ?? '',
        reference: json['reference'] as String? ?? '',
        amount: _d(json['amount']),
        currency: json['currency'] as String? ?? 'CDF',
        method: json['method'] as String? ?? '',
      );
}

class DashboardExpenseLine {
  const DashboardExpenseLine({
    required this.id,
    required this.expenseDate,
    required this.label,
    required this.category,
    required this.destinationId,
    required this.accountTypeName,
    required this.amount,
    required this.currency,
    required this.reference,
  });

  final String id;
  final DateTime expenseDate;
  final String label;
  final String category;
  final String destinationId;
  final String accountTypeName;
  final double amount;
  final String currency;
  final String reference;

  factory DashboardExpenseLine.fromJson(Map<String, dynamic> json) => DashboardExpenseLine(
        id: json['id']?.toString() ?? '',
        expenseDate: DateTime.tryParse(json['expenseDate']?.toString() ?? '') ?? DateTime.now(),
        label: json['label'] as String? ?? '',
        category: json['category'] as String? ?? '',
        destinationId: json['destinationId']?.toString() ?? '',
        accountTypeName: json['accountTypeName'] as String? ?? json['category'] as String? ?? 'Autres',
        amount: _d(json['amount']),
        currency: json['currency'] as String? ?? 'CDF',
        reference: json['reference'] as String? ?? '',
      );
}

class DashboardDebtorLine {
  const DashboardDebtorLine({
    required this.studentId,
    required this.studentName,
    required this.className,
    required this.amountDue,
    required this.amountPaid,
    required this.remaining,
  });

  final String studentId;
  final String studentName;
  final String className;
  final double amountDue;
  final double amountPaid;
  final double remaining;

  factory DashboardDebtorLine.fromJson(Map<String, dynamic> json) => DashboardDebtorLine(
        studentId: json['studentId']?.toString() ?? '',
        studentName: json['studentName'] as String? ?? '',
        className: json['className'] as String? ?? '',
        amountDue: _d(json['amountDue']),
        amountPaid: _d(json['amountPaid']),
        remaining: _d(json['remaining']),
      );
}

class FeeReceivablesBreakdown {
  const FeeReceivablesBreakdown({
    required this.feeTypeId,
    required this.feeTypeName,
    required this.academicYearId,
    required this.academicYearLabel,
    required this.currency,
    required this.totalExpected,
    required this.totalPaid,
    required this.totalRemaining,
    required this.byInstallment,
    required this.byDestination,
    required this.debtors,
  });

  final String feeTypeId;
  final String feeTypeName;
  final String academicYearId;
  final String academicYearLabel;
  final String currency;
  final double totalExpected;
  final double totalPaid;
  final double totalRemaining;
  final List<FeeInstallmentReceivable> byInstallment;
  final List<FeeDestinationReceivable> byDestination;
  final List<DashboardDebtorLine> debtors;

  factory FeeReceivablesBreakdown.fromJson(Map<String, dynamic> json) => FeeReceivablesBreakdown(
        feeTypeId: json['feeTypeId']?.toString() ?? '',
        feeTypeName: json['feeTypeName'] as String? ?? '',
        academicYearId: json['academicYearId']?.toString() ?? '',
        academicYearLabel: json['academicYearLabel'] as String? ?? '',
        currency: json['currency'] as String? ?? 'CDF',
        totalExpected: _d(json['totalExpected']),
        totalPaid: _d(json['totalPaid']),
        totalRemaining: _d(json['totalRemaining']),
        byInstallment: _mapList(json['byInstallment'], FeeInstallmentReceivable.fromJson),
        byDestination: _mapList(json['byDestination'], FeeDestinationReceivable.fromJson),
        debtors: _mapList(json['debtors'], DashboardDebtorLine.fromJson),
      );
}

class FeeInstallmentReceivable {
  const FeeInstallmentReceivable({
    required this.feeInstallmentId,
    required this.installmentName,
    required this.sortOrder,
    required this.amountExpected,
    required this.amountPaid,
    required this.remaining,
  });

  final String feeInstallmentId;
  final String installmentName;
  final int sortOrder;
  final double amountExpected;
  final double amountPaid;
  final double remaining;

  factory FeeInstallmentReceivable.fromJson(Map<String, dynamic> json) => FeeInstallmentReceivable(
        feeInstallmentId: json['feeInstallmentId']?.toString() ?? '',
        installmentName: json['installmentName'] as String? ?? 'Tranche',
        sortOrder: _i(json['sortOrder']),
        amountExpected: _d(json['amountExpected']),
        amountPaid: _d(json['amountPaid']),
        remaining: _d(json['remaining']),
      );
}

class FeeDestinationReceivable {
  const FeeDestinationReceivable({
    required this.destinationId,
    required this.destinationCode,
    required this.destinationName,
    required this.percentage,
    required this.amountExpected,
    required this.amountCollected,
    required this.remaining,
  });

  final String destinationId;
  final String destinationCode;
  final String destinationName;
  final double percentage;
  final double amountExpected;
  final double amountCollected;
  final double remaining;

  factory FeeDestinationReceivable.fromJson(Map<String, dynamic> json) => FeeDestinationReceivable(
        destinationId: json['destinationId']?.toString() ?? '',
        destinationCode: json['destinationCode'] as String? ?? '',
        destinationName: json['destinationName'] as String? ?? 'Compte',
        percentage: _d(json['percentage']),
        amountExpected: _d(json['amountExpected']),
        amountCollected: _d(json['amountCollected']),
        remaining: _d(json['remaining']),
      );
}

class DashboardFundMovement {
  const DashboardFundMovement({
    required this.id,
    required this.allocatedAtUtc,
    required this.destinationName,
    required this.amount,
    required this.currency,
    this.note,
  });

  final String id;
  final DateTime allocatedAtUtc;
  final String destinationName;
  final double amount;
  final String currency;
  final String? note;

  factory DashboardFundMovement.fromJson(Map<String, dynamic> json) => DashboardFundMovement(
        id: json['id']?.toString() ?? '',
        allocatedAtUtc: DateTime.tryParse(json['allocatedAtUtc']?.toString() ?? '') ?? DateTime.now().toUtc(),
        destinationName: json['destinationName'] as String? ?? '',
        amount: _d(json['amount']),
        currency: json['currency'] as String? ?? 'CDF',
        note: json['note'] as String?,
      );
}

class EnrolledStudentsBySection {
  const EnrolledStudentsBySection({
    required this.totalStudents,
    required this.totalBoys,
    required this.totalGirls,
    required this.sections,
  });

  final int totalStudents;
  final int totalBoys;
  final int totalGirls;
  final List<EnrolledSectionGroup> sections;

  factory EnrolledStudentsBySection.fromJson(Map<String, dynamic> json) => EnrolledStudentsBySection(
        totalStudents: _i(json['totalStudents']),
        totalBoys: _i(json['totalBoys']),
        totalGirls: _i(json['totalGirls']),
        sections: _mapList(json['sections'], EnrolledSectionGroup.fromJson),
      );
}

class EnrolledSectionGroup {
  const EnrolledSectionGroup({
    required this.sectionId,
    required this.sectionName,
    required this.totalStudents,
    required this.boys,
    required this.girls,
    required this.classes,
  });

  final String sectionId;
  final String sectionName;
  final int totalStudents;
  final int boys;
  final int girls;
  final List<EnrolledClassRow> classes;

  factory EnrolledSectionGroup.fromJson(Map<String, dynamic> json) => EnrolledSectionGroup(
        sectionId: json['sectionId']?.toString() ?? '',
        sectionName: json['sectionName'] as String? ?? 'Section',
        totalStudents: _i(json['totalStudents']),
        boys: _i(json['boys']),
        girls: _i(json['girls']),
        classes: _mapList(json['classes'], EnrolledClassRow.fromJson),
      );
}

class EnrolledClassRow {
  const EnrolledClassRow({
    required this.classRoomId,
    required this.className,
    required this.totalStudents,
    required this.boys,
    required this.girls,
  });

  final String classRoomId;
  final String className;
  final int totalStudents;
  final int boys;
  final int girls;

  factory EnrolledClassRow.fromJson(Map<String, dynamic> json) => EnrolledClassRow(
        classRoomId: json['classRoomId']?.toString() ?? '',
        className: json['className'] as String? ?? 'Classe',
        totalStudents: _i(json['totalStudents']),
        boys: _i(json['boys']),
        girls: _i(json['girls']),
      );
}

List<T> _mapList<T>(dynamic raw, T Function(Map<String, dynamic>) fromJson) {
  if (raw is! List) return [];
  return raw.whereType<Map>().map((e) => fromJson(Map<String, dynamic>.from(e))).toList();
}

double _d(dynamic v) {
  if (v == null) return 0;
  if (v is num) return v.toDouble();
  return double.tryParse(v.toString()) ?? 0;
}

int _i(dynamic v) {
  if (v == null) return 0;
  if (v is num) return v.toInt();
  return int.tryParse(v.toString()) ?? 0;
}
