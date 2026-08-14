import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'admin_personnel_models.dart';

/// Liste du personnel en consultation seule (DAF mobile).
class PersonnelListScreen extends ConsumerStatefulWidget {
  const PersonnelListScreen({super.key});

  @override
  ConsumerState<PersonnelListScreen> createState() => _PersonnelListScreenState();
}

class _PersonnelListScreenState extends ConsumerState<PersonnelListScreen> {
  final _searchController = TextEditingController();
  Timer? _debounce;
  List<PersonnelListItem> _items = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _searchController.addListener(_onSearchChanged);
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onSearchChanged() {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 400), _load);
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final items = await ref.read(adminPersonnelRepositoryProvider).listPersonnel(
            search: _searchController.text.trim().isEmpty ? null : _searchController.text.trim(),
          );
      if (!mounted) return;
      setState(() => _items = items);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Personnel'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
        actions: [
          IconButton(icon: const Icon(Icons.refresh_rounded), onPressed: _load),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'Rechercher (nom, matricule…)',
                prefixIcon: const Icon(Icons.search),
                suffixIcon: _searchController.text.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          _searchController.clear();
                          _load();
                        },
                      )
                    : null,
                filled: true,
                fillColor: Colors.white,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(12), borderSide: BorderSide.none),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                '${_items.length} membre(s) — consultation seule',
                style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
              ),
            ),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.all(16),
              child: Text(_error!, style: const TextStyle(color: ErpColors.danger)),
            ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _items.isEmpty
                    ? const Center(child: Text('Aucun personnel trouvé'))
                    : RefreshIndicator(
                        onRefresh: _load,
                        child: ListView.separated(
                          padding: const EdgeInsets.all(16),
                          itemCount: _items.length,
                          separatorBuilder: (_, __) => const SizedBox(height: 8),
                          itemBuilder: (_, index) => _PersonnelTile(item: _items[index]),
                        ),
                      ),
          ),
        ],
      ),
    );
  }
}

class _PersonnelTile extends StatelessWidget {
  const _PersonnelTile({required this.item});

  final PersonnelListItem item;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: ErpColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(item.fullName, style: const TextStyle(fontWeight: FontWeight.w700, color: ErpColors.navy)),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: item.isActive ? const Color(0xFFECFDF5) : const Color(0xFFFEF2F2),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  item.statusLabel,
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w700,
                    color: item.isActive ? ErpColors.success : ErpColors.danger,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Text('N° ${item.employeeNumber}', style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
          if (item.functionName != null || item.departmentName != null)
            Text(
              [item.functionName, item.departmentName].whereType<String>().join(' • '),
              style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary),
            ),
          Wrap(
            spacing: 6,
            runSpacing: 4,
            children: [
              _ChipLabel(text: item.categoryLabel),
              _ChipLabel(text: item.contractLabel),
              if (item.seniorityLabel.isNotEmpty) _ChipLabel(text: item.seniorityLabel),
            ],
          ),
          if (item.phone != null || item.email != null) ...[
            const SizedBox(height: 6),
            if (item.phone != null)
              Text(item.phone!, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
            if (item.email != null)
              Text(item.email!, style: const TextStyle(fontSize: 12, color: ErpColors.textSecondary)),
          ],
        ],
      ),
    );
  }
}

class _ChipLabel extends StatelessWidget {
  const _ChipLabel({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(top: 6),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: const Color(0xFFF1F5F9),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Text(text, style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w600)),
    );
  }
}
