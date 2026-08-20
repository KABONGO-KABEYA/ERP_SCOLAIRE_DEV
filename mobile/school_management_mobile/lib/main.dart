import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'app.dart';
import 'core/device/device_identity.dart';
import 'features/parent/offline/parent_offline_cache.dart';
import 'features/parent/notifications/parent_push_foreground_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  FlutterForegroundTask.initCommunicationPort();

  // Ne jamais bloquer le splash indéfiniment (ANR TECNO / Keystore).
  await _guardedStartup(DeviceIdentity.ensureInitialized);
  await _guardedStartup(ParentOfflineCache.init);
  await _guardedStartup(ParentPushForegroundService.init);

  runApp(const ProviderScope(child: SchoolManagementApp()));
}

Future<void> _guardedStartup(Future<void> Function() init) async {
  try {
    await init().timeout(const Duration(seconds: 4));
  } catch (e, st) {
    debugPrint('Startup init skipped/timeout: $e\n$st');
  }
}
