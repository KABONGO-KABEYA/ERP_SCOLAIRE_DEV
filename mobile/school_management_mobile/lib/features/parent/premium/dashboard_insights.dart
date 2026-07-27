import '../models/parent_models.dart';

/// Insights dérivés des données déjà chargées (Dashboard V2).
class ParentGradesInsight {
  const ParentGradesInsight({
    required this.generalAverage,
    required this.rank,
    required this.classSize,
    this.lastSubject,
    this.lastLabel,
    this.lastScore,
    this.lastMaxScore,
  });

  final double generalAverage;
  final int rank;
  final int classSize;
  final String? lastSubject;
  final String? lastLabel;
  final double? lastScore;
  final double? lastMaxScore;

  bool get hasData =>
      generalAverage > 0 ||
      rank > 0 ||
      (lastSubject != null && lastSubject!.isNotEmpty);

  static ParentGradesInsight? fromOverview(ParentGradesOverview overview) {
    if (overview.subjects.isEmpty && overview.generalAverage <= 0 && overview.rank <= 0) {
      return null;
    }

    ParentGradeItem? latest;
    String? latestSubject;
    DateTime? latestDate;

    for (final subject in overview.subjects) {
      final all = [
        ...subject.interrogations,
        ...subject.exams,
        ...subject.works,
      ];
      for (final item in all) {
        final date = item.date ?? DateTime.fromMillisecondsSinceEpoch(0);
        if (latest == null || date.isAfter(latestDate ?? DateTime.fromMillisecondsSinceEpoch(0))) {
          latest = item;
          latestSubject = subject.name;
          latestDate = item.date;
        }
      }
    }

    return ParentGradesInsight(
      generalAverage: overview.generalAverage,
      rank: overview.rank,
      classSize: overview.classSize,
      lastSubject: latestSubject,
      lastLabel: latest?.label,
      lastScore: latest?.score,
      lastMaxScore: latest?.maxScore,
    );
  }
}

class ParentAttendanceInsight {
  const ParentAttendanceInsight({
    required this.presentToday,
    required this.lateThisMonth,
    required this.absentThisMonth,
  });

  final bool presentToday;
  final int lateThisMonth;
  final int absentThisMonth;

  static ParentAttendanceInsight fromDays(List<ParentAttendanceDay> days) {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final monthStart = DateTime(now.year, now.month, 1);

    var presentToday = false;
    var late = 0;
    var absent = 0;

    for (final day in days) {
      final d = DateTime(day.date.year, day.date.month, day.date.day);
      final status = day.status.toLowerCase();
      if (d == today && (status == 'present' || status == 'présent' || status == 'presente')) {
        presentToday = true;
      }
      if (!d.isBefore(monthStart)) {
        if (status == 'late' || status == 'retard') late++;
        if (status == 'absent' || status == 'absence') absent++;
      }
    }

    return ParentAttendanceInsight(
      presentToday: presentToday,
      lateThisMonth: late,
      absentThisMonth: absent,
    );
  }
}

class ParentCommunicationsInsight {
  const ParentCommunicationsInsight({
    required this.unreadCount,
    this.lastTitle,
    this.lastDate,
  });

  final int unreadCount;
  final String? lastTitle;
  final DateTime? lastDate;

  static ParentCommunicationsInsight fromItems(List<ParentCommunicationItem> items) {
    if (items.isEmpty) {
      return const ParentCommunicationsInsight(unreadCount: 0);
    }
    final sorted = [...items]..sort((a, b) => b.date.compareTo(a.date));
    final unread = items.where((i) => !i.isRead).length;
    return ParentCommunicationsInsight(
      unreadCount: unread,
      lastTitle: sorted.first.title,
      lastDate: sorted.first.date,
    );
  }
}

class ParentNextDueInsight {
  const ParentNextDueInsight({
    required this.feeTypeName,
    required this.installmentName,
    required this.amount,
    required this.currencyLabel,
    required this.daysRemaining,
  });

  final String feeTypeName;
  final String installmentName;
  final double amount;
  final String currencyLabel;
  final int daysRemaining;

  static ParentNextDueInsight? fromFeeSituations(ParentFeeSituations situations) {
    for (final fee in situations.feeTypes) {
      for (final line in fee.installments) {
        if (line.remaining > 0) {
          // Sans date d'échéance API : estimation relative à l'ordre des tranches.
          final unpaidIndex = fee.installments
              .where((i) => i.remaining > 0)
              .toList()
              .indexOf(line);
          final days = (unpaidIndex + 1) * 15;
          return ParentNextDueInsight(
            feeTypeName: fee.feeTypeName,
            installmentName: line.installmentName,
            amount: line.remaining,
            currencyLabel: fee.currencyLabel,
            daysRemaining: days,
          );
        }
      }
    }
    return null;
  }
}
