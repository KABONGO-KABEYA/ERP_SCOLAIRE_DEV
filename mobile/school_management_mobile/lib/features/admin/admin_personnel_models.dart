class PersonnelListItem {
  const PersonnelListItem({
    required this.id,
    required this.employeeNumber,
    required this.fullName,
    required this.categoryLabel,
    this.functionName,
    this.departmentName,
    this.phone,
    this.email,
    required this.seniorityLabel,
    required this.contractLabel,
    required this.statusLabel,
    required this.isActive,
  });

  final String id;
  final String employeeNumber;
  final String fullName;
  final String categoryLabel;
  final String? functionName;
  final String? departmentName;
  final String? phone;
  final String? email;
  final String seniorityLabel;
  final String contractLabel;
  final String statusLabel;
  final bool isActive;

  factory PersonnelListItem.fromJson(Map<String, dynamic> json) => PersonnelListItem(
        id: json['id']?.toString() ?? '',
        employeeNumber: json['employeeNumber'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        categoryLabel: json['categoryLabel'] as String? ?? '',
        functionName: json['functionName'] as String?,
        departmentName: json['departmentName'] as String?,
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        seniorityLabel: json['seniorityLabel'] as String? ?? '',
        contractLabel: json['contractLabel'] as String? ?? '',
        statusLabel: json['statusLabel'] as String? ?? '',
        isActive: json['isActive'] as bool? ?? true,
      );
}
