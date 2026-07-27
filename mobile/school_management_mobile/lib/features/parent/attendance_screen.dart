import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'attendance/attendance_v2_screen.dart';

/// Point d'entrée shell existant — délègue au module présences V2.
class ParentAttendanceScreen extends ConsumerWidget {
  const ParentAttendanceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return const ParentAttendanceV2Screen();
  }
}
