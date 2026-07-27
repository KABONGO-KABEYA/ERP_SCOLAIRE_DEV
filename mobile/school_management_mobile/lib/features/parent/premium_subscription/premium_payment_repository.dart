import '../../../core/api/api_client.dart';
import 'models/premium_payment_models.dart';

/// Chemins API Premium — jamais d'URL absolue hardcodée.
abstract final class PremiumPaymentApiPaths {
  static const initiate = '/api/v1/mobile/subscription/payment/initiate';
  static const status = '/api/v1/mobile/subscription/payment/status';
  static const callback = '/api/v1/mobile/subscription/payment/callback';
  static const history = '/api/v1/mobile/subscription/payments';

  static String invoicePdf(String paymentId) =>
      '/api/v1/mobile/subscription/payments/$paymentId/invoice/pdf';
}

/// Repository — appelle ApiClient uniquement.
class PremiumPaymentRepository {
  PremiumPaymentRepository(this._api);

  final ApiClient _api;

  Future<PremiumPaymentInitResult> initiate({
    required PremiumPlanKind plan,
    required PremiumPaymentMethodKind method,
    required String phoneNumber,
  }) {
    return _api.postObject(
      PremiumPaymentApiPaths.initiate,
      {
        'plan': plan.apiValue,
        'paymentMethod': method.apiValue,
        'phoneNumber': phoneNumber,
      },
      PremiumPaymentInitResult.fromJson,
    );
  }

  Future<PremiumPaymentStatusResult> getStatus(String paymentId) {
    return _api.postObject(
      PremiumPaymentApiPaths.status,
      {'paymentId': paymentId},
      PremiumPaymentStatusResult.fromJson,
    );
  }

  Future<void> sendCallback({
    required String paymentId,
    required String status,
    String? message,
  }) {
    return _api.post(
      PremiumPaymentApiPaths.callback,
      {
        'paymentId': paymentId,
        'status': status,
        'message': message,
      },
    );
  }

  Future<List<PremiumPaymentHistoryItem>> getHistory() {
    return _api.getList(
      PremiumPaymentApiPaths.history,
      PremiumPaymentHistoryItem.fromJson,
    );
  }

  Future<List<int>> downloadInvoicePdf(String paymentId) {
    return _api.getBytes(PremiumPaymentApiPaths.invoicePdf(paymentId));
  }
}
