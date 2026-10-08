import 'dart:async';

/// Invalidates pending work as soon as the active school changes.
abstract final class ConnectionContext {
  static int _generation = 0;
  static final _changes = StreamController<int>.broadcast(sync: true);
  static int get generation => _generation;
  static Stream<int> get changes => _changes.stream;
  static void invalidate() => _changes.add(++_generation);
}

/// Endpoint changes wait for writes already sent to finish. Failed writes are
/// never replayed on another endpoint.
class ConnectionActivity {
  int _writes = 0;
  Completer<void>? _idle;
  static final instance = ConnectionActivity();

  void Function() beginWrite() {
    if (_writes++ == 0) _idle = Completer<void>();
    var released = false;
    return () {
      if (released) return;
      released = true;
      if (--_writes == 0) {
        _idle?.complete();
        _idle = null;
      }
    };
  }

  Future<void> waitForIdle() async {
    while (_writes > 0) {
      await _idle!.future;
    }
  }
}
