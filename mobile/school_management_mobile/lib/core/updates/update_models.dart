class UpdateManifest {
  const UpdateManifest({
    required this.latestVersion,
    required this.minimumVersion,
    required this.mandatory,
    required this.releaseNotes,
    this.releaseDate,
    this.desktopUrl,
    this.mobileUrl,
    this.downloadUrl,
    this.sha256,
    this.size,
    this.schemaVersion,
  });

  final String latestVersion;
  final String minimumVersion;
  final bool mandatory;
  final DateTime? releaseDate;
  final List<String> releaseNotes;
  final String? desktopUrl;
  final String? mobileUrl;
  final String? downloadUrl;
  final String? sha256;
  final int? size;
  final int? schemaVersion;

  factory UpdateManifest.fromJson(Map<String, dynamic> json) {
    final notes = json['releaseNotes'];
    return UpdateManifest(
      latestVersion: (json['latestVersion'] ?? '0.0.0').toString(),
      minimumVersion: (json['minimumVersion'] ?? '0.0.0').toString(),
      mandatory: json['mandatory'] == true,
      releaseDate: json['releaseDate'] != null
          ? DateTime.tryParse(json['releaseDate'].toString())
          : null,
      releaseNotes: notes is List
          ? notes.map((e) => e.toString()).toList()
          : const <String>[],
      desktopUrl: json['desktopUrl']?.toString(),
      mobileUrl: json['mobileUrl']?.toString(),
      downloadUrl: json['downloadUrl']?.toString() ?? json['mobileUrl']?.toString(),
      sha256: json['sha256']?.toString(),
      size: json['size'] is int ? json['size'] as int : int.tryParse('${json['size']}'),
      schemaVersion: json['schemaVersion'] is int
          ? json['schemaVersion'] as int
          : int.tryParse('${json['schemaVersion']}'),
    );
  }
}

enum UpdateAvailability { upToDate, optional, mandatory }

class UpdateCheckOutcome {
  const UpdateCheckOutcome({
    required this.availability,
    required this.currentVersion,
    this.manifest,
  });

  final UpdateAvailability availability;
  final String currentVersion;
  final UpdateManifest? manifest;
}
