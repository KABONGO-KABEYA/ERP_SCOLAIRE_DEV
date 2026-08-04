import 'package:package_info_plus/package_info_plus.dart';

import '../device/device_identity.dart';
import '../../features/parent/offline/parent_offline_cache.dart';
import 'activation_session.dart';
import 'activation_session_store.dart';
import 'bootstrap_api_client.dart';
import 'school_binding.dart';
import 'school_binding_repository.dart';

/// Flux Token → Session → Binding via Bootstrap uniquement (§4.5).
class SchoolActivationService {
  SchoolActivationService({
    BootstrapApiClient? bootstrap,
    SchoolBindingRepository? bindingRepository,
    ActivationSessionStore? sessionStore,
  })  : _bootstrap = bootstrap ?? BootstrapApiClient(),
        _bindingRepository = bindingRepository ?? SchoolBindingRepository(),
        _sessionStore = sessionStore ?? ActivationSessionStore();

  final BootstrapApiClient _bootstrap;
  final SchoolBindingRepository _bindingRepository;
  final ActivationSessionStore _sessionStore;

  static String? extractTokenFromDeepLink(Uri uri) {
    if (uri.scheme != 'erp-scolaire') return null;
    if (uri.host != 'activate' && uri.path != '/activate') return null;
    final token = uri.queryParameters['token'];
    if (token == null || token.isEmpty) return null;
    return token;
  }

  Future<SchoolBinding> activateWithToken(String activationToken) async {
    final deviceId = await DeviceIdentity.deviceId;
    final package = await PackageInfo.fromPlatform();
    final clientHints = <String, dynamic>{
      'platform': 'flutter',
      'appVersion': package.version,
      'buildNumber': package.buildNumber,
    };

    final session = await _bootstrap.start(
      BootstrapStartRequest(
        token: activationToken,
        deviceId: deviceId,
        clientHints: clientHints,
      ),
    );
    await _sessionStore.persist(session);

    final binding = await _bootstrap.complete(
      BootstrapCompleteRequest(
        activationSessionId: session.activationSessionId,
        deviceId: deviceId,
      ),
    );

    await _bindingRepository.save(binding);
    await _sessionStore.clear();
    await ParentOfflineCache.ensureActivePartition();
    return binding;
  }
}
