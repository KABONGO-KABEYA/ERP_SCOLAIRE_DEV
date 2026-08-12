import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/auth/mobile_role_routing.dart';
import 'package:school_management_mobile/core/auth/session_school_coherence.dart';
import 'package:school_management_mobile/core/cache/cache_partition_policy.dart';

String _fakeJwt({required String schoolId}) {
  String b64(Map<String, Object?> map) {
    final raw = base64Url.encode(utf8.encode(jsonEncode(map)));
    return raw.replaceAll('=', '');
  }

  final header = b64({'alg': 'none', 'typ': 'JWT'});
  final payload = b64({'school_id': schoolId, 'sub': 'user'});
  return '$header.$payload.sig';
}

void main() {
  group('MobileRoleRouting — home routes', () {
    test('PARENT → /parent/home', () {
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['PARENT'],
          permissions: const [],
        ),
        MobileRoleRouting.parentHome,
      );
    });

    test('ENSEIGNANT → /teacher/assignments', () {
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['ENSEIGNANT'],
          permissions: const ['students.read'],
        ),
        MobileRoleRouting.teacherHome,
      );
    });

    test('TEACHER (legacy) → /teacher/assignments', () {
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['TEACHER'],
          permissions: const ['students.read'],
        ),
        MobileRoleRouting.teacherHome,
      );
    });

    test('PROMOTEUR → /promoteur/dashboard', () {
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['PROMOTEUR'],
          permissions: const ['reports.read'],
        ),
        MobileRoleRouting.promoteurHome,
      );
    });

    test('SECRÉTAIRE permission-based → /secretary/home', () {
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['SECRET_SCOLAIRE'],
          permissions: const ['students.create'],
        ),
        MobileRoleRouting.secretaryHome,
      );
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['ASSISTANT'],
          permissions: const ['students.create'],
        ),
        MobileRoleRouting.secretaryHome,
      );
    });

    test('PREFET → unsupported', () {
      expect(
        MobileRoleRouting.resolve(
          roles: const ['PREFET'],
          permissions: const ['students.read'],
        ),
        MobileSpace.unsupported,
      );
    });

    test('COMPTABLE → unsupported', () {
      expect(
        MobileRoleRouting.resolve(
          roles: const ['COMPTABLE'],
          permissions: const ['payments.read'],
        ),
        MobileSpace.unsupported,
      );
    });

    test('CAISSIER → unsupported', () {
      expect(
        MobileRoleRouting.resolve(
          roles: const ['CAISSIER'],
          permissions: const ['payments.create'],
        ),
        MobileSpace.unsupported,
      );
    });

    test('ADMIN → unsupported (pas secrétaire)', () {
      expect(
        MobileRoleRouting.resolve(
          roles: const ['ADMIN'],
          permissions: const ['admin.full', 'students.create'],
        ),
        MobileSpace.unsupported,
      );
    });

    test('DIRECTION + students.create → unsupported (pas secrétaire)', () {
      expect(
        MobileRoleRouting.resolve(
          roles: const ['DIRECTION'],
          permissions: const ['students.create', 'reports.read'],
        ),
        MobileSpace.unsupported,
      );
    });

    test('rôle inconnu → unsupported (pas fallback parent)', () {
      expect(
        MobileRoleRouting.homeRoute(
          roles: const ['FOO_BAR'],
          permissions: const [],
        ),
        MobileRoleRouting.unsupportedRoute,
      );
      expect(
        MobileRoleRouting.homeRoute(roles: const [], permissions: const []),
        MobileRoleRouting.unsupportedRoute,
      );
    });

    test('égalité exacte : PARENTX ne tombe pas en parent', () {
      expect(
        MobileRoleRouting.resolve(
          roles: const ['PARENTX'],
          permissions: const [],
        ),
        MobileSpace.unsupported,
      );
    });
  });

  group('MobileRoleRouting — guards cross-role', () {
    test('PARENT refuse teacher/secretary/promoteur', () {
      const space = MobileSpace.parent;
      expect(
        MobileRoleRouting.canAccessLocation(
          space: space,
          location: '/parent/home',
        ),
        isTrue,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: space,
          location: '/teacher/assignments',
        ),
        isFalse,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: space,
          location: '/secretary/home',
        ),
        isFalse,
      );
      expect(
        MobileRoleRouting.guardRedirect(
          space: space,
          location: '/teacher/assignments',
        ),
        MobileRoleRouting.parentHome,
      );
    });

    test('ENSEIGNANT refuse parent/secretary', () {
      const space = MobileSpace.teacher;
      expect(
        MobileRoleRouting.canAccessLocation(
          space: space,
          location: '/teacher/assignments',
        ),
        isTrue,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: space,
          location: '/parent/home',
        ),
        isFalse,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: space,
          location: '/secretary/students',
        ),
        isFalse,
      );
    });

    test('PROMOTEUR refuse parent', () {
      expect(
        MobileRoleRouting.canAccessLocation(
          space: MobileSpace.promoteur,
          location: '/promoteur/dashboard',
        ),
        isTrue,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: MobileSpace.promoteur,
          location: '/parent/payments',
        ),
        isFalse,
      );
    });

    test('SECRÉTAIRE refuse parent/teacher', () {
      expect(
        MobileRoleRouting.canAccessLocation(
          space: MobileSpace.secretary,
          location: '/secretary/enrollment',
        ),
        isTrue,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: MobileSpace.secretary,
          location: '/parent/home',
        ),
        isFalse,
      );
      expect(
        MobileRoleRouting.canAccessLocation(
          space: MobileSpace.secretary,
          location: '/teacher/assignments',
        ),
        isFalse,
      );
    });

    test('/parent/activate reste accessible hors espace parent', () {
      expect(
        MobileRoleRouting.canAccessLocation(
          space: MobileSpace.teacher,
          location: '/parent/activate',
        ),
        isTrue,
      );
    });
  });

  group('SessionSchoolCoherence — ActiveSchoolId == JWT.SchoolId', () {
    const schoolA = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
    const schoolB = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

    test('match OK quand session et JWT = actif', () {
      final jwt = _fakeJwt(schoolId: schoolA);
      expect(
        SessionSchoolCoherence.matchesLoginUser(
          activeSchoolId: schoolA,
          userSchoolId: schoolA,
          accessToken: jwt,
        ),
        isTrue,
      );
      expect(
        SessionSchoolCoherence.peekSchoolIdFromJwt(jwt),
        CachePartitionPolicy.normalizeSchoolId(schoolA),
      );
    });

    test('refuse ActiveSchoolId=B avec JWT=A', () {
      final jwt = _fakeJwt(schoolId: schoolA);
      expect(
        SessionSchoolCoherence.matches(
          activeSchoolId: schoolB,
          sessionSchoolId: schoolA,
          jwtSchoolId: SessionSchoolCoherence.peekSchoolIdFromJwt(jwt),
        ),
        isFalse,
      );
    });

    test('scénario A → B → A', () {
      final jwtA = _fakeJwt(schoolId: schoolA);
      final jwtB = _fakeJwt(schoolId: schoolB);

      expect(
        SessionSchoolCoherence.matchesLoginUser(
          activeSchoolId: schoolA,
          userSchoolId: schoolA,
          accessToken: jwtA,
        ),
        isTrue,
      );
      expect(
        SessionSchoolCoherence.matchesLoginUser(
          activeSchoolId: schoolB,
          userSchoolId: schoolA,
          accessToken: jwtA,
        ),
        isFalse,
      );
      expect(
        SessionSchoolCoherence.matchesLoginUser(
          activeSchoolId: schoolB,
          userSchoolId: schoolB,
          accessToken: jwtB,
        ),
        isTrue,
      );
      expect(
        SessionSchoolCoherence.matchesLoginUser(
          activeSchoolId: schoolA,
          userSchoolId: schoolA,
          accessToken: jwtA,
        ),
        isTrue,
      );
    });
  });
}
