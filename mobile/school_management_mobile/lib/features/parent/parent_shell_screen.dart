import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/erp_theme.dart';
import 'models/parent_models.dart';
import 'parent_providers.dart';

class ParentShellScreen extends ConsumerWidget {
  const ParentShellScreen({super.key, required this.navigationShell});

  final StatefulNavigationShell navigationShell;

  /// 5 destinations principales (hub Scolarité + Messages).
  static const _destinations = <_NavItem>[
    _NavItem(label: 'Accueil', icon: Icons.home_outlined, selectedIcon: Icons.home_rounded),
    _NavItem(label: 'Paiements', icon: Icons.payments_outlined, selectedIcon: Icons.payments),
    _NavItem(
      label: 'Scolarité',
      icon: Icons.school_outlined,
      selectedIcon: Icons.school,
      premium: true,
    ),
    _NavItem(
      label: 'Messages',
      icon: Icons.forum_outlined,
      selectedIcon: Icons.forum,
      premium: true,
    ),
    _NavItem(label: 'Profil', icon: Icons.person_outline, selectedIcon: Icons.person),
  ];

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(parentNotificationPollingProvider);
    final features = ref.watch(parentSubscriptionProvider).valueOrNull?.features;
    final unread = ref.watch(parentNotificationUnreadCountProvider).valueOrNull ??
        (ref.watch(parentNotificationInboxProvider).valueOrNull ?? const [])
            .where((n) => !n.isRead)
            .length;

    return Scaffold(
      body: navigationShell,
      bottomNavigationBar: Material(
        elevation: 8,
        color: Theme.of(context).colorScheme.surface,
        child: SafeArea(
          child: SizedBox(
            height: 64,
            child: Row(
              children: [
                for (var index = 0; index < _destinations.length; index++)
                  Expanded(
                    child: _NavButton(
                      item: _destinations[index],
                      selected: navigationShell.currentIndex == index,
                      locked: _destinations[index].premium &&
                          !_hubUnlocked(features, index),
                      badgeCount: index == 3 ? unread : 0,
                      onTap: () => navigationShell.goBranch(
                        index,
                        initialLocation: index == navigationShell.currentIndex,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  bool _hubUnlocked(ParentFeatureFlags? features, int index) {
    if (features == null) return false;
    return switch (index) {
      2 => features.notes || features.bulletins || features.attendance,
      3 => features.communications || features.notifications,
      _ => true,
    };
  }
}

class _NavButton extends StatelessWidget {
  const _NavButton({
    required this.item,
    required this.selected,
    required this.locked,
    required this.onTap,
    this.badgeCount = 0,
  });

  final _NavItem item;
  final bool selected;
  final bool locked;
  final VoidCallback onTap;
  final int badgeCount;

  @override
  Widget build(BuildContext context) {
    final color = selected ? ErpColors.primary : ErpColors.textSecondary;
    return InkWell(
      onTap: onTap,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Stack(
            clipBehavior: Clip.none,
            children: [
              Icon(
                selected ? item.selectedIcon : item.icon,
                color: color,
                size: 24,
              ),
              if (locked)
                Positioned(
                  right: -6,
                  top: -4,
                  child: Icon(
                    Icons.lock,
                    size: 11,
                    color: ErpColors.accentGold,
                  ),
                ),
              if (!locked && badgeCount > 0)
                Positioned(
                  right: -8,
                  top: -4,
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
                    decoration: BoxDecoration(
                      color: ErpColors.danger,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(
                      badgeCount > 9 ? '9+' : '$badgeCount',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 9,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
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
              color: color,
            ),
          ),
        ],
      ),
    );
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

/// Indices shell : 0 Accueil, 1 Paiements, 2 Scolarité, 3 Messages, 4 Profil.
void goParentBranch(BuildContext context, int index) {
  final shell = StatefulNavigationShell.maybeOf(context);
  if (shell != null) {
    shell.goBranch(index);
  } else {
    const paths = [
      '/parent/home',
      '/parent/payments',
      '/parent/scolarite',
      '/parent/messages',
      '/parent/profile',
    ];
    if (index >= 0 && index < paths.length) {
      context.go(paths[index]);
    }
  }
}
