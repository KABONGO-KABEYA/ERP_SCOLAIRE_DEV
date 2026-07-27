import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'communications/communications_v2_screen.dart';

/// Point d'entrée shell existant — délègue au module communications V2.
class ParentCommunicationsScreen extends ConsumerWidget {
  const ParentCommunicationsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return const ParentCommunicationsV2Screen();
  }
}
