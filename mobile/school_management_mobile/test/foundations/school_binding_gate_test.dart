import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:school_management_mobile/core/config/binding_migration_config.dart';
import 'package:school_management_mobile/core/school_binding/school_binding.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_gate.dart';
import 'package:school_management_mobile/core/school_binding/school_binding_repository.dart';

class _MemoryBindingRepository extends SchoolBindingRepository {
  _MemoryBindingRepository(SchoolBinding binding) : _binding = binding;

  final SchoolBinding _binding;

  @override
  Future<SchoolBinding?> load() async => _binding;

  @override
  Future<bool> hasBinding() async => _binding.schoolId.isNotEmpty;

  @override
  Future<bool> hasAnyRegisteredSchool() async => _binding.schoolId.isNotEmpty;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('SchoolBindingGate — étape 4', () {
    tearDown(() {
      SchoolBindingGate.bindingRepository = SchoolBindingRepository();
    });

    test('shouldFilterDiscoveryByBinding false when STRICT off', () async {
      expect(BindingMigrationConfig.strictSchoolDiscovery, isFalse);
      SchoolBindingGate.bindingRepository = _MemoryBindingRepository(
        SchoolBinding(
          schoolId: '33333333-3333-3333-3333-333333333333',
          schoolName: 'A',
          cloudBaseUrl: 'https://c.example',
          serverInstanceId: '11111111-1111-1111-1111-111111111111',
          activationDate: DateTime.utc(2026, 1, 1),
          activationTokenId: 't',
          activationSessionId: 's',
          deviceId: 'd',
          protocolVersion: 2,
        ),
      );
      expect(await SchoolBindingGate.shouldFilterDiscoveryByBinding(), isFalse);
    });

    test('shouldRequireEstablishmentQr when registry empty', () async {
      FlutterSecureStorage.setMockInitialValues({});
      SchoolBindingGate.bindingRepository = SchoolBindingRepository();
      expect(await SchoolBindingGate.shouldRequireEstablishmentQr(), isTrue);
      expect(await SchoolBindingGate.shouldRequireActivationQr(), isTrue);
    });
  });
}
