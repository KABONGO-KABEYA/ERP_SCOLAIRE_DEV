import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:open_filex/open_filex.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:path_provider/path_provider.dart';
import '../cache/school_scoped_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

import '../config/api_config.dart';
import 'update_models.dart';
import 'version_manager.dart';

/// Orchestrateur mobile (miroir du module .NET SchoolManagement.Updates).
class UpdateManager {
  UpdateManager({Dio? dio}) : _dio = dio ?? Dio();

  final Dio _dio;
  static const _prefsAutoCheck = 'updates.autoCheck';
  static const _prefsSnoozeUntil = 'updates.snoozeUntil';
  static const _prefsLastCheck = 'updates.lastCheck';
  static const _allowedHosts = {
    'localhost',
    '127.0.0.1',
    '169.58.93.203',
  };

  Future<UpdateCheckOutcome?> checkSilently({
    String? baseUrl,
    bool ignoreSnooze = false,
  }) async {
    if (!(await SchoolScopedPreferences.getBool(_prefsAutoCheck) ?? true) &&
        !ignoreSnooze) {
      return null;
    }

    if (!ignoreSnooze) {
      final snooze = await SchoolScopedPreferences.getString(_prefsSnoozeUntil);
      if (snooze != null) {
        final until = DateTime.tryParse(snooze);
        if (until != null && until.isAfter(DateTime.now().toUtc())) {
          return null;
        }
      }
    }

    try {
      final info = await PackageInfo.fromPlatform();
      final current = info.version;
      final root = (baseUrl ??
              ApiConfig.effectiveCloudBaseUrl ??
              ApiConfig.effectiveLocalBaseUrl)
          .replaceAll(RegExp(r'/+$'), '');

      final response = await _dio.get<dynamic>(
        '$root/api/v1/update/check',
        queryParameters: {
          'platform': 'mobile',
          'currentVersion': current,
        },
        options: Options(
          receiveTimeout: const Duration(seconds: 8),
          sendTimeout: const Duration(seconds: 8),
          validateStatus: (s) => s != null && s < 500,
        ),
      );

      await SchoolScopedPreferences.setString(
        _prefsLastCheck,
        DateTime.now().toUtc().toIso8601String(),
      );

      if (response.statusCode == 204 || response.data == null) {
        return null;
      }

      final raw = response.data;
      Map<String, dynamic> payload;
      if (raw is Map<String, dynamic>) {
        payload = (raw['data'] is Map<String, dynamic>)
            ? Map<String, dynamic>.from(raw['data'] as Map)
            : Map<String, dynamic>.from(raw);
      } else {
        return null;
      }

      final manifest = UpdateManifest.fromJson(payload);
      final belowMin = VersionManager.isOlderThan(current, manifest.minimumVersion);
      final newer = VersionManager.isNewer(manifest.latestVersion, current);
      if (!newer && !belowMin) {
        return null;
      }

      final mandatory = manifest.mandatory || belowMin;
      return UpdateCheckOutcome(
        availability:
            mandatory ? UpdateAvailability.mandatory : UpdateAvailability.optional,
        currentVersion: current,
        manifest: manifest,
      );
    } catch (e, st) {
      debugPrint('Update check silent fail: $e\n$st');
      return null;
    }
  }

  Future<void> snooze(Duration duration) async {
    await SchoolScopedPreferences.setString(
      _prefsSnoozeUntil,
      DateTime.now().toUtc().add(duration).toIso8601String(),
    );
  }

  bool isUrlAllowed(String url) {
    final uri = Uri.tryParse(url);
    if (uri == null || !uri.hasScheme || uri.host.isEmpty) return false;
    if (!_allowedHosts.contains(uri.host.toLowerCase())) return false;
    return uri.scheme == 'https' || uri.scheme == 'http';
  }

  Future<File> downloadAndVerify(
    UpdateManifest manifest, {
    void Function(int received, int? total)? onProgress,
    CancelToken? cancelToken,
  }) async {
    final url = manifest.downloadUrl ?? manifest.mobileUrl;
    if (url == null || !isUrlAllowed(url)) {
      throw StateError('URL de téléchargement non autorisée.');
    }

    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/SuperEcole-${manifest.latestVersion}.apk');
    await _dio.download(
      url,
      file.path,
      cancelToken: cancelToken,
      onReceiveProgress: (r, t) => onProgress?.call(r, t > 0 ? t : null),
    );

    final digest = sha256.convert(await file.readAsBytes());
    final actual = digest.toString();
    final expected = (manifest.sha256 ?? '').replaceAll('-', '').toLowerCase();
    if (expected.isEmpty || actual != expected) {
      await file.delete();
      throw StateError('Le fichier téléchargé est invalide.');
    }
    return file;
  }

  /// Installe l'APK (sideload). Pour Play Store : ouvrir le listing.
  Future<void> installApk(File apk) async {
    await OpenFilex.open(apk.path);
  }

  Future<void> openPlayStoreListing({String packageId = 'com.example.school_management_mobile'}) async {
    final uri = Uri.parse('market://details?id=$packageId');
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
      return;
    }
    final web = Uri.parse('https://play.google.com/store/apps/details?id=$packageId');
    await launchUrl(web, mode: LaunchMode.externalApplication);
  }
}
