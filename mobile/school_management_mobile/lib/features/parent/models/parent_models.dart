class ParentChild {
  const ParentChild({
    required this.studentId,
    required this.registrationNumber,
    required this.fullName,
    this.className,
  });

  final String studentId;
  final String registrationNumber;
  final String fullName;
  final String? className;

  factory ParentChild.fromJson(Map<String, dynamic> json) => ParentChild(
        studentId: json['studentId'] as String,
        registrationNumber: json['registrationNumber'] as String,
        fullName: json['fullName'] as String,
        className: json['className'] as String?,
      );
}

class ParentPayment {
  const ParentPayment({
    required this.id,
    required this.receiptNumber,
    required this.paymentDate,
    required this.totalAmount,
    required this.currency,
    required this.status,
  });

  final String id;
  final String receiptNumber;
  final DateTime paymentDate;
  final double totalAmount;
  final int currency;
  final int status;

  factory ParentPayment.fromJson(Map<String, dynamic> json) => ParentPayment(
        id: json['id'] as String,
        receiptNumber: json['receiptNumber'] as String,
        paymentDate: DateTime.parse(json['paymentDate'] as String),
        totalAmount: (json['totalAmount'] as num).toDouble(),
        currency: json['currency'] as int,
        status: json['status'] as int,
      );

  String get currencyLabel => currency == 1 ? 'CDF' : 'USD';

  String get statusLabel {
    return switch (status) {
      0 => 'Brouillon',
      1 => 'Validé',
      2 => 'Annulé',
      _ => '—',
    };
  }
}

class ParentBulletin {
  const ParentBulletin({
    required this.academicPeriodId,
    required this.periodName,
    required this.average,
    required this.percentage,
    required this.rank,
    required this.classSize,
    required this.isPublished,
  });

  final String academicPeriodId;
  final String periodName;
  final double average;
  final double percentage;
  final int rank;
  final int classSize;
  final bool isPublished;

  factory ParentBulletin.fromJson(Map<String, dynamic> json) => ParentBulletin(
        academicPeriodId: json['academicPeriodId'] as String,
        periodName: json['periodName'] as String,
        average: (json['average'] as num).toDouble(),
        percentage: (json['percentage'] as num).toDouble(),
        rank: json['rank'] as int,
        classSize: json['classSize'] as int,
        isPublished: json['isPublished'] as bool,
      );
}
