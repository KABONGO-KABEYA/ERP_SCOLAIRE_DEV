import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/erp_theme.dart';
import '../models/parent_models.dart';
import '../offline/parent_offline_cache.dart';
import '../parent_providers.dart';
import '../widgets/parent_async_widgets.dart';
import '../widgets/parent_ui_widgets.dart';
import '../widgets/premium_feature_screen.dart';

/// Module Communications V2 — messagerie + pièces jointes + non-lus.
class ParentCommunicationsV2Screen extends ConsumerWidget {
  const ParentCommunicationsV2Screen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final unlocked = ref
            .watch(parentSubscriptionProvider)
            .valueOrNull
            ?.features
            .communications ??
        false;
    final selected = ref.watch(selectedChildProvider);
    ref.listen(parentChildrenProvider, (_, next) {
      next.whenData((c) => ensureChildSelected(ref, c));
    });

    return Scaffold(
      appBar: AppBar(title: const Text('Communications')),
      body: !unlocked
          ? const PremiumFeatureScreen(featureTitle: 'Communications')
          : selected == null
              ? const Center(child: Text('Sélectionnez un enfant.'))
              : _CommsBody(studentId: selected.studentId),
    );
  }
}

class _CommsBody extends ConsumerWidget {
  const _CommsBody({required this.studentId});

  final String studentId;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final async = ref.watch(parentCommunicationsProvider(studentId));
    final children = ref.watch(parentChildrenProvider).valueOrNull ?? const [];
    final readIds = ref.watch(parentReadCommunicationIdsProvider);
    final showOffline = parentHasOfflineCacheHit(
      ref.watch(parentOfflineCacheHitsProvider),
      [ParentCacheKeys.communications(studentId)],
    );

    return RefreshIndicator(
      onRefresh: () async =>
          ref.invalidate(parentCommunicationsProvider(studentId)),
      child: async.when(
        loading: () => const ParentSkeletonList(itemCount: 4),
        error: (e, _) => ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(ErpSpacing.page),
          children: [
            ParentErrorState(
              message: 'Impossible de charger les communications.\n$e',
              onRetry: () =>
                  ref.invalidate(parentCommunicationsProvider(studentId)),
            ),
          ],
        ),
        data: (items) {
          final sorted = [...items]..sort((a, b) => b.date.compareTo(a.date));
          final unread = sorted
              .where((i) => !i.isRead && !readIds.contains(i.id))
              .length;

          return ListView(
            padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
            children: [
              ParentChildSelector(
                children: children,
                selectedId: studentId,
                onChanged: (id) =>
                    ref.read(selectedChildIdProvider.notifier).state = id,
              ),
              if (children.length > 1) const SizedBox(height: 12),
              ParentOfflineBanner(visible: showOffline),
              ErpCard(
                padding: const EdgeInsets.all(14),
                child: Row(
                  children: [
                    CircleAvatar(
                      backgroundColor:
                          ErpColors.primary.withValues(alpha: 0.12),
                      child: const Icon(
                        Icons.mail_outline,
                        color: ErpColors.primary,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'Boîte de réception',
                            style: TextStyle(fontWeight: FontWeight.w700),
                          ),
                          Text(
                            unread == 0
                                ? '${sorted.length} message(s)'
                                : '$unread non lu(s) · ${sorted.length} au total',
                            style: const TextStyle(
                              fontSize: 12,
                              color: ErpColors.textSecondary,
                            ),
                          ),
                        ],
                      ),
                    ),
                    if (unread > 0)
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 4,
                        ),
                        decoration: BoxDecoration(
                          color: ErpColors.danger,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Text(
                          '$unread',
                          style: const TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w700,
                            fontSize: 12,
                          ),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              const ParentSectionTitle('Messages & documents'),
              if (sorted.isEmpty)
                const ParentEmptyState(
                  title: 'Boîte vide',
                  subtitle:
                      'Aucune communication pour le moment. Les messages de l’école apparaîtront ici.',
                  icon: Icons.forum_outlined,
                )
              else
                ...sorted.map((item) {
                  final isUnread = !item.isRead && !readIds.contains(item.id);
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: Material(
                      color: Colors.transparent,
                      child: InkWell(
                        borderRadius: BorderRadius.circular(12),
                        onTap: () => _openDetail(context, ref, item),
                        child: ErpCard(
                          padding: const EdgeInsets.all(14),
                          child: Row(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Stack(
                                children: [
                                  CircleAvatar(
                                    backgroundColor: ErpColors.primary
                                        .withValues(alpha: 0.1),
                                    child: Icon(
                                      _iconFor(item.type),
                                      color: ErpColors.primary,
                                    ),
                                  ),
                                  if (isUnread)
                                    Positioned(
                                      right: 0,
                                      top: 0,
                                      child: Container(
                                        width: 10,
                                        height: 10,
                                        decoration: const BoxDecoration(
                                          color: ErpColors.danger,
                                          shape: BoxShape.circle,
                                        ),
                                      ),
                                    ),
                                ],
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      item.title,
                                      style: TextStyle(
                                        fontWeight: isUnread
                                            ? FontWeight.w800
                                            : FontWeight.w600,
                                      ),
                                    ),
                                    const SizedBox(height: 2),
                                    Text(
                                      '${_typeLabel(item.type)} · ${DateFormat('dd/MM/yyyy HH:mm').format(item.date)}',
                                      style: const TextStyle(
                                        fontSize: 12,
                                        color: ErpColors.textSecondary,
                                      ),
                                    ),
                                    if (item.body != null &&
                                        item.body!.trim().isNotEmpty) ...[
                                      const SizedBox(height: 6),
                                      Text(
                                        item.body!,
                                        maxLines: 2,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(fontSize: 13),
                                      ),
                                    ],
                                    if (item.attachments.isNotEmpty) ...[
                                      const SizedBox(height: 8),
                                      Wrap(
                                        spacing: 6,
                                        runSpacing: 4,
                                        children: item.attachments
                                            .map(
                                              (a) => Chip(
                                                visualDensity:
                                                    VisualDensity.compact,
                                                avatar: Icon(
                                                  _attachmentIcon(a),
                                                  size: 16,
                                                ),
                                                label: Text(
                                                  a.name,
                                                  style: const TextStyle(
                                                    fontSize: 11,
                                                  ),
                                                ),
                                              ),
                                            )
                                            .toList(),
                                      ),
                                    ],
                                  ],
                                ),
                              ),
                              const Icon(
                                Icons.chevron_right,
                                color: ErpColors.textSecondary,
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  );
                }),
            ],
          );
        },
      ),
    );
  }

  Future<void> _openDetail(
    BuildContext context,
    WidgetRef ref,
    ParentCommunicationItem item,
  ) async {
    final current = ref.read(parentReadCommunicationIdsProvider);
    if (!current.contains(item.id)) {
      ref.read(parentReadCommunicationIdsProvider.notifier).state = {
        ...current,
        item.id,
      };
    }

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (ctx) {
        return DraggableScrollableSheet(
          expand: false,
          initialChildSize: 0.65,
          minChildSize: 0.4,
          maxChildSize: 0.92,
          builder: (_, controller) {
            return ListView(
              controller: controller,
              padding: const EdgeInsets.fromLTRB(20, 8, 20, 28),
              children: [
                Text(
                  item.title,
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  '${_typeLabel(item.type)} · ${DateFormat('dd/MM/yyyy HH:mm').format(item.date)}',
                  style: const TextStyle(
                    fontSize: 12,
                    color: ErpColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 16),
                Text(
                  (item.body ?? '').trim().isEmpty
                      ? 'Aucun contenu détaillé.'
                      : item.body!,
                  style: const TextStyle(height: 1.45),
                ),
                if (item.attachments.isNotEmpty) ...[
                  const SizedBox(height: 20),
                  const ParentSectionTitle('Pièces jointes'),
                  ...item.attachments.map(
                    (a) => Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: ErpCard(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 10,
                        ),
                        child: ListTile(
                          contentPadding: EdgeInsets.zero,
                          leading: Icon(
                            _attachmentIcon(a),
                            color: ErpColors.primary,
                          ),
                          title: Text(a.name),
                          subtitle: Text(
                            a.url == null || a.url!.isEmpty
                                ? _attachmentTypeLabel(a)
                                : '${_attachmentTypeLabel(a)} · disponible',
                          ),
                          trailing: Icon(
                            a.isPdf
                                ? Icons.picture_as_pdf_outlined
                                : a.isImage
                                    ? Icons.image_outlined
                                    : Icons.insert_drive_file_outlined,
                            color: ErpColors.textSecondary,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ],
            );
          },
        );
      },
    );
  }

  IconData _iconFor(String type) => switch (type.toLowerCase()) {
        'circulaire' || 'annonce' => Icons.campaign_outlined,
        'convocation' => Icons.event_note_outlined,
        'document' => Icons.attach_file,
        _ => Icons.mail_outline,
      };

  String _typeLabel(String type) => switch (type.toLowerCase()) {
        'circulaire' => 'Circulaire',
        'annonce' => 'Annonce',
        'convocation' => 'Convocation',
        'document' => 'Document',
        _ => 'Message',
      };

  IconData _attachmentIcon(ParentCommunicationAttachment a) {
    if (a.isPdf) return Icons.picture_as_pdf_outlined;
    if (a.isImage) return Icons.image_outlined;
    return Icons.insert_drive_file_outlined;
  }

  String _attachmentTypeLabel(ParentCommunicationAttachment a) {
    if (a.isPdf) return 'PDF';
    if (a.isImage) return 'Image';
    return 'Document';
  }
}
