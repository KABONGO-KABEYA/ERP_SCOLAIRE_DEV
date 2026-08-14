/// Politique d'accès fonctionnelle dérivée des permissions JWT (alignée Desktop DAF).
abstract final class PermissionPolicy {
  static const reportsRead = 'reports.read';
  static const studentsRead = 'students.read';
  static const paymentsRead = 'payments.read';
  static const paymentsCreate = 'payments.create';
  static const pricingCategoriesAssign = 'pricing-categories.assign';
  static const accountingRead = 'accounting.read';
  static const accountingManage = 'accounting.update';
  static const personnelRead = 'personnel.read';

  static List<String> normalize(Iterable<String> permissions) => permissions
      .map((p) => p.trim().toLowerCase())
      .where((p) => p.isNotEmpty)
      .toList(growable: false);

  static bool has(Iterable<String> permissions, String code) =>
      normalize(permissions).contains(code.toLowerCase());

  static bool canViewFinancialReports(Iterable<String> permissions) =>
      has(permissions, reportsRead);

  static bool canViewExpenses(Iterable<String> permissions) =>
      has(permissions, accountingRead);

  static bool canManageExpenses(Iterable<String> permissions) =>
      has(permissions, accountingManage);

  static bool canAssignPricingCategories(Iterable<String> permissions) =>
      has(permissions, pricingCategoriesAssign);

  static bool canAccessEncaissements(Iterable<String> permissions) =>
      has(permissions, paymentsCreate);

  static bool canViewEncaissementsList(Iterable<String> permissions) =>
      has(permissions, paymentsRead) || has(permissions, paymentsCreate);

  static bool canViewPersonnel(Iterable<String> permissions) =>
      has(permissions, personnelRead);
}
