import '../../../core/utils/student_display_name.dart';
import 'geography_models.dart';

class EnrollmentPrerequisiteIssue {
  const EnrollmentPrerequisiteIssue({
    required this.code,
    required this.message,
    required this.settingsRoute,
    required this.actionLabel,
  });

  final String code;
  final String message;
  final String settingsRoute;
  final String actionLabel;

  factory EnrollmentPrerequisiteIssue.fromJson(Map<String, dynamic> json) =>
      EnrollmentPrerequisiteIssue(
        code: json['code'] as String,
        message: json['message'] as String,
        settingsRoute: json['settingsRoute'] as String,
        actionLabel: json['actionLabel'] as String,
      );
}

class EnrollmentPrerequisites {
  const EnrollmentPrerequisites({
    required this.isReady,
    required this.issues,
    this.currentAcademicYearId,
    this.currentAcademicYearLabel,
    required this.feeTypeCount,
  });

  final bool isReady;
  final List<EnrollmentPrerequisiteIssue> issues;
  final String? currentAcademicYearId;
  final String? currentAcademicYearLabel;
  final int feeTypeCount;

  factory EnrollmentPrerequisites.fromJson(Map<String, dynamic> json) =>
      EnrollmentPrerequisites(
        isReady: json['isReady'] as bool,
        issues: (json['issues'] as List<dynamic>? ?? [])
            .map((e) => EnrollmentPrerequisiteIssue.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
        currentAcademicYearId: json['currentAcademicYearId'] as String?,
        currentAcademicYearLabel: json['currentAcademicYearLabel'] as String?,
        feeTypeCount: json['feeTypeCount'] as int? ?? 0,
      );
}

class EnrollmentStudentSearchResult {
  const EnrollmentStudentSearchResult({
    required this.id,
    required this.registrationNumber,
    required this.firstName,
    required this.lastName,
    this.middleName,
    required this.gender,
    required this.dateOfBirth,
    this.photoPath,
    this.previousClassName,
    this.statusLabel,
    this.lastClassLevel,
  });

  final String id;
  final String registrationNumber;
  final String firstName;
  final String lastName;
  final String? middleName;
  final int gender;
  final String dateOfBirth;
  final String? photoPath;
  final String? previousClassName;
  final String? statusLabel;
  final int? lastClassLevel;

  String get fullName => formatStudentDisplayName(
        lastName: lastName,
        middleName: middleName,
        firstName: firstName,
      );

  factory EnrollmentStudentSearchResult.fromJson(Map<String, dynamic> json) =>
      EnrollmentStudentSearchResult(
        id: json['id']?.toString() ?? '',
        registrationNumber: json['registrationNumber'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        middleName: json['middleName'] as String?,
        gender: json['gender'] as int,
        dateOfBirth: json['dateOfBirth'] as String,
        photoPath: json['photoPath'] as String?,
        previousClassName: json['previousClassName'] as String?,
        statusLabel: json['statusLabel'] as String?,
        lastClassLevel: json['lastClassLevel'] as int?,
      );
}

class EnrollmentGuardianSearchResult {
  const EnrollmentGuardianSearchResult({
    required this.id,
    required this.firstName,
    required this.lastName,
    this.phone,
    this.email,
    this.gender,
  });

  final String id;
  final String firstName;
  final String lastName;
  final String? phone;
  final String? email;
  final int? gender;

  String get fullName => '$lastName $firstName'.trim();

  factory EnrollmentGuardianSearchResult.fromJson(Map<String, dynamic> json) =>
      EnrollmentGuardianSearchResult(
        id: json['id'] as String,
        firstName: json['firstName'] as String,
        lastName: json['lastName'] as String,
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        gender: json['gender'] as int?,
      );
}

class EnrollmentSection {
  const EnrollmentSection({
    required this.id,
    required this.code,
    required this.name,
    required this.cycle,
  });

  final String id;
  final String code;
  final String name;
  final int cycle;

  factory EnrollmentSection.fromJson(Map<String, dynamic> json) => EnrollmentSection(
        id: json['id'] as String,
        code: json['code'] as String,
        name: json['name'] as String,
        cycle: json['cycle'] as int,
      );
}

class EnrollmentClassOption {
  const EnrollmentClassOption({
    required this.classRoomId,
    required this.code,
    required this.fullDisplayName,
    required this.sectionId,
    required this.sectionName,
    this.pedagogicalClassId,
    required this.level,
    this.maxCapacity,
    required this.currentCount,
    required this.isSelectable,
    this.minAge,
    this.maxAge,
    this.studyOption,
  });

  final String classRoomId;
  final String code;
  final String fullDisplayName;
  final String sectionId;
  final String sectionName;
  final String? pedagogicalClassId;
  final int level;
  final int? maxCapacity;
  final int currentCount;
  final bool isSelectable;
  final int? minAge;
  final int? maxAge;
  final String? studyOption;

  factory EnrollmentClassOption.fromJson(Map<String, dynamic> json) => EnrollmentClassOption(
        classRoomId: json['classRoomId'] as String,
        code: json['code'] as String,
        fullDisplayName: json['fullDisplayName'] as String,
        sectionId: json['sectionId'] as String,
        sectionName: json['sectionName'] as String,
        pedagogicalClassId: json['pedagogicalClassId'] as String?,
        level: json['level'] as int? ?? 0,
        maxCapacity: json['maxCapacity'] as int?,
        currentCount: json['currentCount'] as int? ?? 0,
        isSelectable: json['isSelectable'] as bool? ?? true,
        minAge: json['minAge'] as int?,
        maxAge: json['maxAge'] as int?,
        studyOption: json['studyOption'] as String?,
      );
}

class EnrollmentStructureOptions {
  const EnrollmentStructureOptions({
    required this.academicYearId,
    required this.academicYearLabel,
    required this.sections,
    required this.classes,
  });

  final String academicYearId;
  final String academicYearLabel;
  final List<EnrollmentSection> sections;
  final List<EnrollmentClassOption> classes;

  factory EnrollmentStructureOptions.fromJson(Map<String, dynamic> json) =>
      EnrollmentStructureOptions(
        academicYearId: json['academicYearId'] as String,
        academicYearLabel: json['academicYearLabel'] as String,
        sections: (json['sections'] as List<dynamic>)
            .map((e) => EnrollmentSection.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
        classes: (json['classes'] as List<dynamic>)
            .map((e) => EnrollmentClassOption.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
      );
}

class ClassCapacity {
  const ClassCapacity({
    required this.classRoomId,
    this.maxCapacity,
    required this.currentCount,
    required this.remaining,
    required this.isFull,
  });

  final String classRoomId;
  final int? maxCapacity;
  final int currentCount;
  final int remaining;
  final bool isFull;

  factory ClassCapacity.fromJson(Map<String, dynamic> json) => ClassCapacity(
        classRoomId: json['classRoomId'] as String,
        maxCapacity: json['maxCapacity'] as int?,
        currentCount: json['currentCount'] as int? ?? 0,
        remaining: json['remaining'] as int? ?? 0,
        isFull: json['isFull'] as bool? ?? false,
      );
}

class StoredEnrollmentFile {
  const StoredEnrollmentFile({
    required this.storagePath,
    required this.fileName,
    required this.fileSizeBytes,
  });

  final String storagePath;
  final String fileName;
  final int fileSizeBytes;

  factory StoredEnrollmentFile.fromJson(Map<String, dynamic> json) => StoredEnrollmentFile(
        storagePath: json['storagePath'] as String,
        fileName: json['fileName'] as String,
        fileSizeBytes: (json['fileSizeBytes'] as num).toInt(),
      );
}

class EnrollmentDocumentStatus {
  const EnrollmentDocumentStatus({
    required this.documentType,
    required this.status,
    this.fileName,
    this.storagePath,
    this.fileSizeBytes = 0,
  });

  final String documentType;
  final String status;
  final String? fileName;
  final String? storagePath;
  final int fileSizeBytes;

  Map<String, dynamic> toJson() => {
        'documentType': documentType,
        'status': status,
        if (fileName != null) 'fileName': fileName,
        if (storagePath != null) 'storagePath': storagePath,
        'fileSizeBytes': fileSizeBytes,
      };
}

class GuardianInput {
  const GuardianInput({
    required this.firstName,
    required this.lastName,
    this.phone,
    this.email,
    this.residenceAddress,
    this.profession,
    required this.relationship,
    required this.isPrimary,
    required this.canPickup,
    this.gender,
    this.usesStudentAddress = false,
    this.existingGuardianId,
  });

  final String firstName;
  final String lastName;
  final String? phone;
  final String? email;
  final AddressInput? residenceAddress;
  final String? profession;
  final String relationship;
  final bool isPrimary;
  final bool canPickup;
  final int? gender;
  final bool usesStudentAddress;
  final String? existingGuardianId;

  Map<String, dynamic> toJson() => {
        'firstName': firstName,
        'lastName': lastName,
        if (phone != null) 'phone': phone,
        if (email != null) 'email': email,
        if (residenceAddress != null && !usesStudentAddress) 'residenceAddress': residenceAddress!.toJson(),
        if (profession != null) 'profession': profession,
        'relationship': relationship,
        'isPrimary': isPrimary,
        'canPickup': canPickup,
        if (gender != null) 'gender': gender,
        'usesStudentAddress': usesStudentAddress,
        if (existingGuardianId != null) 'existingGuardianId': existingGuardianId,
      };
}

class EnrollmentMedical {
  const EnrollmentMedical({
    this.bloodGroup,
    this.allergies,
    this.chronicDiseases,
    this.treatment,
    this.doctorName,
    this.medicalCenter,
    this.disability,
    this.observations,
    this.medicalEmergency = false,
  });

  final String? bloodGroup;
  final String? allergies;
  final String? chronicDiseases;
  final String? treatment;
  final String? doctorName;
  final String? medicalCenter;
  final String? disability;
  final String? observations;
  final bool medicalEmergency;

  Map<String, dynamic> toJson() => {
        if (bloodGroup != null) 'bloodGroup': bloodGroup,
        if (allergies != null) 'allergies': allergies,
        if (chronicDiseases != null) 'chronicDiseases': chronicDiseases,
        if (treatment != null) 'treatment': treatment,
        if (doctorName != null) 'doctorName': doctorName,
        if (medicalCenter != null) 'medicalCenter': medicalCenter,
        if (disability != null) 'disability': disability,
        if (observations != null) 'observations': observations,
        'medicalEmergency': medicalEmergency,
      };
}

class EnrollmentScolarite {
  const EnrollmentScolarite({
    required this.sectionId,
    required this.classRoomId,
    this.pedagogicalClassId,
    this.orderNumber,
    required this.enrollmentDate,
    required this.registrationKind,
    this.previousSchool,
    this.previousStudentCode,
    this.permanentNumber,
  });

  final String sectionId;
  final String classRoomId;
  final String? pedagogicalClassId;
  final int? orderNumber;
  final String enrollmentDate;
  final int registrationKind;
  final String? previousSchool;
  final String? previousStudentCode;
  final String? permanentNumber;

  Map<String, dynamic> toJson() => {
        'sectionId': sectionId,
        'classRoomId': classRoomId,
        if (pedagogicalClassId != null) 'pedagogicalClassId': pedagogicalClassId,
        if (orderNumber != null) 'orderNumber': orderNumber,
        'enrollmentDate': enrollmentDate,
        'registrationKind': registrationKind,
        if (previousSchool != null) 'previousSchool': previousSchool,
        if (previousStudentCode != null) 'previousStudentCode': previousStudentCode,
        if (permanentNumber != null) 'permanentNumber': permanentNumber,
      };
}

class CompleteEnrollmentRequest {
  const CompleteEnrollmentRequest({
    this.existingStudentId,
    required this.firstName,
    required this.lastName,
    this.middleName,
    required this.gender,
    required this.dateOfBirth,
    this.placeOfBirth,
    this.nationality,
    this.residenceAddress,
    this.language,
    this.religion,
    this.photoPath,
    required this.medical,
    required this.scolarite,
    required this.guardians,
    required this.documents,
    required this.confirmAccuracy,
    this.draftId,
  });

  final String? existingStudentId;
  final String firstName;
  final String lastName;
  final String? middleName;
  final int gender;
  final String dateOfBirth;
  final String? placeOfBirth;
  final String? nationality;
  final AddressInput? residenceAddress;
  final String? language;
  final String? religion;
  final String? photoPath;
  final EnrollmentMedical medical;
  final EnrollmentScolarite scolarite;
  final List<GuardianInput> guardians;
  final List<EnrollmentDocumentStatus> documents;
  final bool confirmAccuracy;
  final String? draftId;

  Map<String, dynamic> toJson() => {
        if (existingStudentId != null) 'existingStudentId': existingStudentId,
        'firstName': firstName,
        'lastName': lastName,
        if (middleName != null) 'middleName': middleName,
        'gender': gender,
        'dateOfBirth': dateOfBirth,
        if (placeOfBirth != null) 'placeOfBirth': placeOfBirth,
        if (nationality != null) 'nationality': nationality,
        if (residenceAddress != null && residenceAddress!.hasContent)
          'residenceAddress': residenceAddress!.toJson(),
        if (language != null) 'language': language,
        if (religion != null) 'religion': religion,
        if (photoPath != null) 'photoPath': photoPath,
        'medical': medical.toJson(),
        'scolarite': scolarite.toJson(),
        'guardians': guardians.map((g) => g.toJson()).toList(),
        'documents': documents.map((d) => d.toJson()).toList(),
        'feeSummary': null,
        'confirmAccuracy': confirmAccuracy,
        if (draftId != null) 'draftId': draftId,
      };
}

class EnrollmentValidationIssue {
  const EnrollmentValidationIssue({
    required this.code,
    required this.message,
    this.stepHint,
  });

  final String code;
  final String message;
  final String? stepHint;

  factory EnrollmentValidationIssue.fromJson(Map<String, dynamic> json) =>
      EnrollmentValidationIssue(
        code: json['code'] as String,
        message: json['message'] as String,
        stepHint: json['stepHint'] as String?,
      );
}

class EnrollmentValidationResult {
  const EnrollmentValidationResult({required this.isValid, required this.issues});

  final bool isValid;
  final List<EnrollmentValidationIssue> issues;

  factory EnrollmentValidationResult.fromJson(Map<String, dynamic> json) =>
      EnrollmentValidationResult(
        isValid: json['isValid'] as bool,
        issues: (json['issues'] as List<dynamic>? ?? [])
            .map((e) => EnrollmentValidationIssue.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
      );
}

class CompleteEnrollmentResult {
  const CompleteEnrollmentResult({
    required this.studentId,
    required this.enrollmentId,
    required this.registrationNumber,
    required this.studentFullName,
    required this.className,
    required this.totalDue,
    required this.message,
  });

  final String studentId;
  final String enrollmentId;
  final String registrationNumber;
  final String studentFullName;
  final String className;
  final double totalDue;
  final String message;

  factory CompleteEnrollmentResult.fromJson(Map<String, dynamic> json) =>
      CompleteEnrollmentResult(
        studentId: json['studentId'] as String,
        enrollmentId: json['enrollmentId'] as String,
        registrationNumber: json['registrationNumber'] as String,
        studentFullName: json['studentFullName'] as String,
        className: json['className'] as String,
        totalDue: (json['totalDue'] as num).toDouble(),
        message: json['message'] as String,
      );
}

const enrollmentDocumentTypes = [
  'Acte de naissance',
  'Photo',
  'Bulletin précédent',
  'Certificat médical',
  'Attestation de réussite',
  'Transfert',
  'Autres',
];
