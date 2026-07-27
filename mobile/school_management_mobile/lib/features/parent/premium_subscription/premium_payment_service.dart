import 'dart:async';

import 'models/premium_payment_models.dart';
import 'premium_payment_repository.dart';

/// Service métier paiement Premium (polling + orchestration).
class PremiumPaymentService {
  PremiumPaymentService(this._repository);

  final PremiumPaymentRepository _repository;

  Future<PremiumPaymentInitResult> initiate({
    required PremiumPlanKind plan,
    required PremiumPaymentMethodKind method,
    required String phoneNumber,
  }) {
    return _repository.initiate(
      plan: plan,
      method: method,
      phoneNumber: phoneNumber,
    );
  }

  /// Poll jusqu'à un statut terminal ou timeout.
  Future<PremiumPaymentStatusResult> waitUntilSettled(
    String paymentId, {
    Duration interval = const Duration(seconds: 2),
    Duration timeout = const Duration(seconds: 45),
  }) async {
    final deadline = DateTime.now().add(timeout);
    PremiumPaymentStatusResult? last;
    while (DateTime.now().isBefore(deadline)) {
      last = await _repository.getStatus(paymentId);
      if (_isTerminal(last.status)) return last;
      await Future<void>.delayed(interval);
    }
    return last ??
        PremiumPaymentStatusResult(
          paymentId: paymentId,
          transactionNumber: '',
          status: PremiumPaymentStatusKind.expired,
          amount: 0,
          currency: 'USD',
          durationLabel: '',
          paymentMethod: '',
          phoneNumber: '',
          failureReason: 'Délai de confirmation dépassé.',
        );
  }

  Future<List<PremiumPaymentHistoryItem>> history() => _repository.getHistory();

  Future<List<int>> invoicePdf(String paymentId) =>
      _repository.downloadInvoicePdf(paymentId);

  bool _isTerminal(PremiumPaymentStatusKind status) => switch (status) {
        PremiumPaymentStatusKind.success ||
        PremiumPaymentStatusKind.refused ||
        PremiumPaymentStatusKind.expired ||
        PremiumPaymentStatusKind.cancelled =>
          true,
        _ => false,
      };
}
