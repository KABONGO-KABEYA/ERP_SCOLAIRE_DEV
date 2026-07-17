class PromoterDashboardOverview {
  const PromoterDashboardOverview({
    required this.schoolName,
    required this.currency,
    required this.period,
    required this.generatedAtUtc,
    required this.summary,
    required this.revenueSeries,
    required this.feeTypeShares,
    required this.fundAllocations,
    required this.recentActivities,
    required this.alerts,
    required this.topClasses,
    required this.topFeeTypes,
    required this.quickStats,
  });

  final String schoolName;
  final String currency;
  final String period;
  final DateTime generatedAtUtc;
  final PromoterFinancialSummary summary;
  final List<RevenuePoint> revenueSeries;
  final List<NamedAmountShare> feeTypeShares;
  final List<FundAllocationShare> fundAllocations;
  final List<DashboardActivity> recentActivities;
  final List<DashboardAlert> alerts;
  final List<ClassRevenueRank> topClasses;
  final List<NamedAmountShare> topFeeTypes;
  final PromoterQuickStats quickStats;

  factory PromoterDashboardOverview.fromJson(Map<String, dynamic> json) =>
      PromoterDashboardOverview(
        schoolName: json['schoolName'] as String? ?? 'Établissement',
        currency: json['currency'] as String? ?? 'CDF',
        period: json['period'] as String? ?? 'Month',
        generatedAtUtc: DateTime.tryParse(json['generatedAtUtc']?.toString() ?? '') ?? DateTime.now().toUtc(),
        summary: PromoterFinancialSummary.fromJson(
          Map<String, dynamic>.from(json['summary'] as Map? ?? {}),
        ),
        revenueSeries: _mapList(json['revenueSeries'], RevenuePoint.fromJson),
        feeTypeShares: _mapList(json['feeTypeShares'], NamedAmountShare.fromJson),
        fundAllocations: _mapList(json['fundAllocations'], FundAllocationShare.fromJson),
        recentActivities: _mapList(json['recentActivities'], DashboardActivity.fromJson),
        alerts: _mapList(json['alerts'], DashboardAlert.fromJson),
        topClasses: _mapList(json['topClasses'], ClassRevenueRank.fromJson),
        topFeeTypes: _mapList(json['topFeeTypes'], NamedAmountShare.fromJson),
        quickStats: PromoterQuickStats.fromJson(
          Map<String, dynamic>.from(json['quickStats'] as Map? ?? {}),
        ),
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
    required this.name,
    required this.amount,
    required this.percentage,
  });

  final String destinationId;
  final String name;
  final double amount;
  final double percentage;

  factory FundAllocationShare.fromJson(Map<String, dynamic> json) => FundAllocationShare(
        destinationId: json['destinationId']?.toString() ?? '',
        name: json['name'] as String? ?? '',
        amount: _d(json['amount']),
        percentage: _d(json['percentage']),
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
  const DashboardAlert({required this.severity, required this.code, required this.message});

  final String severity;
  final String code;
  final String message;

  factory DashboardAlert.fromJson(Map<String, dynamic> json) => DashboardAlert(
        severity: json['severity'] as String? ?? 'info',
        code: json['code'] as String? ?? '',
        message: json['message'] as String? ?? '',
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

List<T> _mapList<T>(dynamic raw, T Function(Map<String, dynamic>) fromJson) {
  if (raw is! List) return [];
  return raw
      .whereType<Map>()
      .map((e) => fromJson(Map<String, dynamic>.from(e)))
      .toList();
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
