import 'dart:async';

/// Three consecutive failures before leaving an established local endpoint;
/// two successful checks before returning from the cloud.
class EndpointStabilityPolicy {
  EndpointStabilityPolicy({Future<void> Function(Duration)? delay})
      : _delay = delay ?? Future<void>.delayed;
  final Future<void> Function(Duration) _delay;
  static const retryTimeouts = [
    Duration(seconds: 2),
    Duration(seconds: 3),
    Duration(seconds: 4),
  ];

  Future<T?> checkLocal<T>({
    required Future<T?> Function(Duration timeout) probe,
    required bool established,
    required bool Function() isCurrent,
  }) async {
    final attempts = established ? retryTimeouts : [retryTimeouts.first];
    for (var i = 0; i < attempts.length; i++) {
      if (!isCurrent()) return null;
      final result = await probe(attempts[i]);
      if (!isCurrent()) return null;
      if (result != null) return result;
      if (i + 1 < attempts.length) {
        await _delay(Duration(milliseconds: 250 * (i + 1)));
      }
    }
    return null;
  }

  Future<T?> confirmReturn<T>({
    required T first,
    required Future<T?> Function() probe,
    required bool Function() isCurrent,
  }) async {
    await _delay(const Duration(milliseconds: 750));
    if (!isCurrent()) return null;
    final second = await probe();
    return isCurrent() ? second : null;
  }
}
