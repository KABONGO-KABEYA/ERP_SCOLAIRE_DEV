import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app.dart';
import 'features/parent/offline/parent_offline_cache.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await ParentOfflineCache.init();
  runApp(const ProviderScope(child: SchoolManagementApp()));
}
