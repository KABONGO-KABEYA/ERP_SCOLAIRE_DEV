import 'dart:async';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/connection/connection_context.dart';
import 'package:school_management_mobile/core/local_server_discovery/discovery_models.dart';
import 'package:school_management_mobile/core/local_server_discovery/endpoint_stability_policy.dart';
import 'package:school_management_mobile/core/local_server_discovery/local_server_discovery.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';

const localA = 'http://192.168.1.10:5096';
const localB = 'http://192.168.1.20:5096';
const cloud = 'https://cloud.example';
SchoolBinding binding(String id) => SchoolBinding(
    schoolId: id,
    schoolName: 'School $id',
    cloudBaseUrl: cloud,
    serverInstanceId: 'local-$id',
    activationDate: DateTime.utc(2026),
    activationTokenId: 't',
    activationSessionId: 's',
    deviceId: 'd',
    protocolVersion: 2);
HealthInfo health(String id, {bool remote = false}) => HealthInfo(
    status: 'ok',
    server: remote ? 'cloud' : 'local',
    school: 'School $id',
    version: '1',
    time: DateTime.utc(2026),
    identity: ServerHealthIdentity(
        schoolId: id,
        serverInstanceId: remote ? 'cloud-instance' : 'local-$id'));

class MemoryBindings extends SchoolBindingRepository {
  MemoryBindings(this.active);
  SchoolBinding? active;
  int updates = 0;
  @override
  Future<SchoolBinding?> load() async => active;
  @override
  Future<void> updateRegisteredBinding(SchoolBinding b) async {
    updates++;
  }
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  late MemoryBindings bindings;
  late Map<String, String> cache;
  late List<String> calls;
  late Future<HealthInfo?> Function(String, Duration) probe;
  LocalServerDiscovery discovery(
          {List<String> prefixes = const ['192.168.1']}) =>
      LocalServerDiscovery(
          bindingRepository: bindings,
          healthProbe: (u, t) {
            calls.add(u);
            return probe(u, t);
          },
          prefixProvider: () async => prefixes,
          candidateProvider: () async => [localA, localB],
          lastKnownLoader: (s) async => cache[s],
          lastKnownSaver: (s, u) async {
            cache[s] = u;
          },
          stability: EndpointStabilityPolicy(delay: (_) async {}));
  setUp(() {
    bindings = MemoryBindings(binding('b'));
    cache = {};
    calls = [];
    probe = (u, _) async => switch (u) {
          localA => health('a'),
          localB => health('b'),
          cloud => health('a', remote: true),
          _ => null
        };
  });
  test('two local servers: only the active school is accepted', () async {
    final r = await discovery().discover();
    expect(r.mode, DiscoveryMode.local);
    expect(r.baseUrl, localB);
    expect(r.schoolId, 'b');
    expect(cache, {'b': localB});
  });
  test('a transient local failure keeps local mode without contacting cloud',
      () async {
    final d = discovery();
    await d.discover();
    calls.clear();
    var n = 0;
    final timeouts = <Duration>[];
    probe = (u, t) async {
      if (u == localB) {
        timeouts.add(t);
        return ++n == 1 ? null : health('b');
      }
      return u == cloud ? health('a', remote: true) : null;
    };
    expect((await d.recheck()).mode, DiscoveryMode.local);
    expect(n, 2);
    expect(timeouts, const [Duration(seconds: 2), Duration(seconds: 3)]);
    expect(calls, isNot(contains(cloud)));
  });
  test('three consecutive local failures precede cloud fallback', () async {
    final d = discovery();
    await d.discover();
    calls.clear();
    probe = (u, _) async => u == cloud ? health('a', remote: true) : null;
    expect((await d.recheck()).mode, DiscoveryMode.remote);
    expect(calls.where((u) => u == localB).length, 3);
    expect(calls.indexOf(cloud), greaterThan(calls.lastIndexOf(localB)));
    expect(cache['b'], localB);
  });
  test('cloud mode rediscovers local and requires two successful checks',
      () async {
    final d = discovery();
    probe = (u, _) async => u == cloud ? health('a', remote: true) : null;
    expect((await d.discover()).mode, DiscoveryMode.remote);
    calls.clear();
    probe = (u, _) async => u == localB ? health('b') : null;
    expect((await d.recheck()).mode, DiscoveryMode.local);
    expect(calls.where((u) => u == localB).length, 2);
    expect(calls, isNot(contains(cloud)));
  });
  test('one successful local check is insufficient to leave cloud', () async {
    final d = discovery();
    probe = (u, _) async => u == cloud ? health('a', remote: true) : null;
    await d.discover();
    var n = 0;
    probe = (u, _) async {
      if (u == localB) return ++n == 1 ? health('b') : null;
      return u == cloud ? health('a', remote: true) : null;
    };
    expect((await d.recheck()).mode, DiscoveryMode.remote);
    expect(n, 2);
  });
  test('shared cloud preserves the school binding and local instance',
      () async {
    probe = (u, _) async => u == cloud ? health('a', remote: true) : null;
    final r = await discovery().discover();
    expect(r.schoolId, 'b');
    expect(r.mode, DiscoveryMode.remote);
    expect(r.serverInstanceIdChanged, isFalse);
    expect(bindings.updates, 0);
    expect(bindings.active!.serverInstanceId, 'local-b');
  });
  test('wrong school on a remote single-school endpoint is rejected', () async {
    probe = (u, _) async => u == cloud ? health('a') : null;
    expect((await discovery().discover()).mode, DiscoveryMode.offline);
  });
  test('late old-school response cannot replace new connection or cache',
      () async {
    bindings.active = binding('a');
    final pending = Completer<HealthInfo?>();
    final started = Completer<void>();
    probe = (u, _) async {
      if (u == localA) {
        if (!started.isCompleted) started.complete();
        return pending.future;
      }
      return u == localB ? health('b') : null;
    };
    final d = discovery();
    final old = d.discover();
    await started.future;
    bindings.active = binding('b');
    ConnectionContext.invalidate();
    d.reset();
    probe = (u, _) async => u == localB ? health('b') : null;
    expect((await d.discover()).baseUrl, localB);
    pending.complete(health('a'));
    await old;
    expect(d.current.schoolId, 'b');
    expect(cache, {'b': localB});
  });
  test('registered private endpoint is accepted across routed LAN subnets',
      () async {
    cache['b'] = localB;
    final r = await discovery(prefixes: const ['10.10.10']).discover();
    expect(r.mode, DiscoveryMode.local);
    expect(r.baseUrl, localB);
  });
  test('missing binding fails closed without network access', () async {
    bindings.active = null;
    expect((await discovery().discover()).mode, DiscoveryMode.offline);
    expect(calls, isEmpty);
  });
  test('all endpoints unavailable gives offline mode', () async {
    probe = (_, __) async => null;
    expect((await discovery().discover()).mode, DiscoveryMode.offline);
  });
  test('saved IPs stay partitioned by school', () async {
    cache['a'] = localA;
    cache['b'] = localB;
    expect((await discovery().discover()).baseUrl, localB);
    expect(calls.first, localB);
    expect(cache['a'], localA);
  });
  test('cancelled school context stops retries', () async {
    var active = true;
    var count = 0;
    final p = EndpointStabilityPolicy(delay: (_) async {
      active = false;
    });
    expect(
        await p.checkLocal<String>(
            established: true,
            isCurrent: () => active,
            probe: (_) async {
              count++;
              return null;
            }),
        isNull);
    expect(count, 1);
  });
  test('all writes must finish before endpoint change; release is idempotent',
      () async {
    final a = ConnectionActivity();
    final release1 = a.beginWrite();
    final release2 = a.beginWrite();
    var done = false;
    final idle = a.waitForIdle().then((_) => done = true);
    release1();
    release1();
    await Future<void>.delayed(Duration.zero);
    expect(done, isFalse);
    release2();
    await idle;
    expect(done, isTrue);
  });
}
