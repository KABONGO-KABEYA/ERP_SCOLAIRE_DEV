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
    if (statusCode == 403) {
      return 'Action non autorisée sur ce serveur.';
    }
    if (statusCode != null && statusCode >= 500) {
      return 'Le serveur Cloud a rencontré une erreur. Réessayez dans un instant.';
    }
  }

  return 'Connexion impossible. Vérifiez vos identifiants.';
}

/// Message d'erreur pour écrans métier (dashboard, listes…), pas seulement le login.
String resolveDashboardErrorMessage(Object error) {
  if (error is DioException) {
    final responseData = error.response?.data;
    if (responseData is Map<String, dynamic>) {
      final apiMessage = responseData['message'] ?? responseData['Message'];
      if (apiMessage is String && apiMessage.trim().isNotEmpty) {
        return apiMessage.trim();
      }
    }

    final statusCode = error.response?.statusCode;
    if (statusCode == 401) {
      return 'Session expirée. Veuillez vous reconnecter.';
    }
    if (statusCode != null && statusCode >= 500) {
      return 'Le serveur Cloud a rencontré une erreur. Réessayez dans un instant.';
    }
    if (statusCode == 403) {
      return 'Lecture seule Cloud : cette action n\'est pas disponible.';
    }
    if (error.type == DioExceptionType.connectionTimeout ||
        error.type == DioExceptionType.receiveTimeout ||
        error.type == DioExceptionType.connectionError) {
      return 'Impossible de joindre le serveur. Vérifiez votre connexion Internet.';
    }
  }

  final raw = error.toString();
  if (_isGenericDioMessage(raw) || raw.startsWith('DioException')) {
    return 'Impossible de charger les données. Réessayez.';
  }
  return raw;
}

bool _isGenericDioMessage(String message) {
  return message.startsWith('DioException') ||
      message.contains('status code of') ||
      message.contains('RequestOptions.validateStatus');
}
