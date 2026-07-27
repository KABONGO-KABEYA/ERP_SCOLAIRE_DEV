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

  Future<List<ParentNotificationItem>> getNotifications() async {
    try {
      return await _api.getList(
        '/api/v1/parent/notifications',
        ParentNotificationItem.fromJson,
      );
    } catch (e) {
      if (_isNetworkFailure(e)) rethrow;
      return const [];
    }
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
