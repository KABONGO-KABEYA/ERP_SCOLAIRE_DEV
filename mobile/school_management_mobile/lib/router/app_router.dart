import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../core/auth/auth_storage.dart';
import '../core/providers/app_providers.dart';
import '../core/connection/connection_mode_notifier.dart';
import '../core/school_binding/school_binding_gate.dart';
import '../features/parent/activation/parent_activation_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/parent/attendance_screen.dart';
import '../features/parent/bulletins_screen.dart';
import '../features/parent/change_password_screen.dart';
import '../features/parent/communications_screen.dart';
import '../features/parent/dashboard_screen.dart';
import '../features/parent/notes_screen.dart';
import '../features/parent/notifications_screen.dart';
import '../features/parent/hubs/parent_hub_screens.dart';
import '../features/parent/parent_shell_screen.dart';
import '../features/parent/payments_screen.dart';
import '../features/parent/profile_screen.dart';
import '../features/parent/subscription_screen.dart';
import '../features/parent/premium_subscription/screens/payment_confirm_screen.dart';
import '../features/parent/premium_subscription/screens/payment_method_screen.dart';
import '../features/parent/premium_subscription/screens/payment_status_screen.dart';
import '../features/parent/premium_subscription/screens/payment_success_screen.dart';
import '../features/parent/premium_subscription/screens/phone_entry_screen.dart';
import '../features/parent/premium_subscription/screens/subscription_history_screen.dart';
import '../features/teacher/assignments_screen.dart';
import '../features/teacher/class_courses_screen.dart';
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
      final onActivate = state.matchedLocation.startsWith('/parent/activate');
      final connection = ref.read(connectionModeProvider);

      if (connection.requiresReauthentication) {
        if (!loggedIn && onLogin) return null;
        await ref.read(authStateProvider.notifier).setLoggedIn(false);
        if (!onLogin && !onActivate) {
          return '/login?reason=server_instance';
        }
      }

      if (loggedIn &&
          await AuthStorage.isParent &&
          await SchoolBindingGate.shouldBlockParentSessionWithoutBinding()) {
        await ref.read(authStateProvider.notifier).setLoggedIn(false);
        if (!onActivate) {
          return '/parent/activate?reason=binding_required';
        }
      }

      if (!loggedIn && !onLogin && !onActivate) return '/login';
      if (loggedIn && onLogin) return await AuthStorage.homeRoute;

      if (state.matchedLocation == '/children') {
        return '/parent/home';
      }

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
      GoRoute(
        path: '/parent/activate',
        builder: (context, state) {
          final token = state.uri.queryParameters['token'];
          return ParentActivationScreen(initialToken: token);
        },
      ),
      GoRoute(
        path: '/children',
        redirect: (_, __) => '/parent/home',
      ),
      StatefulShellRoute.indexedStack(
        builder: (context, state, navigationShell) =>
            ParentShellScreen(navigationShell: navigationShell),
        branches: [
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/parent/home',
                builder: (_, __) => const ParentDashboardScreen(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/parent/payments',
                builder: (_, __) => const ParentPaymentsScreen(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/parent/scolarite',
                builder: (_, __) => const ParentScolariteHubScreen(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/parent/messages',
                builder: (_, __) => const ParentMessagesHubScreen(),
              ),
            ],
          ),
          StatefulShellBranch(
            routes: [
              GoRoute(
                path: '/parent/profile',
                builder: (_, __) => const ParentProfileScreen(),
              ),
            ],
          ),
        ],
      ),
      GoRoute(
        path: '/parent/notes',
        builder: (_, __) => const ParentNotesScreen(),
      ),
      GoRoute(
        path: '/parent/bulletins',
        builder: (_, __) => const ParentBulletinsScreen(),
      ),
      GoRoute(
        path: '/parent/communications',
        builder: (_, __) => const ParentCommunicationsScreen(),
      ),
      GoRoute(
        path: '/parent/notifications',
        builder: (_, __) => const ParentNotificationsScreen(),
      ),
      GoRoute(
        path: '/parent/subscription',
        builder: (_, __) => const ParentSubscriptionScreen(),
      ),
      GoRoute(
        path: '/parent/subscription/payment-method',
        builder: (_, __) => const PremiumPaymentMethodScreen(),
      ),
      GoRoute(
        path: '/parent/subscription/phone',
        builder: (_, __) => const PremiumPhoneEntryScreen(),
      ),
      GoRoute(
        path: '/parent/subscription/confirm',
        builder: (_, __) => const PremiumPaymentConfirmScreen(),
      ),
      GoRoute(
        path: '/parent/subscription/status',
        builder: (_, __) => const PremiumPaymentStatusScreen(),
      ),
      GoRoute(
        path: '/parent/subscription/success',
        builder: (_, __) => const PremiumPaymentSuccessScreen(),
      ),
      GoRoute(
        path: '/parent/subscription/history',
        builder: (_, __) => const PremiumSubscriptionHistoryScreen(),
      ),
      GoRoute(
        path: '/parent/attendance',
        builder: (_, __) => const ParentAttendanceScreen(),
      ),
      GoRoute(
        path: '/parent/change-password',
        builder: (_, __) => const ParentChangePasswordScreen(),
      ),
      GoRoute(path: '/direction/dashboard', builder: (_, __) => const DirectionDashboardScreen()),
      GoRoute(path: '/promoteur/dashboard', builder: (_, __) => const PromoteurDashboardScreen()),
      GoRoute(
        path: '/promoteur/payments',
        builder: (context, state) => PromoteurPaymentsDetailScreen(
          scope: state.uri.queryParameters['scope'] ?? 'Today',
          feeTypeId: state.uri.queryParameters['feeTypeId'],
        ),
      ),
      // Recette du mois : totaux journaliers (pas la liste élèves).
      GoRoute(
        path: '/promoteur/recette-mois',
        builder: (context, state) => PromoteurRevenueDetailScreen(
          scope: 'Month',
          feeTypeId: state.uri.queryParameters['feeTypeId'],
          currency: state.uri.queryParameters['currency'] ?? 'CDF',
        ),
      ),
      // Recette annuelle : totaux mensuels.
      GoRoute(
        path: '/promoteur/recette-annee',
        builder: (context, state) => PromoteurRevenueDetailScreen(
          scope: 'Year',
          feeTypeId: state.uri.queryParameters['feeTypeId'],
          currency: state.uri.queryParameters['currency'] ?? 'CDF',
        ),
      ),
      GoRoute(
        path: '/promoteur/revenue-detail',
        builder: (context, state) => PromoteurRevenueDetailScreen(
          scope: state.uri.queryParameters['scope'] ?? 'Month',
          feeTypeId: state.uri.queryParameters['feeTypeId'],
          currency: state.uri.queryParameters['currency'] ?? 'CDF',
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
        path: '/teacher/classes/:classRoomId/courses',
        builder: (context, state) => TeacherClassCoursesScreen(
          classRoomId: state.pathParameters['classRoomId']!,
          className: state.uri.queryParameters['name'] ?? 'Classe',
          academicYearId: state.uri.queryParameters['yearId'] ?? '',
        ),
      ),
      GoRoute(
        path: '/teacher/classes/:classRoomId/courses/:courseId/evaluations',
        builder: (context, state) => TeacherEvaluationsScreen(
          classRoomId: state.pathParameters['classRoomId']!,
          courseId: state.pathParameters['courseId']!,
          academicYearId: state.uri.queryParameters['yearId'] ?? '',
          courseName: state.uri.queryParameters['courseName'] ?? 'Cours',
          className: state.uri.queryParameters['className'] ?? 'Classe',
          maxScore: int.tryParse(state.uri.queryParameters['maxScore'] ?? '20') ?? 20,
        ),
      ),
      GoRoute(
        path: '/teacher/evaluations/:evaluationId/grades',
        builder: (context, state) => TeacherGradeEntryScreen(
          evaluationId: state.pathParameters['evaluationId']!,
          title: state.uri.queryParameters['title'] ?? 'Notes',
          maxScore: int.tryParse(state.uri.queryParameters['max'] ?? '20') ?? 20,
          classRoomId: state.uri.queryParameters['classRoomId'] ?? '',
          isOpen: state.uri.queryParameters['open'] != 'false',
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
