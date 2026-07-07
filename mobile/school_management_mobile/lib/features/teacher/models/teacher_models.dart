class TeacherAssignment {
  const TeacherAssignment({
    required this.id,
    required this.courseId,
    required this.courseName,
    required this.classRoomId,
    required this.classRoomName,
    required this.academicYearId,
    required this.academicYearLabel,
  });

  final String id;
  final String courseId;
  final String courseName;
  final String classRoomId;
  final String classRoomName;
  final String academicYearId;
  final String academicYearLabel;

  factory TeacherAssignment.fromJson(Map<String, dynamic> json) => TeacherAssignment(
        id: json['id'] as String,
        courseId: json['courseId'] as String,
        courseName: json['courseName'] as String,
        classRoomId: json['classRoomId'] as String,
        classRoomName: json['classRoomName'] as String,
        academicYearId: json['academicYearId'] as String,
        academicYearLabel: json['academicYearLabel'] as String,
      );
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
        registrationNumber: json['registrationNumber'] as String,
        fullName: json['fullName'] as String,
      );
}

class TeacherPeriod {
  const TeacherPeriod({
    required this.id,
    required this.name,
    required this.orderIndex,
  });

  final String id;
  final String name;
  final int orderIndex;

  factory TeacherPeriod.fromJson(Map<String, dynamic> json) => TeacherPeriod(
        id: json['id'] as String,
        name: json['name'] as String,
        orderIndex: json['orderIndex'] as int,
      );
}

class TeacherEvaluation {
  const TeacherEvaluation({
    required this.id,
    required this.title,
    required this.courseId,
    required this.courseName,
    required this.classRoomId,
    required this.maxScore,
    required this.isOpen,
    required this.evaluationDate,
  });

  final String id;
  final String title;
  final String courseId;
  final String courseName;
  final String classRoomId;
  final int maxScore;
  final bool isOpen;
  final String evaluationDate;

  factory TeacherEvaluation.fromJson(Map<String, dynamic> json) => TeacherEvaluation(
        id: json['id'] as String,
        title: json['title'] as String,
        courseId: json['courseId'] as String,
        courseName: json['courseName'] as String,
        classRoomId: json['classRoomId'] as String,
        maxScore: json['maxScore'] as int,
        isOpen: json['isOpen'] as bool,
        evaluationDate: json['evaluationDate'] as String,
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
        score: (json['score'] as num).toDouble(),
        isAbsent: json['isAbsent'] as bool,
      );
}
