class AuthUser {
  const AuthUser({
    required this.id,
    required this.schoolId,
    required this.userName,
    required this.email,
    required this.fullName,
    required this.roles,
    required this.permissions,
  });

  final String id;
  final String schoolId;
  final String userName;
  final String email;
  final String fullName;
  final List<String> roles;
  final List<String> permissions;

  factory AuthUser.fromJson(Map<String, dynamic> json) => AuthUser(
        id: json['id'] as String,
        schoolId: json['schoolId'] as String,
        userName: json['userName'] as String,
        email: json['email'] as String,
        fullName: json['fullName'] as String,
        roles: (json['roles'] as List<dynamic>).map((e) => e.toString()).toList(),
        permissions:
            (json['permissions'] as List<dynamic>).map((e) => e.toString()).toList(),
      );
}

class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.refreshToken,
    required this.user,
  });

  final String accessToken;
  final String refreshToken;
  final AuthUser user;

  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
        accessToken: json['accessToken'] as String,
        refreshToken: json['refreshToken'] as String,
        user: AuthUser.fromJson(json['user'] as Map<String, dynamic>),
      );
}
