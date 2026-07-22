import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers/app_providers.dart';
import '../../../core/theme/erp_theme.dart';
import 'account_helpers.dart';
import 'secretary_account_screen.dart';

class SecretaryAboutScreen extends ConsumerStatefulWidget {
  const SecretaryAboutScreen({super.key});

  @override
  ConsumerState<SecretaryAboutScreen> createState() => _SecretaryAboutScreenState();
}

class _SecretaryAboutScreenState extends ConsumerState<SecretaryAboutScreen> {
  String? _schoolName;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _loadSchool();
  }

  Future<void> _loadSchool() async {
    try {
      final name = await ref.read(accountRepositoryProvider).getSchoolName();
      if (!mounted) return;
      setState(() {
        _schoolName = name;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final connection = ref.watch(connectionModeProvider);
    final server = connection.baseUrl ?? '—';

    return Scaffold(
      appBar: AppBar(title: const Text('À propos')),
      body: ListView(
        padding: const EdgeInsets.all(ErpSpacing.page),
        children: [
          ErpCard(
            child: Column(
              children: [
                Image.asset('assets/icon/app_icon.png', height: 72, fit: BoxFit.contain),
                const SizedBox(height: 16),
                Text(
                  kMobileAppName,
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                const SizedBox(height: 8),
                Text(
                  'Version $kMobileAppVersion',
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          _InfoRow(
            icon: Icons.dns_outlined,
            label: 'Serveur connecté',
            value: server,
          ),
          const SizedBox(height: 12),
          _InfoRow(
            icon: Icons.school_outlined,
            label: 'Établissement',
            value: _loading ? 'Chargement…' : (_schoolName ?? 'Établissement'),
          ),
        ],
      ),
    );
  }
}

class _InfoRow extends StatelessWidget {
  const _InfoRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  final IconData icon;
  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, color: ErpColors.primary, size: 22),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(label, style: Theme.of(context).textTheme.bodyMedium),
                const SizedBox(height: 4),
                Text(
                  value,
                  style: Theme.of(context).textTheme.bodyLarge,
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
