import '../../core/api/api_client.dart';
import 'models/promoteur_dashboard_models.dart';

enum DashboardPeriod { today, week, month, year }

enum RevenueGranularity { daily, weekly, monthly }

extension DashboardPeriodApi on DashboardPeriod {
  String get apiValue => switch (this) {
        DashboardPeriod.today => 'Today',
        DashboardPeriod.week => 'Week',
        DashboardPeriod.month => 'Month',
        DashboardPeriod.year => 'Year',
      };

  String get label => switch (this) {
        DashboardPeriod.today => "Aujourd'hui",
        DashboardPeriod.week => 'Cette semaine',
        DashboardPeriod.month => 'Ce mois',
        DashboardPeriod.year => 'Cette année',
      };
}

extension RevenueGranularityApi on RevenueGranularity {
  String get apiValue => switch (this) {
        RevenueGranularity.daily => 'Daily',
        RevenueGranularity.weekly => 'Weekly',
        RevenueGranularity.monthly => 'Monthly',
      };

  String get label => switch (this) {
        RevenueGranularity.daily => 'Journalier',
        RevenueGranularity.weekly => 'Hebdomadaire',
        RevenueGranularity.monthly => 'Mensuel',
      };
}

class PromoteurDashboardRepository {
  PromoteurDashboardRepository(this._api);

  final ApiClient _api;

  Future<PromoterDashboardOverview> getOverview({
    DashboardPeriod period = DashboardPeriod.month,
    RevenueGranularity granularity = RevenueGranularity.daily,
  }) =>
      _api.getObject(
        '/api/v1/dashboard/overview?period=${period.apiValue}&granularity=${granularity.apiValue}',
        PromoterDashboardOverview.fromJson,
      );
}
