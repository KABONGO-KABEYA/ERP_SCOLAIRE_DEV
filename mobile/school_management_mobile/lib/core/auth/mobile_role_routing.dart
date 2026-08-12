/// Résolution déterministe de l'espace mobile (rôles API exacts + secrétaire permission-based).
enum MobileSpace {
  parent,
  teacher,
  promoteur,
  secretary,
  unsupported,
}

/// Codes rôle catalogue non supportés sur mobile (pas de dashboard dédié).
const unsupportedMobileRoleCodes = {
  'PREFET',
  'COMPTABLE',
  'CAISSIER',
  'ADMIN',
  'DIRECTION',
};

abstract final class MobileRoleRouting {
  static const parentHome = '/parent/home';
  static const teacherHome = '/teacher/assignments';
  static const promoteurHome = '/promoteur/dashboard';
  static const secretaryHome = '/secretary/home';
  static const unsupportedRoute = '/unsupported-role';

  static List<String> normalizeRoles(Iterable<String> roles) => roles
      .map((r) => r.trim().toUpperCase())
      .where((r) => r.isNotEmpty)
      .toList(growable: false);

  static List<String> normalizePermissions(Iterable<String> permissions) =>
      permissions
          .map((p) => p.trim().toLowerCase())
          .where((p) => p.isNotEmpty)
          .toList(growable: false);

  static bool hasExactRole(Iterable<String> normalizedRoles, String code) =>
      normalizedRoles.contains(code.toUpperCase());

  /// Accès espace secrétaire : permissions / rôle SECRET*, sans rôles catalogue
  /// qui ont un autre espace ou qui sont explicitement non supportés.
  ///
  /// Ne déduit PAS le routage de `students.create` seul quand le compte est
  /// DIRECTION/ADMIN/etc. (permissions partagées).
  static bool hasSecretaryMobileAccess({
    required Iterable<String> roles,
    required Iterable<String> permissions,
  }) {
    final roleCodes = normalizeRoles(roles);
    final perms = normalizePermissions(permissions);

    const blocking = {
      'PARENT',
      'ENSEIGNANT',
      'TEACHER',
      'PROMOTEUR',
      ...unsupportedMobileRoleCodes,
    };
    if (roleCodes.any(blocking.contains)) return false;

    if (roleCodes.any(_looksLikeSecretaryRole)) return true;
    return perms.contains('students.create');
  }

  static bool _looksLikeSecretaryRole(String code) {
    if (code == 'SECRETAIRE' || code == 'SECRETARY') return true;
    // Rôles métier locaux du type SECRET_SCOLAIRE — pas un contains PARENT/etc.
    return code.startsWith('SECRET');
  }

    /// Priorité : ENSEIGNANT → PROMOTEUR → PARENT → secrétaire → unsupported.
  static MobileSpace resolve({
    required Iterable<String> roles,
    required Iterable<String> permissions,
  }) {
    final roleCodes = normalizeRoles(roles);

    // `TEACHER` = code legacy encore présent en base (ex. compte Addy).
    if (hasExactRole(roleCodes, 'ENSEIGNANT') ||
        hasExactRole(roleCodes, 'TEACHER')) {
      return MobileSpace.teacher;
    }
    if (hasExactRole(roleCodes, 'PROMOTEUR')) return MobileSpace.promoteur;
    if (hasExactRole(roleCodes, 'PARENT')) return MobileSpace.parent;

    if (hasSecretaryMobileAccess(roles: roleCodes, permissions: permissions)) {
      return MobileSpace.secretary;
    }

    return MobileSpace.unsupported;
  }

  static String homeRouteFor(MobileSpace space) => switch (space) {
        MobileSpace.parent => parentHome,
        MobileSpace.teacher => teacherHome,
        MobileSpace.promoteur => promoteurHome,
        MobileSpace.secretary => secretaryHome,
        MobileSpace.unsupported => unsupportedRoute,
      };

  static String homeRoute({
    required Iterable<String> roles,
    required Iterable<String> permissions,
  }) =>
      homeRouteFor(resolve(roles: roles, permissions: permissions));

  /// Guard de préfixe d'URL pour les quatre espaces développés.
  static bool canAccessLocation({
    required MobileSpace space,
    required String location,
  }) {
    final path = location.split('?').first;

    // Activation parent (QR) : hors espace métier connecté.
    if (path.startsWith('/parent/activate')) return true;

    if (path.startsWith('/parent')) {
      return space == MobileSpace.parent;
    }
    if (path.startsWith('/teacher')) {
      return space == MobileSpace.teacher;
    }
    if (path.startsWith('/promoteur')) {
      return space == MobileSpace.promoteur;
    }
    if (path.startsWith('/secretary')) {
      return space == MobileSpace.secretary;
    }
    return true;
  }

  static String? guardRedirect({
    required MobileSpace space,
    required String location,
  }) {
    if (canAccessLocation(space: space, location: location)) return null;
    return homeRouteFor(space);
  }
}
