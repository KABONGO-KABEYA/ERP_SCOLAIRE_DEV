import 'package:flutter/material.dart';

import '../../../core/theme/erp_theme.dart';

/// Skeleton shimmer léger (sans dépendance externe).
class ParentSkeletonBox extends StatefulWidget {
  const ParentSkeletonBox({
    super.key,
    this.height = 16,
    this.width,
    this.borderRadius = 8,
  });

  final double height;
  final double? width;
  final double borderRadius;

  @override
  State<ParentSkeletonBox> createState() => _ParentSkeletonBoxState();
}

class _ParentSkeletonBoxState extends State<ParentSkeletonBox>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 1100),
    )..repeat(reverse: true);
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _controller,
      builder: (context, _) {
        final t = _controller.value;
        return Container(
          height: widget.height,
          width: widget.width,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(widget.borderRadius),
            color: Color.lerp(
              ErpColors.border.withValues(alpha: 0.55),
              ErpColors.border.withValues(alpha: 0.15),
              t,
            ),
          ),
        );
      },
    );
  }
}

class ParentSkeletonList extends StatelessWidget {
  const ParentSkeletonList({super.key, this.itemCount = 4});

  final int itemCount;

  @override
  Widget build(BuildContext context) {
    return ListView.separated(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
      itemCount: itemCount,
      separatorBuilder: (_, __) => const SizedBox(height: 12),
      itemBuilder: (_, __) => const ErpCard(
        padding: EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ParentSkeletonBox(width: 140, height: 14),
            SizedBox(height: 12),
            ParentSkeletonBox(height: 12),
            SizedBox(height: 8),
            ParentSkeletonBox(width: 220, height: 12),
          ],
        ),
      ),
    );
  }
}

class ParentEmptyState extends StatelessWidget {
  const ParentEmptyState({
    super.key,
    required this.title,
    this.subtitle,
    this.icon = Icons.inbox_outlined,
    this.action,
  });

  final String title;
  final String? subtitle;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Icon(icon, size: 36, color: ErpColors.primary.withValues(alpha: 0.7)),
          const SizedBox(height: 12),
          Text(
            title,
            textAlign: TextAlign.center,
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
          if (subtitle != null && subtitle!.trim().isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              subtitle!,
              textAlign: TextAlign.center,
              style: const TextStyle(color: ErpColors.textSecondary, fontSize: 13),
            ),
          ],
          if (action != null) ...[
            const SizedBox(height: 14),
            action!,
          ],
        ],
      ),
    );
  }
}

class ParentErrorState extends StatelessWidget {
  const ParentErrorState({
    super.key,
    required this.message,
    this.onRetry,
  });

  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      child: Column(
        children: [
          const Icon(Icons.error_outline, color: ErpColors.danger, size: 32),
          const SizedBox(height: 10),
          Text(
            message,
            textAlign: TextAlign.center,
            style: const TextStyle(color: ErpColors.textPrimary),
          ),
          if (onRetry != null) ...[
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Réessayer'),
            ),
          ],
        ],
      ),
    );
  }
}

class ParentOfflineBanner extends StatelessWidget {
  const ParentOfflineBanner({super.key, this.visible = true});

  final bool visible;

  @override
  Widget build(BuildContext context) {
    if (!visible) return const SizedBox.shrink();
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: ErpColors.warning.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: ErpColors.warning.withValues(alpha: 0.35)),
      ),
      child: const Row(
        children: [
          Icon(Icons.cloud_off_outlined, size: 18, color: ErpColors.warning),
          SizedBox(width: 8),
          Expanded(
            child: Text(
              'Données hors ligne (cache local).',
              style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600),
            ),
          ),
        ],
      ),
    );
  }
}

/// Transition douce Material 3 pour overlays locaux.
class ParentFadeSlide extends StatelessWidget {
  const ParentFadeSlide({
    super.key,
    required this.child,
    this.duration = const Duration(milliseconds: 280),
  });

  final Widget child;
  final Duration duration;

  @override
  Widget build(BuildContext context) {
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: duration,
      curve: Curves.easeOutCubic,
      builder: (context, value, child) {
        return Opacity(
          opacity: value,
          child: Transform.translate(
            offset: Offset(0, (1 - value) * 10),
            child: child,
          ),
        );
      },
      child: child,
    );
  }
}
