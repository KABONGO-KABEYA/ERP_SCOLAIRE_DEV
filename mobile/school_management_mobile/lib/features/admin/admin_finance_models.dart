class RealizedReceiptsReport {
  const RealizedReceiptsReport({
    required this.fromDate,
    required this.toDate,
    required this.grandTotal,
    required this.paymentCount,
    required this.items,
    required this.dailyBuckets,
    required this.byClass,
    required this.byFeeType,
    required this.byCurrency,
    required this.bySection,
    required this.dailyByClass,
    required this.dailyByFeeType,
    required this.dailyBySection,
    required this.pivotRows,
    required this.dailyPivotRows,
  });

  final String fromDate;
  final String toDate;
  final double grandTotal;
  final int paymentCount;
  final List<RealizedReceiptLine> items;
  final List<RealizedReceiptsDailyBucket> dailyBuckets;
  final List<RealizedReceiptsByClass> byClass;
  final List<RealizedReceiptsByFeeType> byFeeType;
  final List<RealizedReceiptsByCurrency> byCurrency;
  final List<RealizedReceiptsBySection> bySection;
  final List<RealizedReceiptsDailyByClass> dailyByClass;
  final List<RealizedReceiptsDailyByFeeType> dailyByFeeType;
  final List<RealizedReceiptsDailyBySection> dailyBySection;
  final List<RealizedReceiptsPivotRow> pivotRows;
  final List<RealizedReceiptsDailyPivotRow> dailyPivotRows;

  factory RealizedReceiptsReport.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsReport(
        fromDate: json['fromDate']?.toString() ?? '',
        toDate: json['toDate']?.toString() ?? '',
        grandTotal: _d(json['grandTotal']),
        paymentCount: json['paymentCount'] as int? ?? 0,
        items: _mapList(json['items'], RealizedReceiptLine.fromJson),
        dailyBuckets: _mapList(json['dailyBuckets'], RealizedReceiptsDailyBucket.fromJson),
        byClass: _mapList(json['byClass'], RealizedReceiptsByClass.fromJson),
        byFeeType: _mapList(json['byFeeType'], RealizedReceiptsByFeeType.fromJson),
        byCurrency: _mapList(json['byCurrency'], RealizedReceiptsByCurrency.fromJson),
        bySection: _mapList(json['bySection'], RealizedReceiptsBySection.fromJson),
        dailyByClass: _mapList(json['dailyByClass'], RealizedReceiptsDailyByClass.fromJson),
        dailyByFeeType: _mapList(json['dailyByFeeType'], RealizedReceiptsDailyByFeeType.fromJson),
        dailyBySection: _mapList(json['dailyBySection'], RealizedReceiptsDailyBySection.fromJson),
        pivotRows: _mapList(json['pivotRows'], RealizedReceiptsPivotRow.fromJson),
        dailyPivotRows: _mapList(json['dailyPivotRows'], RealizedReceiptsDailyPivotRow.fromJson),
      );
}

class RealizedReceiptLine {
  const RealizedReceiptLine({
    required this.receiptNumber,
    required this.studentName,
    required this.className,
    required this.paymentDate,
    required this.totalAmount,
    required this.currency,
    this.feeTypesSummary,
  });

  final String receiptNumber;
  final String studentName;
  final String className;
  final String paymentDate;
  final double totalAmount;
  final String currency;
  final String? feeTypesSummary;

  factory RealizedReceiptLine.fromJson(Map<String, dynamic> json) => RealizedReceiptLine(
        receiptNumber: json['receiptNumber'] as String? ?? '',
        studentName: json['studentName'] as String? ?? '',
        className: json['className'] as String? ?? '—',
        paymentDate: json['paymentDate']?.toString() ?? '',
        totalAmount: _d(json['totalAmount']),
        currency: json['currency']?.toString() ?? 'CDF',
        feeTypesSummary: json['feeTypesSummary'] as String?,
      );
}

class RealizedReceiptsDailyBucket {
  const RealizedReceiptsDailyBucket({
    required this.date,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String date;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsDailyBucket.fromJson(Map<String, dynamic> json) => RealizedReceiptsDailyBucket(
        date: json['date']?.toString() ?? '',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsByClass {
  const RealizedReceiptsByClass({
    required this.className,
    required this.sectionName,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String className;
  final String sectionName;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsByClass.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsByClass(
        className: json['className'] as String? ?? '—',
        sectionName: json['sectionName'] as String? ?? '',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsByFeeType {
  const RealizedReceiptsByFeeType({
    required this.feeTypeName,
    required this.currency,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String feeTypeName;
  final String currency;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsByFeeType.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsByFeeType(
        feeTypeName: json['feeTypeName'] as String? ?? '—',
        currency: json['currency']?.toString() ?? 'CDF',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsBySection {
  const RealizedReceiptsBySection({
    required this.sectionName,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String sectionName;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsBySection.fromJson(Map<String, dynamic> json) => RealizedReceiptsBySection(
        sectionName: json['sectionName'] as String? ?? '—',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsDailyByClass {
  const RealizedReceiptsDailyByClass({
    required this.date,
    required this.className,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String date;
  final String className;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsDailyByClass.fromJson(Map<String, dynamic> json) => RealizedReceiptsDailyByClass(
        date: json['date']?.toString() ?? '',
        className: json['className'] as String? ?? '—',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsDailyByFeeType {
  const RealizedReceiptsDailyByFeeType({
    required this.date,
    required this.feeTypeName,
    required this.currency,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String date;
  final String feeTypeName;
  final String currency;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsDailyByFeeType.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsDailyByFeeType(
        date: json['date']?.toString() ?? '',
        feeTypeName: json['feeTypeName'] as String? ?? '—',
        currency: json['currency']?.toString() ?? 'CDF',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsDailyBySection {
  const RealizedReceiptsDailyBySection({
    required this.date,
    required this.sectionName,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String date;
  final String sectionName;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsDailyBySection.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsDailyBySection(
        date: json['date']?.toString() ?? '',
        sectionName: json['sectionName'] as String? ?? '—',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class RealizedReceiptsPivotRow {
  const RealizedReceiptsPivotRow({
    required this.studentName,
    required this.className,
    required this.installmentAmounts,
    required this.rowTotal,
  });

  final String studentName;
  final String className;
  final List<double> installmentAmounts;
  final double rowTotal;

  factory RealizedReceiptsPivotRow.fromJson(Map<String, dynamic> json) => RealizedReceiptsPivotRow(
        studentName: json['studentName'] as String? ?? '',
        className: json['className'] as String? ?? '—',
        installmentAmounts: (json['installmentAmounts'] as List<dynamic>? ?? []).map(_d).toList(),
        rowTotal: _d(json['rowTotal']),
      );
}

class RealizedReceiptsDailyPivotRow {
  const RealizedReceiptsDailyPivotRow({
    required this.date,
    required this.studentName,
    required this.className,
    required this.installmentDetails,
    required this.rowTotal,
  });

  final String date;
  final String studentName;
  final String className;
  final List<String> installmentDetails;
  final double rowTotal;

  factory RealizedReceiptsDailyPivotRow.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsDailyPivotRow(
        date: json['date']?.toString() ?? '',
        studentName: json['studentName'] as String? ?? '',
        className: json['className'] as String? ?? '—',
        installmentDetails: (json['installmentDetails'] as List<dynamic>? ?? [])
            .map((e) => e?.toString() ?? '')
            .toList(),
        rowTotal: _d(json['rowTotal']),
      );
}

class AllocationCashFlowReport {
  const AllocationCashFlowReport({
    required this.globalRows,
    required this.dailyGroups,
    required this.totalsByCurrency,
  });

  final List<AllocationCashFlowRow> globalRows;
  final List<AllocationCashFlowDailyGroup> dailyGroups;
  final List<AllocationCashFlowRow> totalsByCurrency;

  factory AllocationCashFlowReport.fromJson(Map<String, dynamic> json) => AllocationCashFlowReport(
        globalRows: _mapList(json['globalRows'], AllocationCashFlowRow.fromJson),
        dailyGroups: _mapList(json['dailyGroups'], AllocationCashFlowDailyGroup.fromJson),
        totalsByCurrency: _mapList(json['totalsByCurrency'], AllocationCashFlowRow.fromJson),
      );
}

class AllocationCashFlowRow {
  const AllocationCashFlowRow({
    required this.destinationCode,
    required this.destinationName,
    required this.currencyCode,
    required this.periodJ1,
    required this.encaissement,
    required this.depenseP,
    required this.periodeP,
  });

  final String destinationCode;
  final String destinationName;
  final String currencyCode;
  final double periodJ1;
  final double encaissement;
  final double depenseP;
  final double periodeP;

  factory AllocationCashFlowRow.fromJson(Map<String, dynamic> json) => AllocationCashFlowRow(
        destinationCode: json['destinationCode'] as String? ?? '',
        destinationName: json['destinationName'] as String? ?? '',
        currencyCode: json['currencyCode']?.toString() ?? 'CDF',
        periodJ1: _d(json['periodJ1']),
        encaissement: _d(json['encaissement']),
        depenseP: _d(json['depenseP']),
        periodeP: _d(json['periodeP']),
      );
}

class AllocationCashFlowDailyGroup {
  const AllocationCashFlowDailyGroup({
    required this.date,
    required this.rows,
  });

  final String date;
  final List<AllocationCashFlowRow> rows;

  factory AllocationCashFlowDailyGroup.fromJson(Map<String, dynamic> json) => AllocationCashFlowDailyGroup(
        date: json['date']?.toString() ?? '',
        rows: _mapList(json['rows'], AllocationCashFlowRow.fromJson),
      );
}

class WithholdingReport {
  const WithholdingReport({
    required this.groups,
    required this.grandTotal,
    required this.paymentCount,
  });

  final List<WithholdingReportTypeGroup> groups;
  final double grandTotal;
  final int paymentCount;

  factory WithholdingReport.fromJson(Map<String, dynamic> json) => WithholdingReport(
        groups: _mapList(json['groups'], WithholdingReportTypeGroup.fromJson),
        grandTotal: _d(json['grandTotal']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class WithholdingReportTypeGroup {
  const WithholdingReportTypeGroup({
    required this.withholdingTypeCode,
    required this.withholdingTypeName,
    required this.typeTotal,
    required this.students,
  });

  final String withholdingTypeCode;
  final String withholdingTypeName;
  final double typeTotal;
  final List<WithholdingReportStudentLine> students;

  factory WithholdingReportTypeGroup.fromJson(Map<String, dynamic> json) => WithholdingReportTypeGroup(
        withholdingTypeCode: json['withholdingTypeCode'] as String? ?? '',
        withholdingTypeName: json['withholdingTypeName'] as String? ?? '',
        typeTotal: _d(json['typeTotal']),
        students: _mapList(json['students'], WithholdingReportStudentLine.fromJson),
      );
}

class WithholdingReportStudentLine {
  const WithholdingReportStudentLine({
    required this.studentName,
    required this.paymentDate,
    required this.amount,
  });

  final String studentName;
  final String paymentDate;
  final double amount;

  factory WithholdingReportStudentLine.fromJson(Map<String, dynamic> json) =>
      WithholdingReportStudentLine(
        studentName: json['studentName'] as String? ?? '',
        paymentDate: json['paymentDate']?.toString() ?? '',
        amount: _d(json['amount']),
      );
}

class PaymentSituationReportResult {
  const PaymentSituationReportResult({
    required this.academicYearLabel,
    required this.feeTypeName,
    required this.scopeLabel,
    required this.situationLabel,
    required this.installmentColumns,
    required this.pivotRows,
    required this.items,
    required this.totalCount,
    required this.inOrderCount,
    required this.notInOrderCount,
    required this.totalExpected,
    required this.totalPaid,
    required this.totalBalance,
    required this.currency,
  });

  final String academicYearLabel;
  final String feeTypeName;
  final String scopeLabel;
  final String situationLabel;
  final List<PaymentSituationInstallmentColumn> installmentColumns;
  final List<PaymentSituationPivotRow> pivotRows;
  final List<PaymentSituationReportRow> items;
  final int totalCount;
  final int inOrderCount;
  final int notInOrderCount;
  final double totalExpected;
  final double totalPaid;
  final double totalBalance;
  final String currency;

  factory PaymentSituationReportResult.fromJson(Map<String, dynamic> json) => PaymentSituationReportResult(
        academicYearLabel: json['academicYearLabel'] as String? ?? '',
        feeTypeName: json['feeTypeName'] as String? ?? '',
        scopeLabel: json['scopeLabel'] as String? ?? '',
        situationLabel: json['situationLabel'] as String? ?? '',
        installmentColumns:
            _mapList(json['installmentColumns'], PaymentSituationInstallmentColumn.fromJson),
        pivotRows: _mapList(json['pivotRows'], PaymentSituationPivotRow.fromJson),
        items: _mapList(json['items'], PaymentSituationReportRow.fromJson),
        totalCount: json['totalCount'] as int? ?? 0,
        inOrderCount: json['inOrderCount'] as int? ?? 0,
        notInOrderCount: json['notInOrderCount'] as int? ?? 0,
        totalExpected: _d(json['totalExpected']),
        totalPaid: _d(json['totalPaid']),
        totalBalance: _d(json['totalBalance']),
        currency: json['currency']?.toString() ?? 'CDF',
      );
}

class FeeTypeInstallment {
  const FeeTypeInstallment({
    required this.feeInstallmentId,
    required this.installmentName,
    required this.sortOrder,
  });

  final String feeInstallmentId;
  final String installmentName;
  final int sortOrder;

  factory FeeTypeInstallment.fromJson(Map<String, dynamic> json) => FeeTypeInstallment(
        feeInstallmentId: json['feeInstallmentId']?.toString() ?? '',
        installmentName: json['installmentName'] as String? ?? '',
        sortOrder: json['sortOrder'] as int? ?? 0,
      );
}

class PaymentSituationInstallmentColumn {
  const PaymentSituationInstallmentColumn({
    required this.feeInstallmentId,
    required this.installmentName,
    required this.sortOrder,
  });

  final String feeInstallmentId;
  final String installmentName;
  final int sortOrder;

  factory PaymentSituationInstallmentColumn.fromJson(Map<String, dynamic> json) =>
      PaymentSituationInstallmentColumn(
        feeInstallmentId: json['feeInstallmentId']?.toString() ?? '',
        installmentName: json['installmentName'] as String? ?? '',
        sortOrder: json['sortOrder'] as int? ?? 0,
      );
}

class PaymentSituationPivotRow {
  const PaymentSituationPivotRow({
    required this.registrationNumber,
    required this.fullName,
    required this.className,
    required this.sectionName,
    required this.installmentExpected,
    required this.installmentPaid,
    required this.installmentBalances,
    required this.installmentApplicable,
    required this.amountExpected,
    required this.amountPaid,
    required this.balance,
    required this.isInOrder,
  });

  final String registrationNumber;
  final String fullName;
  final String className;
  final String sectionName;
  final List<double> installmentExpected;
  final List<double> installmentPaid;
  final List<double> installmentBalances;
  final List<bool> installmentApplicable;
  final double amountExpected;
  final double amountPaid;
  final double balance;
  final bool isInOrder;

  factory PaymentSituationPivotRow.fromJson(Map<String, dynamic> json) => PaymentSituationPivotRow(
        registrationNumber: json['registrationNumber'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        className: json['className'] as String? ?? '—',
        sectionName: json['sectionName'] as String? ?? '—',
        installmentExpected: (json['installmentExpected'] as List<dynamic>? ?? []).map(_d).toList(),
        installmentPaid: (json['installmentPaid'] as List<dynamic>? ?? []).map(_d).toList(),
        installmentBalances: (json['installmentBalances'] as List<dynamic>? ?? []).map(_d).toList(),
        installmentApplicable:
            (json['installmentApplicable'] as List<dynamic>? ?? []).map((e) => e == true).toList(),
        amountExpected: _d(json['amountExpected']),
        amountPaid: _d(json['amountPaid']),
        balance: _d(json['balance']),
        isInOrder: json['isInOrder'] as bool? ?? false,
      );
}

class PaymentSituationReportRow {
  const PaymentSituationReportRow({
    required this.registrationNumber,
    required this.fullName,
    required this.className,
    required this.amountExpected,
    required this.amountPaid,
    required this.balance,
    required this.currency,
    required this.isInOrder,
  });

  final String registrationNumber;
  final String fullName;
  final String className;
  final double amountExpected;
  final double amountPaid;
  final double balance;
  final String currency;
  final bool isInOrder;

  factory PaymentSituationReportRow.fromJson(Map<String, dynamic> json) => PaymentSituationReportRow(
        registrationNumber: json['registrationNumber'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        className: json['className'] as String? ?? '—',
        amountExpected: _d(json['amountExpected']),
        amountPaid: _d(json['amountPaid']),
        balance: _d(json['balance']),
        currency: json['currency']?.toString() ?? 'CDF',
        isInOrder: json['isInOrder'] as bool? ?? false,
      );
}

class RealizedReceiptsByCurrency {
  const RealizedReceiptsByCurrency({
    required this.currency,
    required this.totalAmount,
    required this.paymentCount,
  });

  final String currency;
  final double totalAmount;
  final int paymentCount;

  factory RealizedReceiptsByCurrency.fromJson(Map<String, dynamic> json) =>
      RealizedReceiptsByCurrency(
        currency: json['currency']?.toString() ?? 'CDF',
        totalAmount: _d(json['totalAmount']),
        paymentCount: json['paymentCount'] as int? ?? 0,
      );
}

class PricingCategoryOption {
  const PricingCategoryOption({
    required this.id,
    required this.code,
    required this.name,
    required this.isActive,
  });

  final String id;
  final String code;
  final String name;
  final bool isActive;

  factory PricingCategoryOption.fromJson(Map<String, dynamic> json) =>
      PricingCategoryOption(
        id: json['id']?.toString() ?? '',
        code: json['code'] as String? ?? '',
        name: json['name'] as String? ?? 'Catégorie',
        isActive: json['isActive'] as bool? ?? true,
      );
}

class StudentPricingAssignment {
  const StudentPricingAssignment({
    required this.enrollmentId,
    required this.studentId,
    required this.registrationNumber,
    required this.fullName,
    required this.className,
    required this.feePricingCategoryId,
    required this.feePricingCategoryName,
  });

  final String enrollmentId;
  final String studentId;
  final String registrationNumber;
  final String fullName;
  final String className;
  final String feePricingCategoryId;
  final String feePricingCategoryName;

  factory StudentPricingAssignment.fromJson(Map<String, dynamic> json) =>
      StudentPricingAssignment(
        enrollmentId: json['enrollmentId']?.toString() ?? '',
        studentId: json['studentId']?.toString() ?? '',
        registrationNumber: json['registrationNumber'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        className: json['className'] as String? ?? '—',
        feePricingCategoryId: json['feePricingCategoryId']?.toString() ?? '',
        feePricingCategoryName:
            json['feePricingCategoryName'] as String? ?? '—',
      );
}

class StudentPricingAssignmentPage {
  const StudentPricingAssignmentPage({
    required this.items,
    required this.totalCount,
  });

  final List<StudentPricingAssignment> items;
  final int totalCount;

  factory StudentPricingAssignmentPage.fromJson(Map<String, dynamic> json) =>
      StudentPricingAssignmentPage(
        items: (json['items'] as List<dynamic>? ?? [])
            .whereType<Map>()
            .map((e) => StudentPricingAssignment.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
        totalCount: json['totalCount'] as int? ?? 0,
      );
}

double _d(dynamic value) {
  if (value is num) return value.toDouble();
  return double.tryParse('$value') ?? 0;
}

List<T> _mapList<T>(dynamic source, T Function(Map<String, dynamic>) fromJson) =>
    (source as List<dynamic>? ?? [])
        .whereType<Map>()
        .map((e) => fromJson(Map<String, dynamic>.from(e)))
        .toList();
