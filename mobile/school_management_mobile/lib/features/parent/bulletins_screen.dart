import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'bulletins/bulletins_v2_screen.dart';

/// Point d'entrée shell existant — délègue au module bulletins V2.
class ParentBulletinsScreen extends ConsumerWidget {
  const ParentBulletinsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return const ParentBulletinsV2Screen();
  }
}
