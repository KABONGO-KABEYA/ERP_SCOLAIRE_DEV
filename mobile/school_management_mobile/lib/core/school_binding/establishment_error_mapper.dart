import 'package:dio/dio.dart';

/// Messages utilisateur pour le flux `/establishment/*` (sans secret).
abstract final class EstablishmentErrorMapper {
  static String fromDio(DioException e) {
    final status = e.response?.statusCode;
    final detail = _extractError(e.response?.data);

    if (e.type == DioExceptionType.connectionTimeout ||
        e.type == DioExceptionType.receiveTimeout ||
        e.type == DioExceptionType.sendTimeout ||
        e.type == DioExceptionType.connectionError ||
        status == null && e.response == null) {
      return 'Bootstrap indisponible. Vérifiez votre connexion Internet et réessayez.';
    }

    if (detail != null && detail.isNotEmpty) {
      return mapServerMessage(detail, statusCode: status);
    }

    return 'Liaison établissement refusée${status != null ? ' ($status)' : ''}.';
  }

  static String mapServerMessage(String detail, {int? statusCode}) {
    final lower = detail.toLowerCase();

    if (lower.contains('type incorrect') ||
        lower.contains('non valide pour l\'établissement') ||
        lower.contains('token_type')) {
      return 'QR invalide : ce code n\'est pas un QR établissement '
          '(ex. invitation parent). Scannez le QR établissement de l\'école.';
    }
    if (lower.contains('révoqué') || lower.contains('revoque')) {
      return 'QR établissement révoqué. Demandez un nouveau QR à l\'école.';
    }
    if (lower.contains('version')) {
      return 'Version du QR incorrecte. Demandez le QR à jour à l\'école.';
    }
    if (lower.contains('introuvable') || lower.contains('registre')) {
      return 'École inconnue sur Bootstrap. Contactez l\'administration.';
    }
    if (lower.contains('expir')) {
      return 'Session expirée. Scannez à nouveau le QR établissement.';
    }
    if (lower.contains('deviceid incompatible') ||
        lower.contains('device id incompatible')) {
      return 'Appareil incompatible entre démarrage et finalisation. Recommencez le scan.';
    }
    if (lower.contains('signature') ||
        lower.contains('invalide') ||
        lower.contains('manquant')) {
      return 'QR établissement invalide.';
    }
    if (statusCode == 503 || statusCode == 502 || statusCode == 504) {
      return 'Bootstrap indisponible. Réessayez plus tard.';
    }
    return detail;
  }

  static String? _extractError(Object? body) {
    if (body is Map && body['error'] != null) {
      return body['error'].toString();
    }
    if (body is String && body.isNotEmpty) return body;
    return null;
  }
}
