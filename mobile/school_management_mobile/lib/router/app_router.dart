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
import '../features/promoteur/detail_screens.dart';
import '../features/enrollment/enrollment_wizard_screen.dart';
import '../features/secretary/secretary_home_screen.dart';
import '../features/secretary/account/about_screen.dart';
import '../features/secretary/account/change_password_screen.dart';
import '../features/secretary/account/secretary_account_screen.dart';
import '../features/secretary/student_dossier_screen.dart';
import '../features/secretary/student_search_screen.dart';

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
      final writePolicy = ref.read(writePolicyProvider);
      if (state.matchedLocation.startsWith('/secretary/enrollment') &&
          !writePolicy.canEnrollStudents) {
        return '/secretary/home';
      }
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
      GoRoute(
        path: '/promoteur/payments',
        builder: (context, state) => PromoteurPaymentsDetailScreen(
          scope: state.uri.queryParameters['scope'] ?? 'Today',
        ),
      ),
      GoRoute(
        path: '/promoteur/expenses',
        builder: (context, state) => PromoteurExpensesDetailScreen(
          scope: state.uri.queryParameters['scope'] ?? 'Month',
          category: state.uri.queryParameters['category'],
        ),
      ),
      GoRoute(
        path: '/promoteur/debtors',
        builder: (context, state) => PromoteurDebtorsDetailScreen(
          feeTypeId: state.uri.queryParameters['feeTypeId'],
        ),
      ),
      GoRoute(
        path: '/promoteur/funds/:destinationId',
        builder: (context, state) => PromoteurFundMovementsScreen(
          destinationId: state.pathParameters['destinationId']!,
          name: state.uri.queryParameters['name'] ?? 'Compte',
        ),
      ),
      GoRoute(path: '/promoteur/students', builder: (_, __) => const PromoteurStudentsDetailScreen()),
      GoRoute(path: '/secretary/home', builder: (_, __) => const SecretaryHomeScreen()),
      GoRoute(path: '/secretary/account', builder: (_, __) => const SecretaryAccountScreen()),
      GoRoute(
        path: '/secretary/account/change-password',
        builder: (_, __) => const SecretaryChangePasswordScreen(),
      ),
      GoRoute(path: '/secretary/account/about', builder: (_, __) => const SecretaryAboutScreen()),
      GoRoute(
        path: '/secretary/students',
        builder: (_, __) => const SecretaryStudentSearchScreen(),
      ),
      GoRoute(
        path: '/secretary/students/:studentId',
        builder: (context, state) => SecretaryStudentDossierScreen(
          studentId: state.pathParameters['studentId']!,
        ),
      ),
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
    _ref.listen(connectionModeProvider, (_, __) => notifyListeners());
  }

  final Ref _ref;
}

Future<void> logout(WidgetRef ref, BuildContext context) async {
  final baseUrl = ref.read(connectionModeProvider).baseUrl;
  await ref.read(authRepositoryProvider).logout(baseUrl: baseUrl);
  await ref.read(authStateProvider.notifier).setLoggedIn(false);
  if (context.mounted) context.go('/login');
}

Future<String?> currentUserName() => AuthStorage.userName;
