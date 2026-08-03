import '../../core/api/api_client.dart';
import 'models/teacher_models.dart';

class TeacherRepository {
  TeacherRepository(this._api);

  final ApiClient _api;

  Future<List<TeacherAssignment>> getAssignments() => _api.getList(
        '/api/v1/teacher/assignments',
        TeacherAssignment.fromJson,
      );

  Future<List<TeacherStudent>> getClassStudents(String classRoomId) => _api.getList(
        '/api/v1/teacher/classes/$classRoomId/students',
        TeacherStudent.fromJson,
      );

  /// Sous-période ouverte (calendrier pédagogique) — null si aucune.
  Future<TeacherPeriod?> getOpenPeriod({
    required String classRoomId,
    required String academicYearId,
  }) async {
    final periods = await _api.getList(
      '/api/v1/teacher/classes/$classRoomId/open-periods?academicYearId=$academicYearId',
      TeacherPeriod.fromJson,
    );
    return periods.isEmpty ? null : periods.first;
  }

  Future<List<EvaluationTypeOption>> getEvaluationTypes() => _api.getList(
        '/api/v1/grades/evaluation-types',
        EvaluationTypeOption.fromJson,
      );

  Future<List<TeacherEvaluation>> getEvaluations({
    required String classRoomId,
    required String academicPeriodId,
  }) =>
      _api.getList(
        '/api/v1/grades/evaluations?classRoomId=$classRoomId&academicPeriodId=$academicPeriodId',
        TeacherEvaluation.fromJson,
      );

  Future<List<GradeEntry>> getGradeEntries(String evaluationId) => _api.getList(
        '/api/v1/grades/evaluations/$evaluationId/entries',
        GradeEntry.fromJson,
      );

  Future<void> submitGrades({
    required String evaluationId,
    required List<Map<String, dynamic>> grades,
  }) =>
      _api.post('/api/v1/grades/entries', {
        'evaluationId': evaluationId,
        'grades': grades,
      });

  Future<TeacherEvaluation> createEvaluation({
    required String academicYearId,
    required String academicPeriodId,
    required String courseId,
    required String classRoomId,
    required String evaluationTypeId,
    required String title,
    required int maxScore,
    required String evaluationDate,
  }) =>
      _api.postObject(
        '/api/v1/grades/evaluations',
        {
          'academicYearId': academicYearId,
          'academicPeriodId': academicPeriodId,
          'courseId': courseId,
          'classRoomId': classRoomId,
          'evaluationTypeId': evaluationTypeId,
          'enrollmentId': null,
          'title': title,
          'weight': 1,
          'maxScore': maxScore,
          'evaluationDate': evaluationDate,
        },
        TeacherEvaluation.fromJson,
      );
}
