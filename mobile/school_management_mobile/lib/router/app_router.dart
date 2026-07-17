import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/auth_storage.dart';
import '../core/providers/app_providers.dart';
import '../features/auth/login_screen.dart';
import '../features/parent/children_screen.dart';
import '../features/parent/child_detail_screen.dart';
import '../features/teacher/assignments_screen.dart';
import '../features/teacher/class_screen.dart';
import '../features/teacher/evaluations_screen.dart';
import '../features/teacher/grade_entry_screen.dart';
import '../features/direction/dashboard_screen.dart';
import '../features/promoteur/dashboard_screen.dart';
import '../features/enrollment/enrollment_wizard_screen.dart';
import '../features/secretary/secretary_home_screen.dart';

final appRouterProvider = Provider<GoRouter>((ref) {
  final authState = ref.watch(authStateProvider);

  return GoRouter(
    initialLocation: '/login',
    refreshListenable: _AuthRefreshListenable(ref),
    redirect: (context, state) async {
      if (authState.isLoading) return null;
      final loggedIn = authState.value ?? false;
      final onLogin = state.matchedLocation == '/login';

      if (!loggedIn && !onLogin) return '/login';
      if (loggedIn && onLogin) return await AuthStorage.homeRoute;

      final canEnroll = await AuthStorage.canManageEnrollments;
      if (state.matchedLocation.startsWith('/secretary') && !canEnroll) {
        return await AuthStorage.homeRoute;
      }
      return null;
    },
    routes: [
      GoRoute(path: '/login', builder: (_, __) => const LoginScreen()),
      GoRoute(path: '/children', builder: (_, __) => const ChildrenScreen()),
      GoRoute(
        path: '/children/:studentId',
        builder: (context, state) => ChildDetailScreen(
          studentId: state.pathParameters['studentId']!,
          studentName: state.uri.queryParameters['name'] ?? 'Élève',
        ),
      ),
      GoRoute(path: '/direction/dashboard', builder: (_, __) => const DirectionDashboardScreen()),
      GoRoute(path: '/promoteur/dashboard', builder: (_, __) => const PromoteurDashboardScreen()),
      GoRoute(path: '/secretary/home', builder: (_, __) => const SecretaryHomeScreen()),
      GoRoute(
        path: '/secretary/enrollment',
        builder: (context, state) => EnrollmentWizardScreen(
          isReinscription: state.uri.queryParameters['mode'] == 're',
        ),
      ),
      GoRoute(path: '/teacher/assignments', builder: (_, __) => const TeacherAssignmentsScreen()),
      GoRoute(
        path: '/teacher/classes/:classRoomId',
        builder: (context, state) => TeacherClassScreen(
          classRoomId: state.pathParameters['classRoomId']!,
          courseId: state.uri.queryParameters['courseId'] ?? '',
          academicYearId: state.uri.queryParameters['yearId'] ?? '',
          courseName: state.uri.queryParameters['course'] ?? 'Cours',
          className: state.uri.queryParameters['class'] ?? 'Classe',
        ),
      ),
      GoRoute(
        path: '/teacher/classes/:classRoomId/evaluations',
        builder: (context, state) => TeacherEvaluationsScreen(
          classRoomId: state.pathParameters['classRoomId']!,
          courseId: state.uri.queryParameters['courseId'] ?? '',
          academicYearId: state.uri.queryParameters['yearId'] ?? '',
          courseName: state.uri.queryParameters['course'] ?? 'Cours',
          className: state.uri.queryParameters['class'] ?? 'Classe',
        ),
      ),
      GoRoute(
        path: '/teacher/evaluations/:evaluationId/grades',
        builder: (context, state) => TeacherGradeEntryScreen(
          evaluationId: state.pathParameters['evaluationId']!,
          title: state.uri.queryParameters['title'] ?? 'Notes',
          maxScore: int.tryParse(state.uri.queryParameters['max'] ?? '20') ?? 20,
          classRoomId: state.uri.queryParameters['classRoomId'] ?? '',
        ),
      ),
    ],
  );
});

class _AuthRefreshListenable extends ChangeNotifier {
  _AuthRefreshListenable(this._ref) {
    _ref.listen(authStateProvider, (_, __) => notifyListeners());
  }

  final Ref _ref;
}

Future<void> logout(WidgetRef ref, BuildContext context) async {
  await ref.read(authRepositoryProvider).logout();
  await ref.read(authStateProvider.notifier).setLoggedIn(false);
  if (context.mounted) context.go('/login');
}

Future<String?> currentUserName() => AuthStorage.userName;
