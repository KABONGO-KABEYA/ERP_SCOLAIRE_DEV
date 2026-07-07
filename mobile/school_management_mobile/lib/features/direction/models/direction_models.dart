class DashboardStats {
  const DashboardStats({
    required this.totalStudents,
    required this.activeEnrollments,
    required this.totalClassRooms,
    required this.totalTeachers,
    required this.totalPaymentsAmount,
    required this.paymentCount,
  });

  final int totalStudents;
  final int activeEnrollments;
  final int totalClassRooms;
  final int totalTeachers;
  final double totalPaymentsAmount;
  final int paymentCount;

  factory DashboardStats.fromJson(Map<String, dynamic> json) => DashboardStats(
        totalStudents: json['totalStudents'] as int,
        activeEnrollments: json['activeEnrollments'] as int,
        totalClassRooms: json['totalClassRooms'] as int,
        totalTeachers: json['totalTeachers'] as int,
        totalPaymentsAmount: (json['totalPaymentsAmount'] as num).toDouble(),
        paymentCount: json['paymentCount'] as int,
      );
}

class EnrollmentByClass {
  const EnrollmentByClass({
    required this.classRoomId,
    required this.classCode,
    required this.className,
    required this.sectionName,
    required this.totalStudents,
    required this.maleCount,
    required this.femaleCount,
  });

  final String classRoomId;
  final String classCode;
  final String className;
  final String sectionName;
  final int totalStudents;
  final int maleCount;
  final int femaleCount;

  factory EnrollmentByClass.fromJson(Map<String, dynamic> json) => EnrollmentByClass(
        classRoomId: json['classRoomId'] as String,
        classCode: json['classCode'] as String,
        className: json['className'] as String,
        sectionName: json['sectionName'] as String,
        totalStudents: json['totalStudents'] as int,
        maleCount: json['maleCount'] as int,
        femaleCount: json['femaleCount'] as int,
      );
}

class FinancialSummary {
  const FinancialSummary({
    required this.totalCollected,
    required this.paymentCount,
    required this.debtorCount,
    required this.upToDateCount,
    required this.partialCount,
  });

  final double totalCollected;
  final int paymentCount;
  final int debtorCount;
  final int upToDateCount;
  final int partialCount;

  factory FinancialSummary.fromJson(Map<String, dynamic> json) => FinancialSummary(
        totalCollected: (json['totalCollected'] as num).toDouble(),
        paymentCount: json['paymentCount'] as int,
        debtorCount: json['debtorCount'] as int,
        upToDateCount: json['upToDateCount'] as int,
        partialCount: json['partialCount'] as int,
      );
}

class ClassAverageReport {
  const ClassAverageReport({
    required this.classRoomId,
    required this.className,
    required this.periodName,
    required this.studentCount,
    required this.classAverage,
    required this.passCount,
    required this.failCount,
  });

  final String classRoomId;
  final String className;
  final String periodName;
  final int studentCount;
  final double classAverage;
  final int passCount;
  final int failCount;

  factory ClassAverageReport.fromJson(Map<String, dynamic> json) => ClassAverageReport(
        classRoomId: json['classRoomId'] as String,
        className: json['className'] as String,
        periodName: json['periodName'] as String,
        studentCount: json['studentCount'] as int,
        classAverage: (json['classAverage'] as num).toDouble(),
        passCount: json['passCount'] as int,
        failCount: json['failCount'] as int,
      );
}
