import 'package:flutter/material.dart';
import 'package:flutter_foreground_task/flutter_foreground_task.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/connection/connection_mode_banner.dart';
import 'core/theme/erp_theme.dart';
import 'core/updates/update_bootstrap.dart';
import 'router/app_router.dart';

class SchoolManagementApp extends ConsumerWidget {
  const SchoolManagementApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final router = ref.watch(appRouterProvider);

    return WithForegroundTask(
      child: MaterialApp.router(
        title: 'ERP Scolaire RDC',
        debugShowCheckedModeBanner: false,
        theme: ErpTheme.light(),
        darkTheme: ErpTheme.dark(),
        themeMode: ThemeMode.system,
        routerConfig: router,
        builder: (context, child) {
          return UpdateBootstrap(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const ConnectionModeBanner(),
                Expanded(child: child ?? const SizedBox.shrink()),
              ],
            ),
          );
        },
      ),
    );
  }
}
