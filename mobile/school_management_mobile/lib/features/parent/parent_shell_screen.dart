import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/erp_theme.dart';
import 'models/parent_models.dart';
import 'parent_providers.dart';

class ParentShellScreen extends ConsumerWidget {
  const ParentShellScreen({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  static const _destinations = <_NavItem>[
    _NavItem(label: 'Accueil', icon: Icons.home_outlined, selectedIcon: Icons.home_rounded),
    _NavItem(label: 'Paiements', icon: Icons.payments_outlined, selectedIcon: Icons.payments),
    _NavItem(label: 'Notes', icon: Icons.school_outlined, selectedIcon: Icons.school, premium: true),
    _NavItem(label: 'Bulletins', icon: Icons.description_outlined, selectedIcon: Icons.description, premium: true),
    _NavItem(label: 'Comms', icon: Icons.forum_outlined, selectedIcon: Icons.forum, premium: true),
    _NavItem(label: 'Notifs', icon: Icons.notifications_outlined, selectedIcon: Icons.notifications, premium: true),
    _NavItem(label: 'Profil', icon: Icons.person_outline, selectedIcon: Icons.person),
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final features = ref.watch(parentSubscriptionProvider).valueOrNull?.features;

    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: Material(
        elevation: 8,
        color: Theme.of(context).colorScheme.surface,
        child: SafeArea(
          child: SizedBox(
            height: 68,
            child: ListView.builder(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 8),
              itemCount: _destinations.length,
              itemBuilder: (context, index) {
                final item = _destinations[index];
                final selected = navigationShell.currentIndex == index;
                final locked = item.premium && !_unlocked(features, index);
                return InkWell(
                  onTap: () => navigationShell.goBranch(
                    index,
                    initialLocation: index == navigationShell.currentIndex,
                  ),
                  borderRadius: BorderRadius.circular(12),
                  child: Container(
                    width: 76,
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Stack(
                          clipBehavior: Clip.none,
                          children: [
                            Icon(
                              selected ? item.selectedIcon : item.icon,
                              color: selected
                                  ? ErpColors.primary
                                  : ErpColors.textSecondary,
                              size: 24,
                            ),
                            if (locked)
                              Positioned(
                                right: -8,
                                top: -4,
                                child: Icon(
                                  Icons.lock,
                                  size: 12,
                                  color: ErpColors.warning.withValues(alpha: 0.95),
                                ),
                              ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          item.label,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                            color: selected
                                ? ErpColors.primary
                                : ErpColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
        ),
      ),
    );
  }

  bool _unlocked(ParentFeatureFlags? features, int index) {
    if (features == null) return false;
    return switch (index) {
      2 => features.notes,
      3 => features.bulletins,
      4 => features.communications,
      5 => features.notifications,
      _ => true,
    };
  }
}

class _NavItem {
  const _NavItem({
    required this.label,
    required this.icon,
    required this.selectedIcon,
    this.premium = false,
  });

  final String label;
  final IconData icon;
  final IconData selectedIcon;
  final bool premium;
}

void goParentBranch(BuildContext context, int index) {
  final shell = StatefulNavigationShell.maybeOf(context);
  if (shell != null) {
    shell.goBranch(index);
  } else {
    const paths = [
      '/parent/home',
      '/parent/payments',
      '/parent/notes',
      '/parent/bulletins',
      '/parent/communications',
      '/parent/notifications',
      '/parent/profile',
    ];
    if (index >= 0 && index < paths.length) {
      context.go(paths[index]);
    }
  }
}
