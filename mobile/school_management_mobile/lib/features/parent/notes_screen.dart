import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'grades/grades_screen.dart';

/// Point d'entrée shell existant — délègue au module grades V2.
class ParentNotesScreen extends ConsumerWidget {
  const ParentNotesScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return const ParentGradesScreen();
  }
}
