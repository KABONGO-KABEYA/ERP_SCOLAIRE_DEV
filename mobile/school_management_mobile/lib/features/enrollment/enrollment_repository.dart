import 'package:dio/dio.dart';

import '../../core/api/api_client.dart';
import 'models/enrollment_models.dart';

class EnrollmentRepository {
  EnrollmentRepository(this._client);

  final ApiClient _client;

  Future<EnrollmentPrerequisites> getPrerequisites() => _client.getObject(
        '/api/v1/enrollment-wizard/prerequisites',
        EnrollmentPrerequisites.fromJson,
      );

  Future<String> generateRegistrationNumber() async {
    final json = await _client.getObject(
      '/api/v1/enrollment-wizard/registration-number',
      (data) => data,
    );
    return json['registrationNumber'] as String;
  }

  Future<List<EnrollmentStudentSearchResult>> searchStudents(
    String search, {
    bool forReinscription = false,
  }) =>
      _client.getList(
        '/api/v1/enrollment-wizard/search-students?search=${Uri.encodeQueryComponent(search)}&forReinscription=$forReinscription',
        EnrollmentStudentSearchResult.fromJson,
      );

  Future<List<EnrollmentGuardianSearchResult>> searchGuardians(String search) =>
      _client.getList(
        '/api/v1/enrollment-wizard/search-guardians?search=${Uri.encodeQueryComponent(search)}',
        EnrollmentGuardianSearchResult.fromJson,
      );

  Future<EnrollmentStructureOptions> getStructureOptions() => _client.getObject(
        '/api/v1/enrollment-wizard/structure-options',
        EnrollmentStructureOptions.fromJson,
      );

  Future<ClassCapacity> getClassCapacity(String classRoomId, String academicYearId) =>
      _client.getObject(
        '/api/v1/enrollment-wizard/class-capacity?classRoomId=$classRoomId&academicYearId=$academicYearId',
        ClassCapacity.fromJson,
      );

  Future<StoredEnrollmentFile> storeFile({
    required String lastName,
    required String firstName,
    required String registrationNumber,
    required String academicYearLabel,
    required String documentType,
    required String fileName,
    String? filePath,
    List<int>? fileBytes,
  }) {
    if (filePath == null && fileBytes == null) {
      throw ArgumentError('Un fichier est requis pour l\'upload.');
    }

    final MultipartFile multipartFile;
    if (filePath != null) {
      multipartFile = MultipartFile.fromFileSync(filePath, filename: fileName);
    } else {
      multipartFile = MultipartFile.fromBytes(fileBytes!, filename: fileName);
    }

    final formData = FormData.fromMap({
      'lastName': lastName,
      'firstName': firstName,
      'registrationNumber': registrationNumber,
      'academicYearLabel': academicYearLabel,
      'documentType': documentType,
      'file': multipartFile,
    });
    return _client.uploadMultipart(
      '/api/v1/enrollment-wizard/store-file',
      formData,
      StoredEnrollmentFile.fromJson,
    );
  }

  Future<EnrollmentValidationResult> validate(CompleteEnrollmentRequest request) =>
      _client.postObject(
        '/api/v1/enrollment-wizard/validate',
        request.toJson(),
        EnrollmentValidationResult.fromJson,
      );

  Future<CompleteEnrollmentResult> complete(CompleteEnrollmentRequest request) =>
      _client.postObject(
        '/api/v1/enrollment-wizard/complete',
        request.toJson(),
        CompleteEnrollmentResult.fromJson,
      );
}
