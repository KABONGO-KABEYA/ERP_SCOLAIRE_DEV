import 'package:flutter/material.dart';

import '../theme/erp_theme.dart';

/// Barre de statut connexion B2B — fond carte, liseré gauche 3px.
class ErpBanner extends StatelessWidget {
  const ErpBanner({
    super.key,
    required this.label,
    required this.icon,
    required this.color,
    this.onTap,
    this.trailing,
    this.busy = false,
  });

  final String label;
  final IconData icon;
  final Color color;
  final VoidCallback? onTap;
  final Widget? trailing;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isDark ? ErpColors.cardDark : ErpColors.cardBackground;
    final border = isDark ? ErpColors.borderDark : ErpColors.border;

    return Material(
      color: bg,
      child: SafeArea(
        bottom: false,
        child: InkWell(
          onTap: busy ? null : onTap,
          child: Container(
            height: 30,
            decoration: BoxDecoration(
              color: bg,
              border: Border(bottom: BorderSide(color: border)),
            ),
            child: Row(
              children: [
                Container(width: 3, height: double.infinity, color: color),
                const SizedBox(width: 10),
                if (busy)
                  SizedBox(
                    width: 14,
                    height: 14,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: color,
                    ),
                  )
                else
                  Icon(icon, size: 15, color: color),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w600,
                      color: ErpColors.textPrimary,
                    ),
                  ),
                ),
                if (trailing != null) ...[
                  const SizedBox(width: 8),
                  trailing!,
                ],
                const SizedBox(width: 10),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

/// Badge « Premium » verrouillé — fond gold léger.
class ErpLockChip extends StatelessWidget {
  const ErpLockChip({super.key, this.label = 'Premium', this.compact = false});

  final String label;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: compact ? 6 : 8,
        vertical: compact ? 2 : 3,
      ),
      decoration: BoxDecoration(
        color: ErpColors.accentGold.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            Icons.lock,
            size: compact ? 10 : 12,
            color: ErpColors.accentGold,
          ),
          if (label.trim().isNotEmpty) ...[
            const SizedBox(width: 4),
            Text(
              label,
              style: TextStyle(
                fontSize: compact ? 10 : 11,
                fontWeight: FontWeight.w700,
                color: const Color(0xFF8A6A1A),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// Point notification non lue.
class ErpBadgeDot extends StatelessWidget {
  const ErpBadgeDot({super.key, this.size = 7});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: const BoxDecoration(
        color: ErpColors.danger,
        shape: BoxShape.circle,
      ),
    );
  }
}

/// Barre de recherche.
class ErpSearchBar extends StatelessWidget {
  const ErpSearchBar({
    super.key,
    required this.controller,
    this.hintText = 'Rechercher…',
    this.onChanged,
    this.onSubmitted,
    this.onClear,
    this.autofocus = false,
  });

  final TextEditingController controller;
  final String hintText;
  final ValueChanged<String>? onChanged;
  final ValueChanged<String>? onSubmitted;
  final VoidCallback? onClear;
  final bool autofocus;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 42,
      child: TextField(
        controller: controller,
        autofocus: autofocus,
        textInputAction: TextInputAction.search,
        onChanged: onChanged,
        onSubmitted: onSubmitted,
        style: const TextStyle(fontSize: 14, color: ErpColors.textPrimary),
        decoration: InputDecoration(
          hintText: hintText,
          isDense: true,
          contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          prefixIcon: const Icon(Icons.search, size: 20, color: ErpColors.textSecondary),
          suffixIcon: controller.text.isEmpty
              ? null
              : IconButton(
                  icon: const Icon(Icons.clear, size: 18),
                  onPressed: () {
                    controller.clear();
                    onClear?.call();
                    onChanged?.call('');
                  },
                ),
          filled: true,
          fillColor: Colors.white,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
            borderSide: const BorderSide(color: ErpColors.border),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
            borderSide: const BorderSide(color: ErpColors.border),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
            borderSide: const BorderSide(color: ErpColors.primary, width: 1.5),
          ),
        ),
      ),
    );
  }
}

class ErpLoadingState extends StatelessWidget {
  const ErpLoadingState({super.key, this.message});

  final String? message;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(ErpSpacing.page),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const SizedBox(
              width: 28,
              height: 28,
              child: CircularProgressIndicator(strokeWidth: 2.5),
            ),
            if (message != null) ...[
              const SizedBox(height: 12),
              Text(
                message!,
                textAlign: TextAlign.center,
                style: const TextStyle(color: ErpColors.textSecondary),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class ErpEmptyState extends StatelessWidget {
  const ErpEmptyState({
    super.key,
    required this.title,
    this.description,
    this.icon = Icons.inbox_outlined,
    this.action,
  });

  final String title;
  final String? description;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return _StateCard(
      icon: icon,
      iconColor: ErpColors.textSecondary,
      title: title,
      description: description,
      action: action,
    );
  }
}

class ErpOfflineState extends StatelessWidget {
  const ErpOfflineState({
    super.key,
    this.title = 'Données en cache',
    this.description =
        'Vous êtes hors ligne. Les informations affichées proviennent du cache local.',
    this.onRetrySync,
    this.action,
  });

  final String title;
  final String? description;
  final VoidCallback? onRetrySync;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return _StateCard(
      icon: Icons.cloud_off_outlined,
      iconColor: ErpColors.modeCache,
      title: title,
      description: description,
      action: action ??
          (onRetrySync == null
              ? null
              : OutlinedButton(
                  onPressed: onRetrySync,
                  child: const Text('Réessayer'),
                )),
    );
  }
}

class ErpErrorState extends StatelessWidget {
  const ErpErrorState({
    super.key,
    required this.message,
    this.onRetry,
    this.title = 'Une erreur est survenue',
  });

  final String title;
  final String message;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return _StateCard(
      icon: Icons.error_outline,
      iconColor: ErpColors.danger,
      title: title,
      description: message,
      action: onRetry == null
          ? null
          : FilledButton(
              onPressed: onRetry,
              child: const Text('Réessayer'),
            ),
    );
  }
}

class _StateCard extends StatelessWidget {
  const _StateCard({
    required this.icon,
    required this.iconColor,
    required this.title,
    this.description,
    this.action,
  });

  final IconData icon;
  final Color iconColor;
  final String title;
  final String? description;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return ErpCard(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 36, color: iconColor),
          const SizedBox(height: 12),
          Text(
            title,
            textAlign: TextAlign.center,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w700,
              color: ErpColors.textPrimary,
            ),
          ),
          if (description != null && description!.trim().isNotEmpty) ...[
            const SizedBox(height: 8),
            Text(
              description!,
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontSize: 13,
                height: 1.4,
                color: ErpColors.textSecondary,
              ),
            ),
          ],
          if (action != null) ...[
            const SizedBox(height: 16),
            action!,
          ],
        ],
      ),
    );
  }
}
