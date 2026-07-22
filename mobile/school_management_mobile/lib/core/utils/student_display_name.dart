/// Format standard : Nom Postnom Prénom (RDC).
String formatStudentDisplayName({
  required String lastName,
  String? middleName,
  required String firstName,
}) {
  final parts = <String>[
    lastName.trim(),
    if (middleName != null && middleName.trim().isNotEmpty) middleName.trim(),
    firstName.trim(),
  ].where((p) => p.isNotEmpty);
  return parts.join(' ');
}
