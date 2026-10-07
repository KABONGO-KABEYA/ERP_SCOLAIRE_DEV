class ControllerAcademicYear {
  const ControllerAcademicYear({
    required this.id,
    required this.label,
    required this.isCurrent,
    required this.isClosed,
  });

  final String id;
  final String label;
  final bool isCurrent;
  final bool isClosed;

  factory ControllerAcademicYear.fromJson(Map<String, dynamic> json) =>
      ControllerAcademicYear(
        id: json['id']?.toString() ?? '',
        label: json['label']?.toString() ?? '',
        isCurrent: json['isCurrent'] == true,
        isClosed: json['isClosed'] == true,
      );
}

class ControllerFeeType {
  const ControllerFeeType({
    required this.id,
    required this.code,
    required this.name,
    required this.currency,
  });

  final String id;
  final String code;
  final String name;
  final String currency;

  factory ControllerFeeType.fromJson(Map<String, dynamic> json) =>
      ControllerFeeType(
        id: json['id']?.toString() ?? '',
        code: json['code']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        currency: _currency(json['currency']),
      );
}

class ControllerSetup {
  const ControllerSetup({
    required this.academicYears,
    required this.currentAcademicYearId,
    required this.feeTypes,
  });

  final List<ControllerAcademicYear> academicYears;
  final String? currentAcademicYearId;
  final List<ControllerFeeType> feeTypes;

  factory ControllerSetup.fromJson(Map<String, dynamic> json) =>
      ControllerSetup(
        academicYears: _mapList(
          json['academicYears'],
          ControllerAcademicYear.fromJson,
        ),
        currentAcademicYearId: json['currentAcademicYearId']?.toString(),
        feeTypes: _mapList(json['feeTypes'], ControllerFeeType.fromJson),
      );
}

class ControllerInstallment {
  const ControllerInstallment({
    required this.id,
    required this.name,
    required this.sortOrder,
  });

  final String id;
  final String name;
  final int sortOrder;

  factory ControllerInstallment.fromJson(Map<String, dynamic> json) =>
      ControllerInstallment(
        id: json['id']?.toString() ?? '',
        name: json['name']?.toString() ?? '',
        sortOrder: (json['sortOrder'] as num?)?.toInt() ?? 0,
      );
}

class ControllerStudentSearchItem {
  const ControllerStudentSearchItem({
    required this.studentId,
    required this.registrationNumber,
    required this.fullName,
    required this.className,
    this.photoPath,
  });

  final String studentId;
  final String registrationNumber;
  final String fullName;
  final String className;
  final String? photoPath;

  factory ControllerStudentSearchItem.fromJson(Map<String, dynamic> json) =>
      ControllerStudentSearchItem(
        studentId: json['studentId']?.toString() ?? '',
        registrationNumber: json['registrationNumber']?.toString() ?? '',
        fullName: json['fullName']?.toString() ?? '',
        className: json['className']?.toString() ?? '—',
        photoPath: json['photoPath']?.toString(),
      );
}

class ControllerStudentSearchResult {
  const ControllerStudentSearchResult(this.items);

  final List<ControllerStudentSearchItem> items;

  factory ControllerStudentSearchResult.fromJson(Map<String, dynamic> json) =>
      ControllerStudentSearchResult(
        _mapList(json['items'], ControllerStudentSearchItem.fromJson),
      );
}

class ControllerCheckResult {
  const ControllerCheckResult({
    required this.studentId,
    required this.registrationNumber,
    required this.fullName,
    required this.className,
    required this.academicYearLabel,
    required this.feeTypeName,
    required this.currency,
    required this.mode,
    required this.amountExpected,
    required this.amountPaid,
    required this.balance,
    required this.status,
    required this.statusLabel,
    this.photoPath,
    this.installmentName,
    this.dueDate,
    this.cardNumber,
  });

  final String studentId;
  final String registrationNumber;
  final String fullName;
  final String className;
  final String academicYearLabel;
  final String feeTypeName;
  final String currency;
  final int mode;
  final double amountExpected;
  final double amountPaid;
  final double balance;
  final int status;
  final String statusLabel;
  final String? photoPath;
  final String? installmentName;
  final String? dueDate;
  final String? cardNumber;

  factory ControllerCheckResult.fromJson(Map<String, dynamic> json) =>
      ControllerCheckResult(
        studentId: json['studentId']?.toString() ?? '',
        registrationNumber: json['registrationNumber']?.toString() ?? '',
        fullName: json['fullName']?.toString() ?? '',
        className: json['className']?.toString() ?? '—',
        academicYearLabel: json['academicYearLabel']?.toString() ?? '',
        feeTypeName: json['feeTypeName']?.toString() ?? '',
        currency: _currency(json['currency']),
        mode: (json['mode'] as num?)?.toInt() ?? 1,
        amountExpected: _double(json['amountExpected']),
        amountPaid: _double(json['amountPaid']),
        balance: _double(json['balance']),
        status: (json['status'] as num?)?.toInt() ?? 3,
        statusLabel: json['statusLabel']?.toString() ?? '',
        photoPath: json['photoPath']?.toString(),
        installmentName: json['installmentName']?.toString(),
        dueDate: json['dueDate']?.toString(),
        cardNumber: json['cardNumber']?.toString(),
      );
}

List<T> _mapList<T>(dynamic value, T Function(Map<String, dynamic>) mapper) {
  if (value is! List) return const [];
  return value
      .whereType<Map>()
      .map((e) => mapper(Map<String, dynamic>.from(e)))
      .toList();
}

double _double(dynamic value) => value is num
    ? value.toDouble()
    : double.tryParse(value?.toString() ?? '') ?? 0;

String _currency(dynamic value) {
  if (value is String && value.isNotEmpty) return value;
  return value == 2 ? 'USD' : 'CDF';
}
