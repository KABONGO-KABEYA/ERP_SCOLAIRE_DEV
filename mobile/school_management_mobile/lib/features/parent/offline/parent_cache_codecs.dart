import '../models/parent_models.dart';

/// Codecs JSON pour le cache Hive (évite de modifier ParentRepository).
abstract final class ParentCacheCodecs {
  static Map<String, dynamic> childToJson(ParentChild c) => {
        'studentId': c.studentId,
        'registrationNumber': c.registrationNumber,
        'fullName': c.fullName,
        'className': c.className,
        'photoUrl': c.photoUrl,
        'schoolName': c.schoolName,
      };

  static Map<String, dynamic> paymentToJson(ParentPayment p) => {
        'id': p.id,
        'receiptNumber': p.receiptNumber,
        'paymentDate': p.paymentDate.toIso8601String(),
        'totalAmount': p.totalAmount,
        'currency': p.currency,
        'status': p.status,
        'feeTypeLabel': p.feeTypeLabel,
        'feeTypeId': p.feeTypeId,
        'academicYearId': p.academicYearId,
      };

  static Map<String, dynamic> paymentSummaryToJson(ParentPaymentSummary s) => {
        'totalDue': s.totalDue,
        'totalPaid': s.totalPaid,
        'balance': s.balance,
        'currencyLabel': s.currencyLabel,
      };

  static Map<String, dynamic> feeSituationsToJson(ParentFeeSituations s) => {
        'academicYearId': s.academicYearId,
        'academicYearLabel': s.academicYearLabel,
        'currencyLabel': s.currencyLabel,
        'totalExpected': s.totalExpected,
        'totalPaid': s.totalPaid,
        'totalBalance': s.totalBalance,
        'feeTypes': s.feeTypes.map(_feeTypeToJson).toList(),
      };

  static Map<String, dynamic> _feeTypeToJson(ParentFeeTypeSituation f) => {
        'feeTypeId': f.feeTypeId,
        'feeTypeName': f.feeTypeName,
        'currency': f.currency,
        'currencyLabel': f.currencyLabel,
        'amountExpected': f.amountExpected,
        'amountPaid': f.amountPaid,
        'balance': f.balance,
        'isInOrder': f.isInOrder,
        'installments': f.installments
            .map(
              (i) => {
                'number': i.number,
                'installmentName': i.installmentName,
                'amountExpected': i.amountExpected,
                'amountPaid': i.amountPaid,
                'remaining': i.remaining,
              },
            )
            .toList(),
      };

  static Map<String, dynamic> bulletinToJson(ParentBulletin b) => {
        'academicPeriodId': b.academicPeriodId,
        'periodName': b.periodName,
        'average': b.average,
        'percentage': b.percentage,
        'rank': b.rank,
        'classSize': b.classSize,
        'isPublished': b.isPublished,
        'pdfUrl': b.pdfUrl,
        'mention': b.mention,
        'decision': b.decision,
        'appreciation': b.appreciation,
      };

  static Map<String, dynamic> gradesToJson(ParentGradesOverview g) => {
        'generalAverage': g.generalAverage,
        'rank': g.rank,
        'classSize': g.classSize,
        'evolution': g.evolution,
        'subjects': g.subjects.map(_subjectToJson).toList(),
      };

  static Map<String, dynamic> _subjectToJson(ParentGradeSubject s) => {
        'name': s.name,
        'average': s.average,
        'maxScore': s.maxScore,
        'interrogations': s.interrogations.map(_gradeItemToJson).toList(),
        'exams': s.exams.map(_gradeItemToJson).toList(),
        'works': s.works.map(_gradeItemToJson).toList(),
      };

  static Map<String, dynamic> _gradeItemToJson(ParentGradeItem i) => {
        'label': i.label,
        'score': i.score,
        'maxScore': i.maxScore,
        'date': i.date?.toIso8601String(),
      };

  static Map<String, dynamic> attendanceToJson(ParentAttendanceDay d) => {
        'date': d.date.toIso8601String(),
        'status': d.status,
        'note': d.note,
      };

  static Map<String, dynamic> communicationToJson(ParentCommunicationItem c) => {
        'id': c.id,
        'title': c.title,
        'type': c.type,
        'date': c.date.toIso8601String(),
        'body': c.body,
        'isRead': c.isRead,
        'attachments': c.attachments
            .map(
              (a) => {
                'name': a.name,
                'type': a.type,
                'url': a.url,
              },
            )
            .toList(),
      };

  static Map<String, dynamic> subscriptionToJson(ParentSubscription s) => {
        'isPremium': s.isPremium,
        'plan': s.plan,
        'expiryDate': s.expiryDate?.toIso8601String(),
        'subscription': {
          'active': s.subscription.active,
          'expiresAt': s.subscription.expiresAt?.toIso8601String(),
        },
        'features': {
          'payments': s.features.payments,
          'notes': s.features.notes,
          'bulletins': s.features.bulletins,
          'communications': s.features.communications,
          'notifications': s.features.notifications,
          'attendance': s.features.attendance,
          'profile': s.features.profile,
          'subscriptionManage': s.features.subscriptionManage,
        },
      };
}
