import 'package:flutter/material.dart';

import '../../../core/theme/erp_theme.dart';

/// Carte d'accueil secrétariat avec ombre légèrement renforcée.
class SecretaryHomeCard extends StatelessWidget {
  const SecretaryHomeCard({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: isDark ? ErpColors.cardDark : ErpColors.card,
        borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
        border: Border.all(
          color: isDark ? ErpColors.borderDark : ErpColors.border,
        ),
        boxShadow: [
          BoxShadow(
            color: ErpColors.primary.withValues(alpha: 0.06),
            blurRadius: 28,
            offset: const Offset(0, 8),
          ),
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 12,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: child,
    );
  }
}

/// Bouton rempli avec légère animation au toucher.
class SecretaryFilledButton extends StatefulWidget {
  const SecretaryFilledButton({
    super.key,
    required this.onPressed,
    required this.icon,
    required this.label,
  });

  final VoidCallback? onPressed;
  final IconData icon;
  final String label;

  @override
  State<SecretaryFilledButton> createState() => _SecretaryFilledButtonState();
}

class _SecretaryFilledButtonState extends State<SecretaryFilledButton> {
  bool _pressed = false;

  Future<void> _handlePress() async {
    setState(() => _pressed = true);
    await Future<void>.delayed(const Duration(milliseconds: 80));
    if (!mounted) return;
    setState(() => _pressed = false);
    widget.onPressed?.call();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedScale(
      scale: _pressed ? 0.97 : 1,
      duration: const Duration(milliseconds: 110),
      curve: Curves.easeOut,
      child: FilledButton.icon(
        onPressed: widget.onPressed == null ? null : _handlePress,
        icon: Icon(widget.icon),
        label: Text(widget.label),
      ),
    );
  }
}

/// Bouton contour avec légère animation au toucher.
class SecretaryOutlinedButton extends StatefulWidget {
  const SecretaryOutlinedButton({
    super.key,
    required this.onPressed,
    required this.icon,
    required this.label,
  });

  final VoidCallback? onPressed;
  final IconData icon;
  final String label;

  @override
  State<SecretaryOutlinedButton> createState() => _SecretaryOutlinedButtonState();
}

class _SecretaryOutlinedButtonState extends State<SecretaryOutlinedButton> {
  bool _pressed = false;

  Future<void> _handlePress() async {
    setState(() => _pressed = true);
    await Future<void>.delayed(const Duration(milliseconds: 80));
    if (!mounted) return;
    setState(() => _pressed = false);
    widget.onPressed?.call();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedScale(
      scale: _pressed ? 0.97 : 1,
      duration: const Duration(milliseconds: 110),
      curve: Curves.easeOut,
      child: OutlinedButton.icon(
        onPressed: widget.onPressed == null ? null : _handlePress,
        icon: Icon(widget.icon),
        label: Text(widget.label),
      ),
    );
  }
}

class SecretaryFeatureIcon extends StatelessWidget {
  const SecretaryFeatureIcon({super.key, required this.icon});

  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: ErpColors.primary.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Icon(icon, color: ErpColors.primary, size: 38),
    );
  }
}
