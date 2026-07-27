import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../core/providers/app_providers.dart';
import '../parent_providers.dart';
import 'models/premium_payment_models.dart';
import 'premium_payment_repository.dart';
import 'premium_payment_service.dart';

final premiumPaymentRepositoryProvider = Provider<PremiumPaymentRepository>(
  (ref) => PremiumPaymentRepository(ref.watch(apiClientProvider)),
);

final premiumPaymentServiceProvider = Provider<PremiumPaymentService>(
  (ref) => PremiumPaymentService(ref.watch(premiumPaymentRepositoryProvider)),
);

final premiumPaymentHistoryProvider =
    FutureProvider<List<PremiumPaymentHistoryItem>>((ref) {
  return ref.watch(premiumPaymentServiceProvider).history();
});

/// Notifier du parcours checkout / paiement Premium.
final premiumPaymentProvider =
    StateNotifierProvider<PremiumPaymentNotifier, PremiumPaymentState>((ref) {
  return PremiumPaymentNotifier(ref);
});

class PremiumPaymentNotifier extends StateNotifier<PremiumPaymentState> {
  PremiumPaymentNotifier(this._ref) : super(const PremiumPaymentState());

  final Ref _ref;

  void selectPlan(PremiumPlanKind plan) {
    state = state.copyWith(plan: plan, clearError: true);
  }

  void selectMethod(PremiumPaymentMethodKind method) {
    state = state.copyWith(method: method, clearError: true);
  }

  void setPhone(String phone) {
    state = state.copyWith(phone: phone, clearError: true);
  }

  void resetFlow() {
    state = const PremiumPaymentState();
  }

  Future<bool> confirmAndPay() async {
    final method = state.method;
    if (method == null) {
      state = state.copyWith(errorMessage: 'Choisissez un mode de paiement.');
      return false;
    }
    if (!method.isValidPhone(state.phone)) {
      state = state.copyWith(
        errorMessage:
            'Numéro invalide pour ${method.label}. Ex. ${method.phonePrefixHint}xxxxxxx',
      );
      return false;
    }

    state = state.copyWith(
      isSubmitting: true,
      status: PremiumPaymentStatusKind.processing,
      clearError: true,
    );

    try {
      final service = _ref.read(premiumPaymentServiceProvider);
      final init = await service.initiate(
        plan: state.plan,
        method: method,
        phoneNumber: state.phone,
      );

      state = state.copyWith(
        paymentId: init.paymentId,
        transactionNumber: init.transactionNumber,
        amount: init.amount,
        currency: init.currency,
        durationLabel: init.durationLabel,
        status: init.status == PremiumPaymentStatusKind.idle
            ? PremiumPaymentStatusKind.processing
            : init.status,
      );

      final settled = await service.waitUntilSettled(init.paymentId);
      state = state.copyWith(
        status: settled.status,
        failureReason: settled.failureReason,
        amount: settled.amount,
        currency: settled.currency,
        durationLabel: settled.durationLabel,
        transactionNumber: settled.transactionNumber,
        isSubmitting: false,
      );

      if (settled.status == PremiumPaymentStatusKind.success) {
        // Déverrouille Premium immédiatement (sans reconnexion).
        _ref.invalidate(parentSubscriptionProvider);
        return true;
      }
      return false;
    } catch (e) {
      state = state.copyWith(
        isSubmitting: false,
        status: PremiumPaymentStatusKind.refused,
        errorMessage: 'Impossible de finaliser le paiement. Vérifiez votre connexion.',
        failureReason: e.toString(),
      );
      return false;
    }
  }
}
