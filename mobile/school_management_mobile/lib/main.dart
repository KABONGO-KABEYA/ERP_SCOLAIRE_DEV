import 'package:flutter/material.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app.dart';
import 'features/parent/offline/parent_offline_cache.dart';
import 'features/parent/notifications/parent_push_foreground_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  FlutterForegroundTask.initCommunicationPort();
  await ParentOfflineCache.init();
  await ParentPushForegroundService.init();
  runApp(const ProviderScope(child: SchoolManagementApp()));
}
