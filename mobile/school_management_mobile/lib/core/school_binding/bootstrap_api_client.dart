import 'package:dio/dio.dart';

import '../config/bootstrap_config.dart';
import '../school_binding/activation_session.dart';
import '../school_binding/school_binding.dart';

class BootstrapStartRequest {
  BootstrapStartRequest({
    required this.token,
    required this.deviceId,
    this.clientHints,
  });

  final String token;
  final String deviceId;
  final Map<String, dynamic>? clientHints;

  Map<String, dynamic> toJson() => {
        'token': token,
        'deviceId': deviceId,
        if (clientHints != null) 'clientHints': clientHints,
      };
}

class BootstrapCompleteRequest {
  BootstrapCompleteRequest({
    required this.activationSessionId,
    required this.deviceId,
  });

  final String activationSessionId;
  final String deviceId;

  Map<String, dynamic> toJson() => {
        'activationSessionId': activationSessionId,
        'deviceId': deviceId,
      };
}

/// Client HTTP Bootstrap (architecture v2 §4.1 — activation uniquement).
class BootstrapApiClient {
  BootstrapApiClient({Dio? dio})
      : _dio = dio ??
            Dio(BaseOptions(
              baseUrl: BootstrapConfig.baseUrl,
              connectTimeout: const Duration(seconds: 20),
              receiveTimeout: const Duration(seconds: 30),
              headers: {'Content-Type': 'application/json'},
            ));

  final Dio _dio;

  Future<ActivationSession> start(BootstrapStartRequest request) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/activation/start',
      data: request.toJson(),
    );
    return ActivationSession.fromJson(response.data ?? {});
  }

  Future<SchoolBinding> complete(BootstrapCompleteRequest request) async {
    final response = await _dio.post<Map<String, dynamic>>(
      '/activation/complete',
      data: request.toJson(),
    );
    return SchoolBinding.fromJson(response.data ?? {});
  }
}
