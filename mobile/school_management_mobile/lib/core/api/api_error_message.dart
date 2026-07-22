import 'package:dio/dio.dart';

/// Message lisible pour l'utilisateur à partir d'une erreur API / réseau.
String resolveApiErrorMessage(Object error) {
  if (error is DioException) {
    final responseData = error.response?.data;
    if (responseData is Map<String, dynamic>) {
      final apiMessage = responseData['message'] ?? responseData['Message'];
      if (apiMessage is String && apiMessage.trim().isNotEmpty) {
        return apiMessage.trim();
      }

      final validationErrors = responseData['errors'];
      if (validationErrors is Map) {
        final messages = <String>[];
        for (final value in validationErrors.values) {
          if (value is List && value.isNotEmpty) {
            messages.add(value.first.toString());
          } else if (value is String && value.trim().isNotEmpty) {
            messages.add(value.trim());
          }
        }
        if (messages.isNotEmpty) return messages.join('\n');
      }
    }

    final message = error.message?.trim();
    if (message != null && message.isNotEmpty && !_isGenericDioMessage(message)) {
      return message;
    }

    final statusCode = error.response?.statusCode;
    if (statusCode == 401) {
      return 'Nom d\'utilisateur ou mot de passe incorrect.';
    }
    if (statusCode == 400) {
      return 'Vérifiez l\'identifiant et le mot de passe saisis.';
    }
  }

  return 'Connexion impossible. Vérifiez vos identifiants.';
}

bool _isGenericDioMessage(String message) {
  return message.startsWith('DioException') ||
      message.contains('status code of') ||
      message.contains('RequestOptions.validateStatus');
}
