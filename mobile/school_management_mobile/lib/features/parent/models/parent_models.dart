class ParentChild {
  const ParentChild({
    required this.studentId,
    required this.registrationNumber,
    required this.fullName,
    this.className,
    this.photoUrl,
    this.schoolName,
  });

  final String studentId;
  final String registrationNumber;
  final String fullName;
  final String? className;
  final String? photoUrl;
  final String? schoolName;

  factory ParentChild.fromJson(Map<String, dynamic> json) => ParentChild(
        studentId: json['studentId']?.toString() ?? '',
        registrationNumber: json['registrationNumber'] as String? ?? '',
        fullName: json['fullName'] as String? ?? '',
        className: json['className'] as String?,
        photoUrl: json['photoUrl'] as String?,
        schoolName: json['schoolName'] as String?,
      );
}

class ParentPayment {
  const ParentPayment({
    required this.id,
    required this.receiptNumber,
    required this.paymentDate,
    required this.totalAmount,
    required this.currency,
    required this.status,
    this.feeTypeLabel,
    this.feeTypeId,
    this.academicYearId,
  });

  final String id;
  final String receiptNumber;
  final DateTime paymentDate;
  final double totalAmount;
  final int currency;
  final int status;
  final String? feeTypeLabel;
  final String? feeTypeId;
  final String? academicYearId;

  factory ParentPayment.fromJson(Map<String, dynamic> json) {
    final currencyRaw = json['currency'];
    final statusRaw = json['status'];
    return ParentPayment(
      id: json['id']?.toString() ?? '',
      receiptNumber: json['receiptNumber'] as String? ?? '',
      paymentDate: DateTime.tryParse(json['paymentDate']?.toString() ?? '') ??
          DateTime.now(),
      totalAmount: _asDouble(json['totalAmount']),
      currency: _asCurrency(currencyRaw),
      status: _asStatus(statusRaw),
      feeTypeLabel: json['feeTypeLabel'] as String? ?? json['feeType'] as String?,
      feeTypeId: json['feeTypeId']?.toString(),
      academicYearId: json['academicYearId']?.toString(),
    );
  }

  String get currencyLabel => currency == 1 ? 'CDF' : 'USD';

  String get feeLabel => feeTypeLabel?.trim().isNotEmpty == true
      ? feeTypeLabel!
      : 'Frais scolaires';

  /// Domain: EnAttente=1, Complet=2, Annule=3, Rembourse=4
  bool get isCompleted => status == 2;

  String get statusLabel {
    return switch (status) {
      1 => 'En attente',
      2 => 'Validé',
      3 => 'Annulé',
      4 => 'Remboursé',
      _ => '—',
    };
  }

  static double _asDouble(dynamic value) {
    if (value is num) return value.toDouble();
    if (value is String) return double.tryParse(value.replaceAll(',', '.')) ?? 0;
    return 0;
  }

  static int _asCurrency(dynamic value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    final text = value?.toString().toUpperCase() ?? '';
    if (text.contains('USD') || text == '2') return 2;
    return 1;
  }

  static int _asStatus(dynamic value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    final text = value?.toString().toLowerCase() ?? '';
    return switch (text) {
      'enattente' || '1' => 1,
      'complet' || '2' => 2,
      'annule' || '3' => 3,
      'rembourse' || '4' => 4,
      _ => int.tryParse(text) ?? 0,
    };
  }
}

class ParentPaymentSummary {
  const ParentPaymentSummary({
    required this.totalDue,
    required this.totalPaid,
    required this.balance,
    required this.currencyLabel,
  });

  final double totalDue;
  final double totalPaid;
  final double balance;
  final String currencyLabel;

  double get progress => totalDue <= 0 ? 0 : (totalPaid / totalDue).clamp(0.0, 1.0);

  factory ParentPaymentSummary.fromPayments(List<ParentPayment> payments) {
    if (payments.isEmpty) {
      return const ParentPaymentSummary(
        totalDue: 0,
        totalPaid: 0,
        balance: 0,
        currencyLabel: 'CDF',
      );
    }
    final completed = payments.where((p) => p.isCompleted).toList();
    final source = completed.isNotEmpty ? completed : payments;
    final currency = source.first.currencyLabel;
    final paid = source.fold<double>(0, (sum, p) => sum + p.totalAmount);
    return ParentPaymentSummary(
      totalDue: paid,
      totalPaid: paid,
      balance: 0,
      currencyLabel: currency,
    );
  }

  factory ParentPaymentSummary.fromJson(Map<String, dynamic> json) {
    final currencyRaw = json['currency'];
    final label = json['currencyLabel'] as String? ??
        ((currencyRaw is int
                ? currencyRaw
                : int.tryParse(currencyRaw?.toString() ?? '')) ==
            2
            ? 'USD'
            : 'CDF');
    final totalDue = ParentPayment._asDouble(
      json['totalDue'] ?? json['totalExpected'],
    );
    final totalPaid = ParentPayment._asDouble(json['totalPaid']);
    final balance = json['balance'] != null
        ? ParentPayment._asDouble(json['balance'])
        : totalDue - totalPaid;
    return ParentPaymentSummary(
      totalDue: totalDue,
      totalPaid: totalPaid,
      balance: balance,
      currencyLabel: label,
    );
  }
}

class ParentFeeInstallmentSituation {
  const ParentFeeInstallmentSituation({
    required this.number,
    required this.installmentName,
    required this.amountExpected,
    required this.amountPaid,
    required this.remaining,
  });

  final int number;
  final String installmentName;
  final double amountExpected;
  final double amountPaid;
  final double remaining;

  factory ParentFeeInstallmentSituation.fromJson(Map<String, dynamic> json) =>
      ParentFeeInstallmentSituation(
        number: _asInt(json['number']),
        installmentName: json['installmentName'] as String? ?? 'Tranche',
        amountExpected: ParentPayment._asDouble(json['amountExpected']),
        amountPaid: ParentPayment._asDouble(json['amountPaid']),
        remaining: ParentPayment._asDouble(json['remaining']),
      );

  static int _asInt(dynamic value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value?.toString() ?? '') ?? 0;
  }
}

class ParentFeeTypeSituation {
  const ParentFeeTypeSituation({
    required this.feeTypeId,
    required this.feeTypeName,
    required this.currency,
    required this.currencyLabel,
    required this.amountExpected,
    required this.amountPaid,
    required this.balance,
    required this.isInOrder,
    this.installments = const [],
  });

  final String feeTypeId;
  final String feeTypeName;
  final int currency;
  final String currencyLabel;
  final double amountExpected;
  final double amountPaid;
  final double balance;
  final bool isInOrder;
  final List<ParentFeeInstallmentSituation> installments;

  double get progress =>
      amountExpected <= 0 ? 0 : (amountPaid / amountExpected).clamp(0.0, 1.0);

  ParentPaymentSummary get asSummary => ParentPaymentSummary(
        totalDue: amountExpected,
        totalPaid: amountPaid,
        balance: balance,
        currencyLabel: currencyLabel,
      );

  factory ParentFeeTypeSituation.fromJson(Map<String, dynamic> json) {
    final currencyRaw = json['currency'];
    final currency = ParentPayment._asCurrency(currencyRaw);
    final label = json['currencyLabel'] as String? ??
        (currency == 2 ? 'USD' : 'CDF');
    return ParentFeeTypeSituation(
      feeTypeId: json['feeTypeId']?.toString() ?? '',
      feeTypeName: json['feeTypeName'] as String? ?? 'Type de frais',
      currency: currency,
      currencyLabel: label,
      amountExpected: ParentPayment._asDouble(json['amountExpected']),
      amountPaid: ParentPayment._asDouble(json['amountPaid']),
      balance: ParentPayment._asDouble(json['balance']),
      isInOrder: json['isInOrder'] as bool? ?? false,
      installments: (json['installments'] as List<dynamic>?)
              ?.map((e) => ParentFeeInstallmentSituation.fromJson(
                    Map<String, dynamic>.from(e as Map),
                  ))
              .toList() ??
          const [],
    );
  }
}

class ParentFeeSituations {
  const ParentFeeSituations({
    required this.academicYearId,
    required this.academicYearLabel,
    required this.currencyLabel,
    required this.totalExpected,
    required this.totalPaid,
    required this.totalBalance,
    this.feeTypes = const [],
  });

  final String academicYearId;
  final String academicYearLabel;
  final String currencyLabel;
  final double totalExpected;
  final double totalPaid;
  final double totalBalance;
  final List<ParentFeeTypeSituation> feeTypes;

  ParentPaymentSummary get overallSummary => ParentPaymentSummary(
        totalDue: totalExpected,
        totalPaid: totalPaid,
        balance: totalBalance,
        currencyLabel: currencyLabel,
      );

  factory ParentFeeSituations.fromJson(Map<String, dynamic> json) =>
      ParentFeeSituations(
        academicYearId: json['academicYearId']?.toString() ?? '',
        academicYearLabel: json['academicYearLabel'] as String? ?? '—',
        currencyLabel: json['currencyLabel'] as String? ?? 'CDF',
        totalExpected: ParentPayment._asDouble(json['totalExpected']),
        totalPaid: ParentPayment._asDouble(json['totalPaid']),
        totalBalance: ParentPayment._asDouble(json['totalBalance']),
        feeTypes: (json['feeTypes'] as List<dynamic>?)
                ?.map((e) => ParentFeeTypeSituation.fromJson(
                      Map<String, dynamic>.from(e as Map),
                    ))
                .toList() ??
            const [],
      );

  static const empty = ParentFeeSituations(
    academicYearId: '',
    academicYearLabel: '—',
    currencyLabel: 'CDF',
    totalExpected: 0,
    totalPaid: 0,
    totalBalance: 0,
  );
}

class ParentBulletin {
  const ParentBulletin({
    required this.academicPeriodId,
    required this.periodName,
    required this.average,
    required this.percentage,
    required this.rank,
    required this.classSize,
    required this.isPublished,
    this.pdfUrl,
    this.mention,
    this.decision,
    this.appreciation,
  });

  final String academicPeriodId;
  final String periodName;
  final double average;
  final double percentage;
  final int rank;
  final int classSize;
  final bool isPublished;
  final String? pdfUrl;
  final String? mention;
  final String? decision;
  final String? appreciation;

  factory ParentBulletin.fromJson(Map<String, dynamic> json) => ParentBulletin(
        academicPeriodId: json['academicPeriodId']?.toString() ?? '',
        periodName: json['periodName'] as String? ?? '—',
        average: (json['average'] as num?)?.toDouble() ?? 0,
        percentage: (json['percentage'] as num?)?.toDouble() ?? 0,
        rank: json['rank'] as int? ?? 0,
        classSize: json['classSize'] as int? ?? 0,
        isPublished: json['isPublished'] as bool? ?? false,
        pdfUrl: json['pdfUrl'] as String?,
        mention: json['mention'] as String?,
        decision: json['decision'] as String?,
        appreciation: json['appreciation'] as String?,
      );
}

class ParentSubscriptionStatus {
  const ParentSubscriptionStatus({
    required this.active,
    this.expiresAt,
  });

  final bool active;
  final DateTime? expiresAt;

  factory ParentSubscriptionStatus.fromJson(Map<String, dynamic> json) =>
      ParentSubscriptionStatus(
        active: json['active'] as bool? ?? false,
        expiresAt: json['expiresAt'] != null
            ? DateTime.tryParse(json['expiresAt'].toString())
            : null,
      );
}

class ParentFeatureFlags {
  const ParentFeatureFlags({
    required this.payments,
    required this.notes,
    required this.bulletins,
    required this.communications,
    required this.notifications,
    required this.attendance,
    this.profile = true,
    this.subscriptionManage = true,
  });

  final bool payments;
  final bool notes;
  final bool bulletins;
  final bool communications;
  final bool notifications;
  final bool attendance;
  final bool profile;
  final bool subscriptionManage;

  static const free = ParentFeatureFlags(
    payments: true,
    notes: false,
    bulletins: false,
    communications: false,
    notifications: false,
    attendance: false,
  );

  static const premium = ParentFeatureFlags(
    payments: true,
    notes: true,
    bulletins: true,
    communications: true,
    notifications: true,
    attendance: true,
  );

  factory ParentFeatureFlags.fromJson(Map<String, dynamic> json) =>
      ParentFeatureFlags(
        payments: json['payments'] as bool? ?? true,
        notes: json['notes'] as bool? ?? false,
        bulletins: json['bulletins'] as bool? ?? false,
        communications: json['communications'] as bool? ?? false,
        notifications: json['notifications'] as bool? ?? false,
        attendance: json['attendance'] as bool? ?? false,
        profile: json['profile'] as bool? ?? true,
        subscriptionManage: json['subscriptionManage'] as bool? ?? true,
      );
}

class ParentSubscription {
  const ParentSubscription({
    required this.isPremium,
    required this.plan,
    required this.subscription,
    required this.features,
    this.expiryDate,
  });

  final bool isPremium;
  final String plan;
  final DateTime? expiryDate;
  final ParentSubscriptionStatus subscription;
  final ParentFeatureFlags features;

  bool get isActive => isPremium || subscription.active;

  static final freeDefault = ParentSubscription(
    isPremium: false,
    plan: 'Free',
    subscription: const ParentSubscriptionStatus(active: false),
    features: ParentFeatureFlags.free,
  );

  factory ParentSubscription.fromJson(Map<String, dynamic> json) {
    final nested = json['subscription'] as Map<String, dynamic>?;
    final featuresJson = json['features'] as Map<String, dynamic>?;
    final isPremium = json['isPremium'] as bool? ??
        nested?['active'] as bool? ??
        false;
    final expiry = json['expiryDate'] != null
        ? DateTime.tryParse(json['expiryDate'].toString())
        : (nested?['expiresAt'] != null
            ? DateTime.tryParse(nested!['expiresAt'].toString())
            : null);

    return ParentSubscription(
      isPremium: isPremium,
      plan: json['plan'] as String? ?? (isPremium ? 'Premium' : 'Free'),
      expiryDate: expiry,
      subscription: nested != null
          ? ParentSubscriptionStatus.fromJson(nested)
          : ParentSubscriptionStatus(active: isPremium, expiresAt: expiry),
      features: featuresJson != null
          ? ParentFeatureFlags.fromJson(featuresJson)
          : (isPremium ? ParentFeatureFlags.premium : ParentFeatureFlags.free),
    );
  }
}

class ParentGradeSubject {
  const ParentGradeSubject({
    required this.name,
    required this.average,
    required this.maxScore,
    this.interrogations = const [],
    this.exams = const [],
    this.works = const [],
  });

  final String name;
  final double average;
  final double maxScore;
  final List<ParentGradeItem> interrogations;
  final List<ParentGradeItem> exams;
  final List<ParentGradeItem> works;

  factory ParentGradeSubject.fromJson(Map<String, dynamic> json) =>
      ParentGradeSubject(
        name: json['name'] as String? ?? 'Matière',
        average: (json['average'] as num?)?.toDouble() ?? 0,
        maxScore: (json['maxScore'] as num?)?.toDouble() ?? 20,
        interrogations: _items(json['interrogations']),
        exams: _items(json['exams']),
        works: _items(json['works']),
      );

  static List<ParentGradeItem> _items(dynamic raw) {
    if (raw is! List) return const [];
    return raw
        .map((e) => ParentGradeItem.fromJson(Map<String, dynamic>.from(e as Map)))
        .toList();
  }
}

class ParentGradeItem {
  const ParentGradeItem({
    required this.label,
    required this.score,
    required this.maxScore,
    this.date,
  });

  final String label;
  final double score;
  final double maxScore;
  final DateTime? date;

  factory ParentGradeItem.fromJson(Map<String, dynamic> json) => ParentGradeItem(
        label: json['label'] as String? ?? json['title'] as String? ?? 'Évaluation',
        score: (json['score'] as num?)?.toDouble() ?? 0,
        maxScore: (json['maxScore'] as num?)?.toDouble() ?? 20,
        date: json['date'] != null ? DateTime.tryParse(json['date'].toString()) : null,
      );
}

class ParentGradesOverview {
  const ParentGradesOverview({
    required this.generalAverage,
    required this.rank,
    required this.classSize,
    required this.evolution,
    required this.subjects,
  });

  final double generalAverage;
  final int rank;
  final int classSize;
  final List<double> evolution;
  final List<ParentGradeSubject> subjects;

  factory ParentGradesOverview.fromJson(Map<String, dynamic> json) =>
      ParentGradesOverview(
        generalAverage: (json['generalAverage'] as num?)?.toDouble() ?? 0,
        rank: json['rank'] as int? ?? 0,
        classSize: json['classSize'] as int? ?? 0,
        evolution: (json['evolution'] as List<dynamic>?)
                ?.map((e) => (e as num).toDouble())
                .toList() ??
            const [],
        subjects: (json['subjects'] as List<dynamic>?)
                ?.map((e) =>
                    ParentGradeSubject.fromJson(Map<String, dynamic>.from(e as Map)))
                .toList() ??
            const [],
      );

  static ParentGradesOverview empty() => const ParentGradesOverview(
        generalAverage: 0,
        rank: 0,
        classSize: 0,
        evolution: [],
        subjects: [],
      );
}

class ParentCommunicationAttachment {
  const ParentCommunicationAttachment({
    required this.name,
    required this.type,
    this.url,
  });

  final String name;
  final String type; // pdf | image | document
  final String? url;

  factory ParentCommunicationAttachment.fromJson(Map<String, dynamic> json) =>
      ParentCommunicationAttachment(
        name: json['name'] as String? ?? 'Pièce jointe',
        type: (json['type'] as String? ?? 'document').toLowerCase(),
        url: json['url'] as String?,
      );

  bool get isPdf => type == 'pdf';
  bool get isImage => type == 'image' || type == 'jpg' || type == 'png' || type == 'jpeg';
}

class ParentCommunicationItem {
  const ParentCommunicationItem({
    required this.id,
    required this.title,
    required this.type,
    required this.date,
    this.body,
    this.isRead = false,
    this.attachments = const [],
  });

  final String id;
  final String title;
  final String type;
  final DateTime date;
  final String? body;
  final bool isRead;
  final List<ParentCommunicationAttachment> attachments;

  factory ParentCommunicationItem.fromJson(Map<String, dynamic> json) =>
      ParentCommunicationItem(
        id: json['id']?.toString() ?? '',
        title: json['title'] as String? ?? 'Communication',
        type: json['type'] as String? ?? 'message',
        date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime.now(),
        body: json['body'] as String?,
        isRead: json['isRead'] as bool? ?? false,
        attachments: (json['attachments'] as List<dynamic>?)
                ?.map((e) => ParentCommunicationAttachment.fromJson(
                      Map<String, dynamic>.from(e as Map),
                    ))
                .toList() ??
            const [],
      );

  ParentCommunicationItem copyWith({bool? isRead}) => ParentCommunicationItem(
        id: id,
        title: title,
        type: type,
        date: date,
        body: body,
        isRead: isRead ?? this.isRead,
        attachments: attachments,
      );
}

class ParentNotificationItem {
  const ParentNotificationItem({
    required this.id,
    required this.title,
    required this.message,
    required this.date,
    this.isRead = false,
  });

  final String id;
  final String title;
  final String message;
  final DateTime date;
  final bool isRead;

  factory ParentNotificationItem.fromJson(Map<String, dynamic> json) =>
      ParentNotificationItem(
        id: json['id']?.toString() ?? '',
        title: json['title'] as String? ?? 'Notification',
        message: json['message'] as String? ?? '',
        date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime.now(),
        isRead: json['isRead'] as bool? ?? false,
      );
}

class ParentAttendanceDay {
  const ParentAttendanceDay({
    required this.date,
    required this.status,
    this.note,
  });

  final DateTime date;
  final String status; // present | absent | late
  final String? note;

  factory ParentAttendanceDay.fromJson(Map<String, dynamic> json) =>
      ParentAttendanceDay(
        date: DateTime.tryParse(json['date']?.toString() ?? '') ?? DateTime.now(),
        status: json['status'] as String? ?? 'present',
        note: json['note'] as String?,
      );
}
