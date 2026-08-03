class TeacherAssignment {
  const TeacherAssignment({
    required this.id,
    required this.courseId,
    required this.courseName,
    required this.classRoomId,
    required this.classRoomName,
    required this.academicYearId,
    required this.academicYearLabel,
    required this.maxScore,
    required this.studentCount,
  });

  final String id;
  final String courseId;
  final String courseName;
  final String classRoomId;
  final String classRoomName;
  final String academicYearId;
  final String academicYearLabel;
  final int maxScore;
  final int studentCount;

  factory TeacherAssignment.fromJson(Map<String, dynamic> json) => TeacherAssignment(
        id: json['id'] as String,
        courseId: json['courseId'] as String,
        courseName: json['courseName'] as String,
        classRoomId: json['classRoomId'] as String,
        classRoomName: json['classRoomName'] as String,
        academicYearId: json['academicYearId'] as String,
        academicYearLabel: json['academicYearLabel'] as String? ?? '—',
        maxScore: (json['maxScore'] as num?)?.toInt() ?? 20,
        studentCount: (json['studentCount'] as num?)?.toInt() ?? 0,
      );
}

class TeacherClassCard {
  const TeacherClassCard({
    required this.classRoomId,
    required this.classRoomName,
    required this.academicYearId,
    required this.academicYearLabel,
    required this.studentCount,
    required this.courses,
  });

  final String classRoomId;
  final String classRoomName;
  final String academicYearId;
  final String academicYearLabel;
  final int studentCount;
  final List<TeacherAssignment> courses;

  int get courseCount => courses.length;
}

class TeacherStudent {
  const TeacherStudent({
    required this.studentId,
    required this.registrationNumber,
    required this.fullName,
  });

  final String studentId;
  final String registrationNumber;
  final String fullName;

  factory TeacherStudent.fromJson(Map<String, dynamic> json) => TeacherStudent(
        studentId: json['studentId'] as String,
        registrationNumber: json['registrationNumber'] as String? ?? '',
        fullName: json['fullName'] as String,
      );
}

class TeacherPeriod {
  const TeacherPeriod({
    required this.id,
    required this.name,
    required this.orderIndex,
    required this.isClosed,
    required this.kindLabel,
    this.startDate,
    this.endDate,
  });

  final String id;
  final String name;
  final int orderIndex;
  final bool isClosed;
  final String kindLabel;
  final String? startDate;
  final String? endDate;

  bool get isEditable => !isClosed;

  factory TeacherPeriod.fromJson(Map<String, dynamic> json) => TeacherPeriod(
        id: json['id'] as String,
        name: json['name'] as String,
        orderIndex: (json['orderIndex'] as num?)?.toInt() ?? 0,
        isClosed: json['isClosed'] as bool? ?? false,
        kindLabel: json['kindLabel'] as String? ?? '',
        startDate: json['startDate'] as String?,
        endDate: json['endDate'] as String?,
      );
}

class EvaluationTypeOption {
  const EvaluationTypeOption({
    required this.id,
    required this.code,
    required this.name,
  });

  final String id;
  final String code;
  final String name;

  factory EvaluationTypeOption.fromJson(Map<String, dynamic> json) => EvaluationTypeOption(
        id: json['id'] as String,
        code: json['code'] as String? ?? '',
        name: json['name'] as String,
      );
}

class TeacherEvaluation {
  const TeacherEvaluation({
    required this.id,
    required this.title,
    required this.evaluationTypeId,
    required this.evaluationTypeName,
    required this.courseId,
    required this.courseName,
    required this.classRoomId,
    required this.maxScore,
    required this.isOpen,
    required this.evaluationDate,
    required this.gradedCount,
    required this.studentCount,
  });

  final String id;
  final String title;
  final String evaluationTypeId;
  final String evaluationTypeName;
  final String courseId;
  final String courseName;
  final String classRoomId;
  final int maxScore;
  final bool isOpen;
  final String evaluationDate;
  final int gradedCount;
  final int studentCount;

  factory TeacherEvaluation.fromJson(Map<String, dynamic> json) => TeacherEvaluation(
        id: json['id'] as String,
        title: json['title'] as String,
        evaluationTypeId: json['evaluationTypeId'] as String? ?? '',
        evaluationTypeName: json['evaluationTypeName'] as String? ?? '',
        courseId: json['courseId'] as String,
        courseName: json['courseName'] as String? ?? '',
        classRoomId: json['classRoomId'] as String,
        maxScore: (json['maxScore'] as num?)?.toInt() ?? 20,
        isOpen: json['isOpen'] as bool? ?? true,
        evaluationDate: json['evaluationDate']?.toString() ?? '',
        gradedCount: (json['gradedCount'] as num?)?.toInt() ?? 0,
        studentCount: (json['studentCount'] as num?)?.toInt() ?? 0,
      );
}

class GradeEntry {
  const GradeEntry({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.score,
    required this.isAbsent,
  });

  final String? id;
  final String studentId;
  final String studentName;
  final double score;
  final bool isAbsent;

  factory GradeEntry.fromJson(Map<String, dynamic> json) => GradeEntry(
        id: json['id'] as String?,
        studentId: json['studentId'] as String,
        studentName: json['studentName'] as String,
        score: (json['score'] as num?)?.toDouble() ?? 0,
        isAbsent: json['isAbsent'] as bool? ?? false,
      );
}

List<TeacherClassCard> groupAssignmentsByClass(List<TeacherAssignment> assignments) {
  final map = <String, List<TeacherAssignment>>{};
  for (final a in assignments) {
    map.putIfAbsent(a.classRoomId, () => []).add(a);
  }

  return map.entries.map((e) {
    final courses = e.value;
    final first = courses.first;
    return TeacherClassCard(
      classRoomId: first.classRoomId,
      classRoomName: first.classRoomName,
      academicYearId: first.academicYearId,
      academicYearLabel: first.academicYearLabel,
      studentCount: first.studentCount,
      courses: courses,
    );
  }).toList()
    ..sort((a, b) => a.classRoomName.compareTo(b.classRoomName));
}
