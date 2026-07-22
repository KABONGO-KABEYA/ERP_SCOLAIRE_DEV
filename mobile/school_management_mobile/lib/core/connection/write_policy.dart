import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'connection_mode.dart';
import 'connection_mode_notifier.dart';

/// Politique d'écriture mobile = mode + (rôles/permissions déjà gérés ailleurs).
final writePolicyProvider = Provider<WritePolicy>((ref) {
  final mode = ref.watch(connectionModeProvider).mode;
  return WritePolicy(mode);
});

class WritePolicy {
  const WritePolicy(this.mode);

  final ConnectionMode mode;

  bool get canMutateBusinessData => mode.allowsWrites;

  bool get canSubmitGrades => mode.allowsGradeWrites;

  bool get canEnrollStudents => mode.allowsWrites;

  String get readOnlyHint =>
      'Mode Cloud : consultation uniquement. Revenez sur le réseau de l\'école pour modifier.';
}
