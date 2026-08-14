import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers/app_providers.dart';
import '../../core/theme/erp_theme.dart';
import 'admin_finance_models.dart';

class PricingCategoriesScreen extends ConsumerStatefulWidget {
  const PricingCategoriesScreen({super.key});

  @override
  ConsumerState<PricingCategoriesScreen> createState() => _PricingCategoriesScreenState();
}

class _PricingCategoriesScreenState extends ConsumerState<PricingCategoriesScreen> {
  final _searchController = TextEditingController();
  List<StudentPricingAssignment> _items = [];
  List<PricingCategoryOption> _categories = [];
  String? _academicYearId;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final prereq = await ref.read(dafStudentRepositoryProvider).getPrerequisites();
      final categories = await ref.read(adminFinanceRepositoryProvider).getPricingCategories();
      if (!mounted) return;
      setState(() {
        _academicYearId = prereq.currentAcademicYearId;
        _categories = categories.where((c) => c.isActive).toList();
      });
      await _search();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _search() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final page = await ref.read(adminFinanceRepositoryProvider).searchPricingAssignments(
            academicYearId: _academicYearId,
            search: _searchController.text,
          );
      if (!mounted) return;
      setState(() => _items = page.items);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _changeCategory(StudentPricingAssignment student) async {
    if (_categories.isEmpty) return;

    final selected = await showModalBottomSheet<PricingCategoryOption>(
      context: context,
      showDragHandle: true,
      builder: (context) => SafeArea(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
              child: Text(
                student.fullName,
                style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
              ),
            ),
            Flexible(
              child: ListView.builder(
                shrinkWrap: true,
                itemCount: _categories.length,
                itemBuilder: (context, index) {
                  final category = _categories[index];
                  return ListTile(
                    title: Text(category.name),
                    subtitle: Text(category.code),
                    trailing: student.feePricingCategoryId == category.id
                        ? const Icon(Icons.check_circle, color: ErpColors.primary)
                        : null,
                    onTap: () => Navigator.pop(context, category),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );

    if (selected == null || selected.id == student.feePricingCategoryId) return;

    try {
      final updated = await ref.read(adminFinanceRepositoryProvider).updatePricingAssignment(
            enrollmentId: student.enrollmentId,
            feePricingCategoryId: selected.id,
          );
      if (!mounted) return;
      setState(() {
        final index = _items.indexWhere((s) => s.enrollmentId == student.enrollmentId);
        if (index >= 0) {
          _items[index] = updated;
        }
      });
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Catégorie mise à jour : ${updated.feePricingCategoryName}')),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString()), backgroundColor: ErpColors.danger),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: ErpColors.pageBackground,
      appBar: AppBar(
        title: const Text('Catégories tarifaires'),
        backgroundColor: Colors.white,
        foregroundColor: ErpColors.navy,
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(16),
            child: TextField(
              controller: _searchController,
              decoration: InputDecoration(
                hintText: 'Rechercher un élève…',
                filled: true,
                fillColor: Colors.white,
                prefixIcon: const Icon(Icons.search),
                suffixIcon: IconButton(
                  icon: const Icon(Icons.arrow_forward_rounded),
                  onPressed: _search,
                ),
              ),
              onSubmitted: (_) => _search(),
            ),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Text(_error!, style: const TextStyle(color: ErpColors.danger)),
            ),
          Expanded(
            child: _loading && _items.isEmpty
                ? const Center(child: CircularProgressIndicator())
                : RefreshIndicator(
                    onRefresh: _search,
                    child: _items.isEmpty
                        ? ListView(
                            physics: const AlwaysScrollableScrollPhysics(),
                            children: const [
                              SizedBox(height: 120),
                              Center(child: Text('Aucun élève trouvé.')),
                            ],
                          )
                        : ListView.separated(
                            padding: const EdgeInsets.fromLTRB(16, 0, 16, 16),
                            itemCount: _items.length,
                            separatorBuilder: (_, __) => const SizedBox(height: 8),
                            itemBuilder: (context, index) {
                              final item = _items[index];
                              return Container(
                                decoration: BoxDecoration(
                                  color: Colors.white,
                                  borderRadius: BorderRadius.circular(12),
                                ),
                                child: ListTile(
                                  title: Text(item.fullName, style: const TextStyle(fontWeight: FontWeight.w600)),
                                  subtitle: Text('${item.registrationNumber} · ${item.className}\n${item.feePricingCategoryName}'),
                                  isThreeLine: true,
                                  trailing: const Icon(Icons.edit_outlined, color: ErpColors.primary),
                                  onTap: () => _changeCategory(item),
                                ),
                              );
                            },
                          ),
                  ),
          ),
        ],
      ),
    );
  }
}
