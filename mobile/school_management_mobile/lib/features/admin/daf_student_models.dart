class StudentFinancialSummary {
  const StudentFinancialSummary({
    required this.totalDue,
    required this.totalPaid,
    required this.balance,
    required this.currency,
  });

  final double totalDue;
  final double totalPaid;
  final double balance;
  final String currency;

  factory StudentFinancialSummary.fromJson(Map<String, dynamic> json) => StudentFinancialSummary(
        totalDue: _d(json['totalDue']),
        totalPaid: _d(json['totalPaid']),
        balance: _d(json['balance']),
        currency: json['currency']?.toString() ?? 'CDF',
      );
}

class PaymentSituationPage {
  const PaymentSituationPage({required this.items});

  final List<PaymentSituationItem> items;

  factory PaymentSituationPage.fromJson(Map<String, dynamic> json) => PaymentSituationPage(
        items: (json['items'] as List<dynamic>? ?? [])
            .whereType<Map>()
            .map((e) => PaymentSituationItem.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
      );
}

class PaymentSituationItem {
  const PaymentSituationItem({
    required this.feeTypeId,
    required this.feeTypeName,
    required this.feePricingCategoryName,
    required this.amountExpected,
    required this.amountPaid,
    required this.balance,
    required this.currency,
  });

  final String feeTypeId;
  final String feeTypeName;
  final String feePricingCategoryName;
  final double amountExpected;
  final double amountPaid;
  final double balance;
  final String currency;

  factory PaymentSituationItem.fromJson(Map<String, dynamic> json) => PaymentSituationItem(
        feeTypeId: json['feeTypeId']?.toString() ?? '',
        feeTypeName: json['feeTypeName'] as String? ?? 'Frais',
        feePricingCategoryName: json['feePricingCategoryName'] as String? ?? '—',
        amountExpected: _d(json['amountExpected']),
        amountPaid: _d(json['amountPaid']),
        balance: _d(json['balance']),
        currency: json['currency']?.toString() ?? 'CDF',
      );
}

class FeeTypeCatalogItem {
  const FeeTypeCatalogItem({
    required this.id,
    required this.name,
    required this.isActive,
  });

  final String id;
  final String name;
  final bool isActive;

  factory FeeTypeCatalogItem.fromJson(Map<String, dynamic> json) => FeeTypeCatalogItem(
        id: json['id']?.toString() ?? '',
        name: json['name'] as String? ?? 'Frais',
        isActive: json['isActive'] as bool? ?? true,
      );
}

class FeeCatalog {
  const FeeCatalog({required this.feeTypes});

  final List<FeeTypeCatalogItem> feeTypes;

  factory FeeCatalog.fromJson(Map<String, dynamic> json) => FeeCatalog(
        feeTypes: (json['feeTypes'] as List<dynamic>? ?? [])
            .whereType<Map>()
            .map((e) => FeeTypeCatalogItem.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
      );
}

class InstallmentPlan {
  const InstallmentPlan({
    required this.currency,
    required this.lines,
  });

  final String currency;
  final List<InstallmentPlanLine> lines;

  factory InstallmentPlan.fromJson(Map<String, dynamic> json) => InstallmentPlan(
        currency: json['currency']?.toString() ?? 'CDF',
        lines: (json['lines'] as List<dynamic>? ?? [])
            .whereType<Map>()
            .map((e) => InstallmentPlanLine.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
      );
}

class InstallmentPlanLine {
  const InstallmentPlanLine({
    required this.installmentName,
    required this.amountExpected,
    required this.amountPaid,
    required this.remaining,
    this.dueDate,
  });

  final String installmentName;
  final double amountExpected;
  final double amountPaid;
  final double remaining;
  final String? dueDate;

  factory InstallmentPlanLine.fromJson(Map<String, dynamic> json) => InstallmentPlanLine(
        installmentName: json['installmentName'] as String? ?? 'Tranche',
        amountExpected: _d(json['amountExpected']),
        amountPaid: _d(json['amountPaid']),
        remaining: _d(json['remaining']),
        dueDate: json['dueDate']?.toString(),
      );
}

class StudentDossierPayload {
  const StudentDossierPayload({
    required this.studentId,
    required this.enrollmentId,
    required this.registrationNumber,
    required this.dossier,
  });

  final String studentId;
  final String enrollmentId;
  final String registrationNumber;
  final Map<String, dynamic> dossier;

  factory StudentDossierPayload.fromJson(Map<String, dynamic> json) => StudentDossierPayload(
        studentId: json['studentId']?.toString() ?? '',
        enrollmentId: json['enrollmentId']?.toString() ?? '',
        registrationNumber: json['registrationNumber'] as String? ?? '',
        dossier: Map<String, dynamic>.from(json['dossier'] as Map? ?? {}),
      );
}

double _d(dynamic value) {
  if (value is num) return value.toDouble();
  return double.tryParse('$value') ?? 0;
}
