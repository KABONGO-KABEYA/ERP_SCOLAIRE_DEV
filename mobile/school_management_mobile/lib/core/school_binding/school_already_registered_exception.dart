/// Levée lorsqu'un QR tente d'associer une école déjà présente dans le registre.
class SchoolAlreadyRegisteredException implements Exception {
  SchoolAlreadyRegisteredException(this.schoolId, {this.schoolName});

  final String schoolId;
  final String? schoolName;

  @override
  String toString() {
    final name = schoolName?.trim();
    if (name != null && name.isNotEmpty) {
      return 'L\'établissement « $name » est déjà associé à cette application.';
    }
    return 'Cet établissement est déjà associé à cette application.';
  }
}
