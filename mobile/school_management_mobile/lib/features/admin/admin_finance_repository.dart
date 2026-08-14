import '../../core/api/api_client.dart';

import 'admin_finance_models.dart';



class AdminFinanceRepository {

  AdminFinanceRepository(this._api);



  final ApiClient _api;



  Future<RealizedReceiptsReport> getRealizedReceipts({

    required String fromDate,

    required String toDate,

    String? academicYearId,

    String? feeTypeId,

    String? classRoomId,

    String? sectionId,

    int pageSize = 2000,

  }) {

    final parts = <String>[

      'fromDate=$fromDate',

      'toDate=$toDate',

      'page=1',

      'pageSize=$pageSize',

    ];

    if (academicYearId != null && academicYearId.isNotEmpty) {

      parts.add('academicYearId=$academicYearId');

    }

    if (feeTypeId != null && feeTypeId.isNotEmpty) {

      parts.add('feeTypeId=$feeTypeId');

    }

    if (classRoomId != null && classRoomId.isNotEmpty) {

      parts.add('classRoomId=$classRoomId');

    }

    if (sectionId != null && sectionId.isNotEmpty) {

      parts.add('sectionId=$sectionId');

    }

    return _api.getObject(

      '/api/v1/reports/financial-realized-receipts?${parts.join('&')}',

      RealizedReceiptsReport.fromJson,

    );

  }



  Future<AllocationCashFlowReport> getAllocationCashFlow({

    String? academicYearId,

    required String fromDate,

    required String toDate,

    String? feeTypeId,

    String? classRoomId,

    String? sectionId,

  }) {

    final parts = <String>[

      'fromDate=$fromDate',

      'toDate=$toDate',

    ];

    if (academicYearId != null && academicYearId.isNotEmpty) {

      parts.add('academicYearId=$academicYearId');

    }

    if (feeTypeId != null && feeTypeId.isNotEmpty) {

      parts.add('feeTypeId=$feeTypeId');

    }

    if (classRoomId != null && classRoomId.isNotEmpty) {

      parts.add('classRoomId=$classRoomId');

    }

    if (sectionId != null && sectionId.isNotEmpty) {

      parts.add('sectionId=$sectionId');

    }

    return _api.getObject(

      '/api/v1/revenue-allocation/entries/cash-flow?${parts.join('&')}',

      AllocationCashFlowReport.fromJson,

    );

  }



  Future<WithholdingReport> getWithholdingReport({

    String? academicYearId,

    required String fromDate,

    required String toDate,

    String? feeTypeId,

    String? classRoomId,

    String? sectionId,

  }) {

    final parts = <String>[

      'fromDate=$fromDate',

      'toDate=$toDate',

    ];

    if (academicYearId != null && academicYearId.isNotEmpty) {

      parts.add('academicYearId=$academicYearId');

    }

    if (feeTypeId != null && feeTypeId.isNotEmpty) {

      parts.add('feeTypeId=$feeTypeId');

    }

    if (classRoomId != null && classRoomId.isNotEmpty) {

      parts.add('classRoomId=$classRoomId');

    }

    if (sectionId != null && sectionId.isNotEmpty) {

      parts.add('sectionId=$sectionId');

    }

    return _api.getObject(

      '/api/v1/revenue-allocation/entries/withholdings?${parts.join('&')}',

      WithholdingReport.fromJson,

    );

  }



  Future<PaymentSituationReportResult> getPaymentSituationReport({
    required String academicYearId,
    required String feeTypeId,
    int scopeKind = 0,
    List<String>? feeInstallmentIds,
    int situationFilter = 0,
    int sortBy = 0,
    String? sectionId,
    String? classRoomId,
    String? studyOption,
    String? feePricingCategoryId,
  }) {
    final parts = <String>[
      'academicYearId=$academicYearId',
      'feeTypeId=$feeTypeId',
      'scopeKind=$scopeKind',
      'situationFilter=$situationFilter',
      'sortBy=$sortBy',
    ];
    if (scopeKind == 1 && feeInstallmentIds != null) {
      for (final id in feeInstallmentIds) {
        if (id.isNotEmpty) parts.add('feeInstallmentIds=$id');
      }
    }
    if (sectionId != null && sectionId.isNotEmpty) {
      parts.add('sectionId=$sectionId');
    }
    if (classRoomId != null && classRoomId.isNotEmpty) {
      parts.add('classRoomId=$classRoomId');
    }
    if (studyOption != null && studyOption.isNotEmpty) {
      parts.add('studyOption=${Uri.encodeQueryComponent(studyOption)}');
    }
    if (feePricingCategoryId != null && feePricingCategoryId.isNotEmpty) {
      parts.add('feePricingCategoryId=$feePricingCategoryId');
    }
    return _api.getObject(
      '/api/v1/reports/payment-situations?${parts.join('&')}',
      PaymentSituationReportResult.fromJson,
    );
  }

  Future<List<FeeTypeInstallment>> getFeeTypeInstallments(String feeTypeId) => _api.getList(
        '/api/v1/school-fees/fee-types/$feeTypeId/installments',
        FeeTypeInstallment.fromJson,
      );



  Future<List<PricingCategoryOption>> getPricingCategories() => _api.getList(

        '/api/v1/school-fees/pricing-categories',

        PricingCategoryOption.fromJson,

      );



  Future<StudentPricingAssignmentPage> searchPricingAssignments({

    String? academicYearId,

    String? search,

    int page = 1,

    int pageSize = 50,

  }) {

    final parts = <String>[

      'page=$page',

      'pageSize=$pageSize',

    ];

    if (academicYearId != null && academicYearId.isNotEmpty) {

      parts.add('academicYearId=$academicYearId');

    }

    if (search != null && search.trim().isNotEmpty) {

      parts.add('search=${Uri.encodeQueryComponent(search.trim())}');

    }

    return _api.getObject(

      '/api/v1/finance/pricing-assignments?${parts.join('&')}',

      StudentPricingAssignmentPage.fromJson,

    );

  }



  Future<StudentPricingAssignment> updatePricingAssignment({

    required String enrollmentId,

    required String feePricingCategoryId,

    String? notes,

  }) =>

      _api.putObject(

        '/api/v1/finance/pricing-assignments/$enrollmentId',

        {

          'feePricingCategoryId': feePricingCategoryId,

          if (notes != null && notes.isNotEmpty) 'notes': notes,

        },

        StudentPricingAssignment.fromJson,

      );

}


