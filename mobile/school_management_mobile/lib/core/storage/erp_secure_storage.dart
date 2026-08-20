import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Stockage sécurisé partagé — options Android anti-ANR (TECNO / Transsion).
///
/// `encryptedSharedPreferences: true` provoque parfois un blocage Keystore
/// au premier lancement → splash ANR avant `runApp`.
abstract final class ErpSecureStorage {
  static const instance = FlutterSecureStorage(
    aOptions: AndroidOptions(
      encryptedSharedPreferences: false,
      resetOnError: true,
    ),
  );
}
