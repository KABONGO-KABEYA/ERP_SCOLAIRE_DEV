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

  Future<List<TeacherPeriod>> getPeriods(String academicYearId) => _api.getList(
        '/api/v1/teacher/periods?academicYearId=$academicYearId',
        TeacherPeriod.fromJson,
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

  Future<void> createEvaluation({
    required String academicYearId,
    required String academicPeriodId,
    required String courseId,
    required String classRoomId,
    required String title,
  }) =>
      _api.post('/api/v1/grades/evaluations', {
        'academicYearId': academicYearId,
        'academicPeriodId': academicPeriodId,
        'courseId': courseId,
        'classRoomId': classRoomId,
        'title': title,
        'evaluationType': 1,
        'weight': 1,
        'maxScore': 20,
        'evaluationDate': DateTime.now().toIso8601String().split('T').first,
      });
}
