import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import '../../core/api/api_client.dart';
import 'models/parent_models.dart';

class ParentRepository {
  ParentRepository(this._api);

  final ApiClient _api;

  Future<List<ParentChild>> getChildren() => _api.getList(
        '/api/v1/parent/children',
        ParentChild.fromJson,
      );

  Future<List<ParentPayment>> getPayments(String studentId) => _api.getList(
        '/api/v1/parent/children/$studentId/payments',
        ParentPayment.fromJson,
      );

  Future<ParentPaymentSummary> getPaymentSummary(String studentId) async {
    try {
      return await _api.getObject(
        '/api/v1/parent/children/$studentId/payment-summary',
        ParentPaymentSummary.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      final situations = await getFeeSituations(studentId);
      return situations.overallSummary;
    }
  }

  Future<ParentFeeSituations> getFeeSituations(String studentId) async {
    try {
      return await _api.getObject(
        '/api/v1/parent/children/$studentId/fee-situations',
        ParentFeeSituations.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return ParentFeeSituations.empty;
    }
  }

  Future<List<ParentBulletin>> getBulletins(String studentId) => _api.getList(
        '/api/v1/parent/children/$studentId/bulletins',
        ParentBulletin.fromJson,
      );

  Future<ParentSubscription> getSubscription() async {
    try {
      return await _api.getObject(
        '/api/mobile/subscription',
        ParentSubscription.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return ParentSubscription.freeDefault;
    }
  }

  Future<ParentGradesOverview> getGrades(String studentId) async {
    try {
      return await _api.getObject(
        '/api/v1/parent/children/$studentId/grades',
        ParentGradesOverview.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return ParentGradesOverview.empty();
    }
  }

  Future<List<ParentCommunicationItem>> getCommunications(String studentId) async {
    try {
      return await _api.getList(
        '/api/v1/parent/children/$studentId/communications',
        ParentCommunicationItem.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return const [];
    }
  }

  Future<List<ParentNotificationItem>> getNotifications({
    String? category,
    String? query,
  }) async {
    try {
      final params = <String, String>{};
      if (category != null && category.isNotEmpty) {
        params['category'] = category;
      }
      if (query != null && query.trim().isNotEmpty) {
        params['q'] = query.trim();
      }
      final qs = params.isEmpty
          ? ''
          : '?${params.entries.map((e) => '${e.key}=${Uri.encodeQueryComponent(e.value)}').join('&')}';
      return await _api.getList(
        '/api/v1/parent/notifications$qs',
        ParentNotificationItem.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return const [];
    }
  }

  /// Delta inbox (Foreground Service / catch-up SignalR).
  Future<List<ParentNotificationItem>> getNotificationChanges({
    String? afterId,
    DateTime? since,
    int take = 50,
  }) async {
    try {
      final params = <String, String>{
        'take': '$take',
      };
      if (afterId != null && afterId.trim().isNotEmpty) {
        params['afterId'] = afterId.trim();
      }
      if (since != null) {
        params['since'] = since.toUtc().toIso8601String();
      }
      if (!params.containsKey('afterId') && !params.containsKey('since')) {
        return const [];
      }
      final qs =
          '?${params.entries.map((e) => '${e.key}=${Uri.encodeQueryComponent(e.value)}').join('&')}';
      return await _api.getList(
        '/api/v1/parent/notifications/changes$qs',
        ParentNotificationItem.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return const [];
    }
  }

  Future<void> acknowledgeNotificationDelivered(String notificationId) async {
    try {
      await _api.post(
        '/api/v1/parent/notifications/$notificationId/delivered',
        null,
      );
    } catch (_) {}
  }

  Future<int> getUnreadNotificationCount() async {
    try {
      final data = await _api.getObject(
        '/api/v1/parent/notifications/unread-count',
        (json) => json,
      );
      return (data['count'] as num?)?.toInt() ?? 0;
    } catch (_) {
      return 0;
    }
  }

  Future<void> markNotificationRead(String notificationId) async {
    await _api.post('/api/v1/parent/notifications/$notificationId/read', null);
  }

  Future<void> markAllNotificationsRead() async {
    await _api.post('/api/v1/parent/notifications/read-all', null);
  }

  Future<void> registerDeviceToken(String token, {String platform = 'android'}) async {
    await _api.post('/api/v1/parent/notifications/device-token', {
      'token': token,
      'platform': platform,
    });
  }

  Future<List<ParentAttendanceDay>> getAttendance(String studentId) async {
    try {
      return await _api.getList(
        '/api/v1/parent/children/$studentId/attendance',
        ParentAttendanceDay.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return const [];
    }
  }

  Future<Uint8List?> getChildPhotoBytes(String studentId) async {
    try {
      final bytes = await _api.getBytes('/api/v1/parent/children/$studentId/photo');
      return Uint8List.fromList(bytes);
    } catch (_) {
      return null;
    }
  }

  /// Same desktop receipt model: fee-type statement PDF for the payment.
  Future<void> openPaymentReceipt(ParentPayment payment) async {
    final feeQuery = payment.feeTypeId != null && payment.feeTypeId!.isNotEmpty
        ? '?feeTypeId=${payment.feeTypeId}'
        : '';
    final bytes = await _api.getBytes(
      '/api/v1/parent/payments/${payment.id}/receipt/pdf$feeQuery',
    );
    final dir = await getTemporaryDirectory();
    final file = File(
      '${dir.path}/recu-${payment.receiptNumber.replaceAll(RegExp(r"[^\w\-]+"), "_")}.pdf',
    );
    await file.writeAsBytes(bytes, flush: true);
    await OpenFilex.open(file.path);
  }

  Future<void> openBulletinPdf(String studentId, ParentBulletin bulletin) async {
    final bytes = await _api.getBytes(
      '/api/v1/parent/children/$studentId/bulletins/${bulletin.academicPeriodId}/pdf',
    );
    final dir = await getTemporaryDirectory();
    final safe = bulletin.periodName.replaceAll(RegExp(r'[^\w\-]+'), '_');
    final file = File('${dir.path}/bulletin-$safe.pdf');
    await file.writeAsBytes(bytes, flush: true);
    await OpenFilex.open(file.path);
  }

  static bool _isNetworkFailure(Object e) {
    if (e is! DioException) return false;
    return switch (e.type) {
      DioExceptionType.connectionTimeout ||
      DioExceptionType.sendTimeout ||
      DioExceptionType.receiveTimeout ||
      DioExceptionType.connectionError =>
        true,
      DioExceptionType.unknown => e.error is SocketException,
      _ => false,
    };
  }
}
