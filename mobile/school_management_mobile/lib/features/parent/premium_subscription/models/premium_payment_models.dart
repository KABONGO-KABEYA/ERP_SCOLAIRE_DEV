/// Modèles du parcours de paiement Premium (feature isolée).
library;

enum PremiumPlanKind { monthly, annual }

enum PremiumPaymentMethodKind { airtel, orange, mpesa }

enum PremiumPaymentStatusKind {
  idle,
  pending,
  processing,
  success,
  refused,
  expired,
  cancelled,
}

extension PremiumPlanKindX on PremiumPlanKind {
  String get apiValue => switch (this) {
        PremiumPlanKind.monthly => 'monthly',
        PremiumPlanKind.annual => 'annual',
      };

  String get label => switch (this) {
        PremiumPlanKind.monthly => 'Mensuel',
        PremiumPlanKind.annual => 'Annuel (année scolaire)',
      };

  String get durationLabel => switch (this) {
        PremiumPlanKind.monthly => '1 mois',
        PremiumPlanKind.annual => '1 année scolaire',
      };

  double get amountUsd => switch (this) {
        PremiumPlanKind.monthly => 0.50,
        PremiumPlanKind.annual => 1.50,
      };

  String get priceLabel => switch (this) {
        PremiumPlanKind.monthly => '0,50 USD / mois',
        PremiumPlanKind.annual => '1,50 USD / année',
      };
}

extension PremiumPaymentMethodKindX on PremiumPaymentMethodKind {
  String get apiValue => switch (this) {
        PremiumPaymentMethodKind.airtel => 'airtel',
        PremiumPaymentMethodKind.orange => 'orange',
        PremiumPaymentMethodKind.mpesa => 'mpesa',
      };

  String get label => switch (this) {
        PremiumPaymentMethodKind.airtel => 'Airtel Money',
        PremiumPaymentMethodKind.orange => 'Orange Money',
        PremiumPaymentMethodKind.mpesa => 'M-Pesa',
      };

  String get assetPath => switch (this) {
        PremiumPaymentMethodKind.airtel =>
          'assets/images/payments/airtel_money.png',
        PremiumPaymentMethodKind.orange =>
          'assets/images/payments/orange_money.png',
        PremiumPaymentMethodKind.mpesa => 'assets/images/payments/m-pesa.png',
      };

  /// Préfixe indicatif RDC pour le réseau.
  String get phonePrefixHint => switch (this) {
        PremiumPaymentMethodKind.airtel => '099',
        PremiumPaymentMethodKind.orange => '089',
        PremiumPaymentMethodKind.mpesa => '081',
      };

  bool isValidPhone(String raw) {
    final digits = raw.replaceAll(RegExp(r'\D'), '');
    var phone = digits;
    if (phone.startsWith('243') && phone.length >= 12) {
      phone = '0${phone.substring(3)}';
    }
    if (phone.length != 10 || !phone.startsWith('0')) return false;
    final prefix = phone.substring(0, 3);
    return switch (this) {
      PremiumPaymentMethodKind.airtel =>
        prefix == '099' || prefix == '097' || prefix == '098',
      PremiumPaymentMethodKind.orange =>
        prefix == '089' || prefix == '084' || prefix == '085' || prefix == '080',
      PremiumPaymentMethodKind.mpesa =>
        prefix == '081' || prefix == '082' || prefix == '083',
    };
  }
}

PremiumPaymentStatusKind parsePremiumPaymentStatus(String? raw) {
  return switch ((raw ?? '').toLowerCase()) {
    'pending' || 'enattente' => PremiumPaymentStatusKind.pending,
    'processing' || 'encours' || 'inprogress' => PremiumPaymentStatusKind.processing,
    'success' || 'succeeded' || 'paid' || 'reussi' => PremiumPaymentStatusKind.success,
    'refused' || 'failed' || 'rejected' => PremiumPaymentStatusKind.refused,
    'expired' => PremiumPaymentStatusKind.expired,
    'cancelled' || 'canceled' => PremiumPaymentStatusKind.cancelled,
    _ => PremiumPaymentStatusKind.idle,
  };
}

class PremiumPaymentInitResult {
  const PremiumPaymentInitResult({
    required this.paymentId,
    required this.transactionNumber,
    required this.status,
    required this.amount,
    required this.currency,
    required this.durationLabel,
  });

  final String paymentId;
  final String transactionNumber;
  final PremiumPaymentStatusKind status;
  final double amount;
  final String currency;
  final String durationLabel;

  factory PremiumPaymentInitResult.fromJson(Map<String, dynamic> json) =>
      PremiumPaymentInitResult(
        paymentId: json['paymentId']?.toString() ?? '',
        transactionNumber: json['transactionNumber'] as String? ?? '',
        status: parsePremiumPaymentStatus(json['status']?.toString()),
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        currency: json['currency'] as String? ?? 'USD',
        durationLabel: json['durationLabel'] as String? ?? '',
      );
}

class PremiumPaymentStatusResult {
  const PremiumPaymentStatusResult({
    required this.paymentId,
    required this.transactionNumber,
    required this.status,
    required this.amount,
    required this.currency,
    required this.durationLabel,
    required this.paymentMethod,
    required this.phoneNumber,
    this.failureReason,
    this.updatedAt,
  });

  final String paymentId;
  final String transactionNumber;
  final PremiumPaymentStatusKind status;
  final double amount;
  final String currency;
  final String durationLabel;
  final String paymentMethod;
  final String phoneNumber;
  final String? failureReason;
  final DateTime? updatedAt;

  factory PremiumPaymentStatusResult.fromJson(Map<String, dynamic> json) =>
      PremiumPaymentStatusResult(
        paymentId: json['paymentId']?.toString() ?? '',
        transactionNumber: json['transactionNumber'] as String? ?? '',
        status: parsePremiumPaymentStatus(json['status']?.toString()),
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        currency: json['currency'] as String? ?? 'USD',
        durationLabel: json['durationLabel'] as String? ?? '',
        paymentMethod: json['paymentMethod'] as String? ?? '',
        phoneNumber: json['phoneNumber'] as String? ?? '',
        failureReason: json['failureReason'] as String?,
        updatedAt: json['updatedAt'] != null
            ? DateTime.tryParse(json['updatedAt'].toString())
            : null,
      );
}

class PremiumPaymentHistoryItem {
  const PremiumPaymentHistoryItem({
    required this.id,
    required this.transactionNumber,
    required this.date,
    required this.amount,
    required this.currency,
    required this.paymentMethod,
    required this.status,
    required this.phoneNumber,
    required this.durationLabel,
    required this.invoiceAvailable,
  });

  final String id;
  final String transactionNumber;
  final DateTime date;
  final double amount;
  final String currency;
  final String paymentMethod;
  final PremiumPaymentStatusKind status;
  final String phoneNumber;
  final String durationLabel;
  final bool invoiceAvailable;

  factory PremiumPaymentHistoryItem.fromJson(Map<String, dynamic> json) =>
      PremiumPaymentHistoryItem(
        id: json['id']?.toString() ?? '',
        transactionNumber: json['transactionNumber'] as String? ?? '',
        date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime.now(),
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        currency: json['currency'] as String? ?? 'USD',
        paymentMethod: json['paymentMethod'] as String? ?? '',
        status: parsePremiumPaymentStatus(json['status']?.toString()),
        phoneNumber: json['phoneNumber'] as String? ?? '',
        durationLabel: json['durationLabel'] as String? ?? '',
        invoiceAvailable: json['invoiceAvailable'] as bool? ?? false,
      );
}

/// État immutable du parcours checkout + paiement.
class PremiumPaymentState {
  const PremiumPaymentState({
    this.plan = PremiumPlanKind.annual,
    this.method,
    this.phone = '',
    this.status = PremiumPaymentStatusKind.idle,
    this.paymentId,
    this.transactionNumber,
    this.amount,
    this.currency = 'USD',
    this.durationLabel,
    this.failureReason,
    this.isSubmitting = false,
    this.errorMessage,
  });

  final PremiumPlanKind plan;
  final PremiumPaymentMethodKind? method;
  final String phone;
  final PremiumPaymentStatusKind status;
  final String? paymentId;
  final String? transactionNumber;
  final double? amount;
  final String currency;
  final String? durationLabel;
  final String? failureReason;
  final bool isSubmitting;
  final String? errorMessage;

  double get displayAmount => amount ?? plan.amountUsd;
  String get displayDuration => durationLabel ?? plan.durationLabel;

  PremiumPaymentState copyWith({
    PremiumPlanKind? plan,
    PremiumPaymentMethodKind? method,
    String? phone,
    PremiumPaymentStatusKind? status,
    String? paymentId,
    String? transactionNumber,
    double? amount,
    String? currency,
    String? durationLabel,
    String? failureReason,
    bool? isSubmitting,
    String? errorMessage,
    bool clearMethod = false,
    bool clearError = false,
  }) {
    return PremiumPaymentState(
      plan: plan ?? this.plan,
      method: clearMethod ? null : (method ?? this.method),
      phone: phone ?? this.phone,
      status: status ?? this.status,
      paymentId: paymentId ?? this.paymentId,
      transactionNumber: transactionNumber ?? this.transactionNumber,
      amount: amount ?? this.amount,
      currency: currency ?? this.currency,
      durationLabel: durationLabel ?? this.durationLabel,
      failureReason: failureReason ?? this.failureReason,
      isSubmitting: isSubmitting ?? this.isSubmitting,
      errorMessage: clearError ? null : (errorMessage ?? this.errorMessage),
    );
  }
}
