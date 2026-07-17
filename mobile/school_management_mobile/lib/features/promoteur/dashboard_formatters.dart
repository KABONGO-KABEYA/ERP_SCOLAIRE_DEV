import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../core/theme/erp_theme.dart';

final _money = NumberFormat('#,##0', 'fr_FR');
final _pct = NumberFormat('+0.0;-0.0', 'fr_FR');
final _time = DateFormat('HH:mm');
final _day = DateFormat("EEEE d MMMM yyyy", 'fr_FR');

String formatMoney(num value, [String currency = 'CDF']) =>
    '${_money.format(value)} $currency';

String formatPercent(num value) => '${_pct.format(value)} %';

String formatTime(DateTime dt) => _time.format(dt.toLocal());

String formatLongDate(DateTime dt) {
  final raw = _day.format(dt.toLocal());
  return raw.isEmpty ? '' : '${raw[0].toUpperCase()}${raw.substring(1)}';
}

Color parseHexColor(String hex, {Color fallback = ErpColors.primary}) {
  var cleaned = hex.replaceAll('#', '').trim();
  if (cleaned.length == 6) cleaned = 'FF$cleaned';
  if (cleaned.length != 8) return fallback;
  final value = int.tryParse(cleaned, radix: 16);
  return value == null ? fallback : Color(value);
}

Color alertColor(String severity) => switch (severity.toLowerCase()) {
      'success' => ErpColors.success,
      'warning' => ErpColors.warning,
      'danger' => ErpColors.danger,
      _ => ErpColors.primary,
    };
