import '../../core/api/api_client.dart';
import 'models/direction_models.dart';

class DirectionRepository {
  DirectionRepository(this._api);

  final ApiClient _api;

  Future<DashboardStats> getDashboard() => _api.getObject(
        '/api/v1/reports/dashboard',
        DashboardStats.fromJson,
      );

  Future<FinancialSummary> getFinancialSummary() => _api.getObject(
        '/api/v1/reports/financial-summary',
        FinancialSummary.fromJson,
      );

  Future<List<EnrollmentByClass>> getEnrollmentByClass() => _api.getList(
        '/api/v1/reports/enrollment-by-class',
        EnrollmentByClass.fromJson,
      );

  Future<List<ClassAverageReport>> getClassAverages() => _api.getList(
        '/api/v1/reports/class-averages',
        ClassAverageReport.fromJson,
      );
}
