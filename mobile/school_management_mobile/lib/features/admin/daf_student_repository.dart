import '../../core/api/api_client.dart';
import '../enrollment/models/enrollment_models.dart';
import '../secretary/models/secretary_student_models.dart';
import 'daf_student_models.dart';

class DafStudentRepository {
  DafStudentRepository(this._api);

  final ApiClient _api;

  Future<EnrollmentPrerequisites> getPrerequisites() => _api.getObject(
        '/api/v1/enrollment-wizard/prerequisites',
        EnrollmentPrerequisites.fromJson,
      );

  Future<StudentSearchPage> searchStudentsByClass({
    required String academicYearId,
    required String classRoomId,
  }) =>
      _api.getObject(
        '/api/v1/students?AcademicYearId=$academicYearId&ClassRoomId=$classRoomId'
        '&ApplyFilters=true&IncludeInscrits=true&Page=1&PageSize=500',
        StudentSearchPage.fromJson,
      );

  Future<StudentSearchPage> searchEnrolledStudents({
    required String academicYearId,
    required String search,
  }) =>
      _api.getObject(
        '/api/v1/students?Search=${Uri.encodeQueryComponent(search.trim())}'
        '&AcademicYearId=$academicYearId&ApplyFilters=false&IncludeAll=false'
        '&IncludeInscrits=true&Page=1&PageSize=500',
        StudentSearchPage.fromJson,
      );

  Future<StudentDossierPayload> getStudentDossier(String studentId) => _api.getObject(
        '/api/v1/enrollment-wizard/student-dossier/$studentId',
        StudentDossierPayload.fromJson,
      );

  Future<StudentProfile> getStudentProfile(String studentId) => _api.getObject(
        '/api/v1/students/$studentId/profile',
        StudentProfile.fromJson,
      );

  Future<StudentFinancialSummary> getFinancialSummary(String studentId, String academicYearId) =>
      _api.getObject(
        '/api/v1/payments/student/$studentId/summary?academicYearId=$academicYearId',
        StudentFinancialSummary.fromJson,
      );

  Future<PaymentSituationPage> getPaymentSituations({
    required String studentId,
    required String academicYearId,
  }) =>
      _api.getObject(
        '/api/v1/finance/payment-situations?academicYearId=$academicYearId&studentId=$studentId&page=1&pageSize=100',
        PaymentSituationPage.fromJson,
      );

  Future<InstallmentPlan> getInstallmentPlan(String enrollmentId, String feeTypeId) => _api.getObject(
        '/api/v1/finance/payment-situations/$enrollmentId/installment-plan?feeTypeId=$feeTypeId',
        InstallmentPlan.fromJson,
      );

  Future<FeeCatalog> getFeeCatalog() => _api.getObject(
        '/api/v1/school-fees/catalog',
        FeeCatalog.fromJson,
      );
}

String resolveStudentRegime(String? sectionName) {
  final name = sectionName?.toLowerCase() ?? '';
  if (name.contains('maternelle')) return 'Maternelle';
  if (name.contains('primaire')) return 'Primaire';
  return 'Secondaire';
}

const studentRegimeOrder = ['Maternelle', 'Primaire', 'Secondaire'];

int regimeSortKey(String regime) {
  final index = studentRegimeOrder.indexOf(regime);
  return index >= 0 ? index : studentRegimeOrder.length;
}
