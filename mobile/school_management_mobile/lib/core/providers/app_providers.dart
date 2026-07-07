import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../api/api_client.dart';
import '../auth/auth_storage.dart';
import '../../features/auth/auth_repository.dart';
import '../../features/parent/parent_repository.dart';
import '../../features/teacher/teacher_repository.dart';
import '../../features/direction/direction_repository.dart';

final authRepositoryProvider = Provider((ref) => AuthRepository());
final apiClientProvider = Provider((ref) => ApiClient());
final parentRepositoryProvider =
    Provider((ref) => ParentRepository(ref.watch(apiClientProvider)));
final teacherRepositoryProvider =
    Provider((ref) => TeacherRepository(ref.watch(apiClientProvider)));
final directionRepositoryProvider =
    Provider((ref) => DirectionRepository(ref.watch(apiClientProvider)));

final authStateProvider =
    StateNotifierProvider<AuthNotifier, AsyncValue<bool>>((ref) => AuthNotifier());

class AuthNotifier extends StateNotifier<AsyncValue<bool>> {
  AuthNotifier() : super(const AsyncValue.loading()) {
    refresh();
  }

  Future<void> refresh() async {
    state = const AsyncValue.loading();
    try {
      final loggedIn = await AuthStorage.isLoggedIn;
      state = AsyncValue.data(loggedIn);
    } catch (e, st) {
      state = AsyncValue.error(e, st);
    }
  }

  Future<void> setLoggedIn(bool value) async {
    state = AsyncValue.data(value);
  }
}
