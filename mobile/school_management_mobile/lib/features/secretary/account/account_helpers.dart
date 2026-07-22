const String kMobileAppName = 'ERP SCOLAIRE Mobile';
const String kMobileAppVersion = '1.0.0';

String resolveRoleLabel(List<String> roles) {
  for (final role in roles) {
    final upper = role.toUpperCase();
    if (upper.contains('SECRET')) return 'Secrétaire scolaire';
    if (upper.contains('ADMIN')) return 'Administrateur';
    if (upper.contains('DIRECTION')) return 'Direction';
    if (upper.contains('ENSEIGNANT')) return 'Enseignant';
    if (upper.contains('PARENT')) return 'Parent';
    if (upper.contains('PROMOTEUR') || upper.contains('PROPRIETAIRE')) {
      return 'Promoteur';
    }
  }
  return roles.isNotEmpty ? roles.first : 'Utilisateur';
}

String resolveServerLabel(String? baseUrl, String modeLabel) {
  if (baseUrl == null || baseUrl.isEmpty) return modeLabel;
  return '$modeLabel · $baseUrl';
}
