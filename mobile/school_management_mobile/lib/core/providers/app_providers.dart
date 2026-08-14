import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../auth/auth_storage.dart';
import '../config/api_config.dart';
import '../connection/connection_mode_notifier.dart';
import '../../features/auth/auth_repository.dart';
import '../../features/parent/parent_repository.dart';
import '../../features/teacher/teacher_repository.dart';
import '../../features/direction/direction_repository.dart';
import '../../features/promoteur/promoteur_dashboard_repository.dart';
import '../../features/enrollment/enrollment_repository.dart';
import '../../features/enrollment/geography_repository.dart';
import '../../features/secretary/secretary_student_repository.dart';
import '../../features/admin/daf_student_repository.dart';
import '../../features/admin/admin_finance_repository.dart';
import '../../features/admin/admin_personnel_repository.dart';

export '../connection/connection_mode_notifier.dart';
export '../connection/write_policy.dart';

final authRepositoryProvider = Provider((ref) => AuthRepository());

final apiClientProvider = Provider<ApiClient>((ref) {
  final snap = ref.watch(connectionModeProvider);
  final raw = snap.baseUrl ?? ApiConfig.effectiveLocalBaseUrl;
  final url = ApiConfig.isValidBaseUrl(raw) ? ApiConfig.normalize(raw) : ApiConfig.effectiveLocalBaseUrl;
  return ApiClient(
    baseUrl: url,
    onSessionExpired: () => ref.read(authStateProvider.notifier).setLoggedIn(false),
  );
});

final parentRepositoryProvider =
    Provider((ref) => ParentRepository(ref.watch(apiClientProvider)));
final teacherRepositoryProvider =
    Provider((ref) => TeacherRepository(ref.watch(apiClientProvider)));
final directionRepositoryProvider =
    Provider((ref) => DirectionRepository(ref.watch(apiClientProvider)));
final promoteurDashboardRepositoryProvider =
    Provider((ref) => PromoteurDashboardRepository(ref.watch(apiClientProvider)));
final enrollmentRepositoryProvider =
    Provider((ref) => EnrollmentRepository(ref.watch(apiClientProvider)));
final geographyRepositoryProvider =
    Provider((ref) => GeographyRepository(ref.watch(apiClientProvider)));
final secretaryStudentRepositoryProvider =
    Provider((ref) => SecretaryStudentRepository(ref.watch(apiClientProvider)));
final dafStudentRepositoryProvider =
    Provider((ref) => DafStudentRepository(ref.watch(apiClientProvider)));
final adminFinanceRepositoryProvider =
    Provider((ref) => AdminFinanceRepository(ref.watch(apiClientProvider)));
final adminPersonnelRepositoryProvider =
    Provider((ref) => AdminPersonnelRepository(ref.watch(apiClientProvider)));

final authStateProvider =
    StateNotifierProvider<AuthNotifier, AsyncValue<bool>>((ref) => AuthNotifier());

class AuthNotifier extends StateNotifier<AsyncValue<bool>> {
  AuthNotifier() : super(const AsyncValue.loading()) {
    refresh();
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    try {
      var loggedIn = await AuthStorage.isLoggedIn;
      if (loggedIn && !await AuthStorage.sessionMatchesActiveSchool) {
        await AuthStorage.clearSession();
        loggedIn = false;
      }
      state = AsyncValue.data(loggedIn);
    } catch (e, st) {
      state = AsyncValue.error(e, st);
    }
  }

  Future<void> setLoggedIn(bool value) async {
    state = AsyncValue.data(value);
  }
}
