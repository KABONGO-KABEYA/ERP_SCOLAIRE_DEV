import 'package:flutter/material.dart';

/// Design system ERP Scolaire RDC — aligné sur le Desktop WPF.
abstract final class ErpColors {
  static const navy = Color(0xFF0B1F47);
  static const primary = Color(0xFF1D4ED8);
  static const primaryLegacy = Color(0xFF1E5EFF);
  static const sidebar = Color(0xFF0F1F3D);
  static const pageBackground = Color(0xFFF5F7FB);
  static const card = Color(0xFFFFFFFF);
  static const textPrimary = Color(0xFF1F2937);
  static const textSecondary = Color(0xFF6B7280);
  static const success = Color(0xFF22C55E);
  static const warning = Color(0xFFF59E0B);
  static const danger = Color(0xFFEF4444);
  static const border = Color(0xFFE5E7EB);

  static const pageBackgroundDark = Color(0xFF111827);
  static const cardDark = Color(0xFF1F2937);
  static const textPrimaryDark = Color(0xFFF9FAFB);
  static const textSecondaryDark = Color(0xFF9CA3AF);
  static const borderDark = Color(0xFF374151);
}

abstract final class ErpSpacing {
  static const page = 20.0;
  static const card = 20.0;
  static const section = 16.0;
  static const item = 20.0;
  static const cardRadius = 16.0;
  static const buttonRadius = 10.0;
  static const inputRadius = 10.0;
  static const buttonHeight = 42.0;
}

abstract final class ErpTheme {
  static ThemeData light() {
    const scheme = ColorScheme(
      brightness: Brightness.light,
      primary: ErpColors.primary,
      onPrimary: Colors.white,
      secondary: ErpColors.primary,
      onSecondary: Colors.white,
      error: ErpColors.danger,
      onError: Colors.white,
      surface: ErpColors.card,
      onSurface: ErpColors.textPrimary,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: ErpColors.pageBackground,
      fontFamily: 'Segoe UI',
      textTheme: const TextTheme(
        headlineLarge: TextStyle(fontSize: 28, fontWeight: FontWeight.w600, color: ErpColors.textPrimary),
        headlineMedium: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: ErpColors.textPrimary),
        titleLarge: TextStyle(fontSize: 18, fontWeight: FontWeight.w600, color: ErpColors.textPrimary),
        bodyLarge: TextStyle(fontSize: 14, fontWeight: FontWeight.w500, color: ErpColors.textPrimary),
        bodyMedium: TextStyle(fontSize: 13, fontWeight: FontWeight.normal, color: ErpColors.textSecondary),
      ),
      cardTheme: CardThemeData(
        color: ErpColors.card,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
          side: const BorderSide(color: ErpColors.border),
        ),
        margin: EdgeInsets.zero,
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(120, ErpSpacing.buttonHeight),
          backgroundColor: ErpColors.primary,
          foregroundColor: Colors.white,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpSpacing.buttonRadius)),
          textStyle: const TextStyle(fontSize: 14, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(100, ErpSpacing.buttonHeight),
          foregroundColor: ErpColors.textPrimary,
          side: const BorderSide(color: ErpColors.border),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpSpacing.buttonRadius)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
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
        hintStyle: const TextStyle(color: ErpColors.textSecondary),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: ErpColors.card,
        indicatorColor: ErpColors.primary.withValues(alpha: 0.12),
        labelTextStyle: WidgetStateProperty.all(
          const TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
        ),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: ErpColors.card,
        foregroundColor: ErpColors.textPrimary,
        elevation: 0,
        centerTitle: false,
      ),
    );
  }

  static ThemeData dark() {
    const scheme = ColorScheme(
      brightness: Brightness.dark,
      primary: ErpColors.primary,
      onPrimary: Colors.white,
      secondary: ErpColors.primary,
      onSecondary: Colors.white,
      error: ErpColors.danger,
      onError: Colors.white,
      surface: ErpColors.cardDark,
      onSurface: ErpColors.textPrimaryDark,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: scheme,
      scaffoldBackgroundColor: ErpColors.pageBackgroundDark,
      fontFamily: 'Segoe UI',
      cardTheme: CardThemeData(
        color: ErpColors.cardDark,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
          side: const BorderSide(color: ErpColors.borderDark),
        ),
      ),
      filledButtonTheme: light().filledButtonTheme,
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(100, ErpSpacing.buttonHeight),
          foregroundColor: ErpColors.textPrimaryDark,
          side: const BorderSide(color: ErpColors.borderDark),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpSpacing.buttonRadius)),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: ErpColors.cardDark,
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
          borderSide: const BorderSide(color: ErpColors.borderDark),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
          borderSide: const BorderSide(color: ErpColors.borderDark),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(ErpSpacing.inputRadius),
          borderSide: const BorderSide(color: ErpColors.primary, width: 1.5),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: ErpColors.cardDark,
        indicatorColor: ErpColors.primary.withValues(alpha: 0.2),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: ErpColors.cardDark,
        foregroundColor: ErpColors.textPrimaryDark,
        elevation: 0,
      ),
    );
  }
}

/// Carte standard ERP avec ombre légère.
class ErpCard extends StatelessWidget {
  const ErpCard({super.key, required this.child, this.padding = const EdgeInsets.all(ErpSpacing.card)});

  final Widget child;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: isDark ? ErpColors.cardDark : ErpColors.card,
        borderRadius: BorderRadius.circular(ErpSpacing.cardRadius),
        border: Border.all(color: isDark ? ErpColors.borderDark : ErpColors.border),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.06),
            blurRadius: 24,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: child,
    );
  }
}
