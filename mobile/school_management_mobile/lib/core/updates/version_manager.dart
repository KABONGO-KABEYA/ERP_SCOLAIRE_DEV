/// Comparaison sémantique de versions (1.0.9 < 1.0.10 < 1.1.0).
class VersionManager {
  static List<int> parse(String? value) {
    if (value == null || value.trim().isEmpty) {
      return [0, 0, 0, 0];
    }
    var cleaned = value.trim();
    final plus = cleaned.indexOf('+');
    if (plus >= 0) cleaned = cleaned.substring(0, plus);
    final dash = cleaned.indexOf('-');
    if (dash >= 0) cleaned = cleaned.substring(0, dash);
    final parts = cleaned.split('.');
    final numbers = List<int>.filled(4, 0);
    for (var i = 0; i < parts.length && i < 4; i++) {
      numbers[i] = int.tryParse(parts[i]) ?? 0;
    }
    return numbers;
  }

  static int compare(String? left, String? right) {
    final a = parse(left);
    final b = parse(right);
    for (var i = 0; i < 4; i++) {
      if (a[i] != b[i]) return a[i].compareTo(b[i]);
    }
    return 0;
  }

  static bool isNewer(String? candidate, String? current) =>
      compare(candidate, current) > 0;

  static bool isOlderThan(String? current, String? minimum) =>
      compare(current, minimum) < 0;
}
