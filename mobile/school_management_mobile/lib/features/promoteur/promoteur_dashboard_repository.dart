import '../../core/api/api_client.dart';
import 'models/promoteur_dashboard_models.dart';

enum DashboardPeriod { today, week, month, year }

enum DashboardDetailScope { today, month, year }

extension DashboardPeriodApi on DashboardPeriod {
  String get apiValue => switch (this) {
        DashboardPeriod.today => 'Today',
        DashboardPeriod.week => 'Week',
        DashboardPeriod.month => 'Month',
        DashboardPeriod.year => 'Year',
      };
}

extension DashboardDetailScopeApi on DashboardDetailScope {
  String get apiValue => switch (this) {
        DashboardDetailScope.today => 'Today',
        DashboardDetailScope.month => 'Month',
        DashboardDetailScope.year => 'Year',
      };

  String get label => switch (this) {
        DashboardDetailScope.today => "Aujourd'hui",
        DashboardDetailScope.month => 'Ce mois',
        DashboardDetailScope.year => 'Cette année',
      };
}

class PromoteurDashboardRepository {
  PromoteurDashboardRepository(this._api);

  final ApiClient _api;

  PromoterDashboardOverview? _cache;
  DateTime? _cacheAt;
  String? _cacheFeeTypeId;
  static const _cacheTtl = Duration(seconds: 20);

  Future<PromoterDashboardOverview> getOverview({
    bool forceRefresh = false,
    String? feeTypeId,
  }) async {
    final now = DateTime.now();
    final cacheKey = feeTypeId ?? '';
    if (!forceRefresh &&
        _cache != null &&
        _cacheAt != null &&
        _cacheFeeTypeId == cacheKey &&
        now.difference(_cacheAt!) < _cacheTtl) {
      return _cache!;
    }

    final feeQuery = (feeTypeId == null || feeTypeId.isEmpty) ? '' : '&feeTypeId=$feeTypeId';
    final data = await _api.getObject(
      '/api/v1/dashboard/overview?period=Month&granularity=Daily$feeQuery',
      PromoterDashboardOverview.fromJson,
    );
    _cache = data;
    _cacheAt = now;
    _cacheFeeTypeId = cacheKey;
    return data;
  }

  void invalidateCache() {
    _cache = null;
    _cacheAt = null;
    _cacheFeeTypeId = null;
  }

  Future<List<DashboardPaymentLine>> getPayments(
    DashboardDetailScope scope, {
    String? feeTypeId,
  }) {
    final feeQuery = (feeTypeId == null || feeTypeId.isEmpty) ? '' : '&feeTypeId=$feeTypeId';
    return _api.getList(
      '/api/v1/dashboard/payments?scope=${scope.apiValue}$feeQuery',
      DashboardPaymentLine.fromJson,
    );
  }

  Future<List<RevenuePoint>> getRevenueDetail(
    DashboardDetailScope scope, {
    String? feeTypeId,
  }) {
    final feeQuery = (feeTypeId == null || feeTypeId.isEmpty) ? '' : '&feeTypeId=$feeTypeId';
    return _api.getList(
      '/api/v1/dashboard/revenue-detail?scope=${scope.apiValue}$feeQuery',
      RevenuePoint.fromJson,
    );
  }

  Future<List<DashboardExpenseLine>> getExpenses(
    DashboardDetailScope scope, {
    String? destinationId,
  }) {
    final q = destinationId == null || destinationId.isEmpty
        ? ''
        : '&destinationId=$destinationId';
    return _api.getList(
      '/api/v1/dashboard/expenses?scope=${scope.apiValue}$q',
      DashboardExpenseLine.fromJson,
    );
  }

  Future<List<DashboardDebtorLine>> getDebtors({String? feeTypeId}) {
    final feeQuery = (feeTypeId == null || feeTypeId.isEmpty) ? '' : '?feeTypeId=$feeTypeId';
    return _api.getList('/api/v1/dashboard/debtors$feeQuery', DashboardDebtorLine.fromJson);
  }

  Future<FeeReceivablesBreakdown> getReceivablesBreakdown({String? feeTypeId}) {
    final feeQuery = (feeTypeId == null || feeTypeId.isEmpty) ? '' : '?feeTypeId=$feeTypeId';
    return _api.getObject(
      '/api/v1/dashboard/receivables-breakdown$feeQuery',
      FeeReceivablesBreakdown.fromJson,
    );
  }

  Future<EnrolledStudentsBySection> getEnrolledStudents() => _api.getObject(
        '/api/v1/dashboard/enrolled-students',
        EnrolledStudentsBySection.fromJson,
      );

  Future<List<DashboardFundMovement>> getFundMovements(String destinationId) =>
      _api.getList(
        '/api/v1/dashboard/fund-movements?destinationId=$destinationId',
        DashboardFundMovement.fromJson,
      );
}
