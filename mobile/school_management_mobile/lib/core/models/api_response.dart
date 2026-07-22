class ApiResponse<T> {
  const ApiResponse({
    required this.success,
    this.message,
    this.data,
    this.errors,
  });

  final bool success;
  final String? message;
  final T? data;
  final List<String>? errors;

  factory ApiResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? json) fromJsonT,
  ) {
    return ApiResponse(
      success: json['success'] as bool? ?? json['Success'] as bool? ?? false,
      message: json['message'] as String? ?? json['Message'] as String?,
      data: json['data'] == null && json['Data'] == null
          ? null
          : fromJsonT(json['data'] ?? json['Data']),
      errors: (json['errors'] as List<dynamic>? ?? json['Errors'] as List<dynamic>?)
          ?.map((e) => e.toString())
          .toList(),
    );
  }
}
