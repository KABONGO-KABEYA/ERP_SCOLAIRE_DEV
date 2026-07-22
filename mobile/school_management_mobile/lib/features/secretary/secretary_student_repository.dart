import 'package:dio/dio.dart';

import '../../core/api/api_client.dart';
import '../enrollment/models/enrollment_models.dart';
import 'models/secretary_student_models.dart';

class SecretaryStudentRepository {
  SecretaryStudentRepository(this._api);

  final ApiClient _api;

  Future<StudentSearchPage> searchStudents({
    required String search,
    int page = 1,
    int pageSize = 30,
  }) =>
      _api.getObject(
        '/api/v1/students?Search=${Uri.encodeQueryComponent(search)}'
        '&IncludeAll=true&Page=$page&PageSize=$pageSize',
        StudentSearchPage.fromJson,
      );

  /// Recherche rapide (nom, matricule, téléphone tuteur) — même moteur que le wizard.
  Future<List<EnrollmentStudentSearchResult>> quickSearch(String search) =>
      _api.getList(
        '/api/v1/enrollment-wizard/search-students'
        '?search=${Uri.encodeQueryComponent(search)}&forReinscription=false',
        EnrollmentStudentSearchResult.fromJson,
      );

  Future<StudentProfile> getProfile(String studentId) => _api.getObject(
        '/api/v1/students/$studentId/profile',
        StudentProfile.fromJson,
      );

  Future<List<StudentDocument>> listDocuments(String studentId) => _api.getList(
        '/api/v1/documents?studentId=$studentId',
        StudentDocument.fromJson,
      );

  Future<StudentDocument> uploadDocument({
    required String studentId,
    required String documentType,
    required String fileName,
    String? filePath,
    List<int>? fileBytes,
  }) {
    if (filePath == null && fileBytes == null) {
      throw ArgumentError('Un fichier est requis.');
    }

    final MultipartFile file;
    if (filePath != null) {
      file = MultipartFile.fromFileSync(filePath, filename: fileName);
    } else {
      file = MultipartFile.fromBytes(fileBytes!, filename: fileName);
    }

    final form = FormData.fromMap({
      'studentId': studentId,
      'documentType': documentType,
      'file': file,
    });

    return _api.uploadMultipart(
      '/api/v1/documents',
      form,
      StudentDocument.fromJson,
    );
  }

  Future<void> deleteDocument(String documentId) =>
      _api.delete('/api/v1/documents/$documentId');
}
