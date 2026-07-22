import '../../../core/utils/student_display_name.dart';

class StudentSummary {
  const StudentSummary({
    required this.id,
    required this.registrationNumber,
    required this.firstName,
    required this.lastName,
    this.middleName,
    required this.gender,
    required this.dateOfBirth,
    this.phone,
    this.email,
    this.isEnrolledCurrentYear = false,
    this.currentYearClassName,
    this.currentYearStatus,
  });

  final String id;
  final String registrationNumber;
  final String firstName;
  final String lastName;
  final String? middleName;
  final int gender;
  final String dateOfBirth;
  final String? phone;
  final String? email;
  final bool isEnrolledCurrentYear;
  final String? currentYearClassName;
  final int? currentYearStatus;

  String get fullName => formatStudentDisplayName(
        lastName: lastName,
        middleName: middleName,
        firstName: firstName,
      );

  String get genderLabel => gender == 2 ? 'Féminin' : 'Masculin';

  factory StudentSummary.fromJson(Map<String, dynamic> json) => StudentSummary(
        id: json['id']?.toString() ?? '',
        registrationNumber: json['registrationNumber'] as String? ?? '',
        firstName: json['firstName'] as String? ?? '',
        lastName: json['lastName'] as String? ?? '',
        middleName: json['middleName'] as String?,
        gender: json['gender'] is int ? json['gender'] as int : int.tryParse('${json['gender']}') ?? 1,
        dateOfBirth: json['dateOfBirth']?.toString() ?? '',
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        isEnrolledCurrentYear: json['isEnrolledCurrentYear'] as bool? ?? false,
        currentYearClassName: json['currentYearClassName'] as String?,
        currentYearStatus: json['currentYearStatus'] is int
            ? json['currentYearStatus'] as int
            : int.tryParse('${json['currentYearStatus']}'),
      );
}

class StudentEnrollmentHistory {
  const StudentEnrollmentHistory({
    required this.enrollmentId,
    required this.academicYearLabel,
    required this.classDisplayName,
    required this.isCurrentYear,
    required this.isActive,
    required this.enrollmentDate,
    this.sectionName,
  });

  final String enrollmentId;
  final String academicYearLabel;
  final String classDisplayName;
  final bool isCurrentYear;
  final bool isActive;
  final String enrollmentDate;
  final String? sectionName;

  factory StudentEnrollmentHistory.fromJson(Map<String, dynamic> json) => StudentEnrollmentHistory(
        enrollmentId: json['enrollmentId']?.toString() ?? '',
        academicYearLabel: json['academicYearLabel'] as String? ?? '',
        classDisplayName: json['classDisplayName'] as String? ?? '',
        isCurrentYear: json['isCurrentYear'] as bool? ?? false,
        isActive: json['isActive'] as bool? ?? false,
        enrollmentDate: json['enrollmentDate']?.toString() ?? '',
        sectionName: json['sectionName'] as String?,
      );
}

class StudentProfile {
  const StudentProfile({
    required this.student,
    required this.enrollments,
  });

  final StudentSummary student;
  final List<StudentEnrollmentHistory> enrollments;

  factory StudentProfile.fromJson(Map<String, dynamic> json) => StudentProfile(
        student: StudentSummary.fromJson(Map<String, dynamic>.from(json['student'] as Map? ?? {})),
        enrollments: (json['enrollments'] as List<dynamic>? ?? [])
            .whereType<Map>()
            .map((e) => StudentEnrollmentHistory.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
      );
}

class StudentDocument {
  const StudentDocument({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.documentType,
    required this.fileName,
    required this.fileSizeBytes,
    this.mimeType,
    required this.createdAt,
  });

  final String id;
  final String studentId;
  final String studentName;
  final String documentType;
  final String fileName;
  final int fileSizeBytes;
  final String? mimeType;
  final DateTime createdAt;

  bool get isPhoto => documentType.toLowerCase() == 'photo';

  factory StudentDocument.fromJson(Map<String, dynamic> json) => StudentDocument(
        id: json['id']?.toString() ?? '',
        studentId: json['studentId']?.toString() ?? '',
        studentName: json['studentName'] as String? ?? '',
        documentType: json['documentType'] as String? ?? '',
        fileName: json['fileName'] as String? ?? '',
        fileSizeBytes: (json['fileSizeBytes'] as num?)?.toInt() ?? 0,
        mimeType: json['mimeType'] as String?,
        createdAt: DateTime.tryParse(json['createdAt']?.toString() ?? '') ?? DateTime.now(),
      );
}

class StudentSearchPage {
  const StudentSearchPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<StudentSummary> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  factory StudentSearchPage.fromJson(Map<String, dynamic> json) => StudentSearchPage(
        items: (json['items'] as List<dynamic>? ?? [])
            .whereType<Map>()
            .map((e) => StudentSummary.fromJson(Map<String, dynamic>.from(e)))
            .toList(),
        page: (json['page'] as num?)?.toInt() ?? 1,
        pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
        totalPages: (json['totalPages'] as num?)?.toInt() ?? 0,
      );
}
