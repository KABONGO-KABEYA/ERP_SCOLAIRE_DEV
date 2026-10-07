import '../../core/api/api_client.dart';
import 'controller_models.dart';

class ControllerRepository {
  ControllerRepository(this._api);

  final ApiClient _api;

  Future<ControllerSetup> getSetup({String? academicYearId}) {
    final query = academicYearId == null || academicYearId.isEmpty
        ? ''
        : '?academicYearId=$academicYearId';
    return _api.getObject(
      '/api/v1/controller/setup$query',
      ControllerSetup.fromJson,
    );
  }

  Future<List<ControllerInstallment>> getInstallments({
    required String academicYearId,
    required String feeTypeId,
  }) =>
      _api.getList(
        '/api/v1/controller/installments?academicYearId=$academicYearId&feeTypeId=$feeTypeId',
        ControllerInstallment.fromJson,
      );

  Future<ControllerStudentSearchResult> searchStudents({
    required String academicYearId,
    required String feeTypeId,
    required String search,
  }) =>
      _api.getObject(
        '/api/v1/controller/students/search?academicYearId=$academicYearId&feeTypeId=$feeTypeId&search=${Uri.encodeQueryComponent(search)}',
        ControllerStudentSearchResult.fromJson,
      );

  Future<ControllerCheckResult> checkQr({
    required String qrPayload,
    required String academicYearId,
    required String feeTypeId,
    required int mode,
    String? feeInstallmentId,
  }) =>
      _api.postObject(
        '/api/v1/controller/check/qr',
        _checkPayload(
          academicYearId: academicYearId,
          feeTypeId: feeTypeId,
          mode: mode,
          feeInstallmentId: feeInstallmentId,
        )..['qrPayload'] = qrPayload,
        ControllerCheckResult.fromJson,
      );

  Future<ControllerCheckResult> checkStudent({
    required String studentId,
    required String academicYearId,
    required String feeTypeId,
    required int mode,
    String? feeInstallmentId,
  }) =>
      _api.postObject(
        '/api/v1/controller/check/student',
        _checkPayload(
          academicYearId: academicYearId,
          feeTypeId: feeTypeId,
          mode: mode,
          feeInstallmentId: feeInstallmentId,
        )..['studentId'] = studentId,
        ControllerCheckResult.fromJson,
      );

  Map<String, dynamic> _checkPayload({
    required String academicYearId,
    required String feeTypeId,
    required int mode,
    String? feeInstallmentId,
  }) =>
      {
        'academicYearId': academicYearId,
        'feeTypeId': feeTypeId,
        'mode': mode,
        if (feeInstallmentId != null) 'feeInstallmentId': feeInstallmentId,
      };
}
