# Référence API — v1

Documentation générée depuis les contrôleurs (`scripts/export-api-docs.ps1`).

| | |
|---|---|
| **Base URL locale** | `http://localhost:5041` |
| **Base URL cloud** | `http://169.58.93.203:1804` |
| **Swagger UI** | `{base}/swagger` |
| **OpenAPI JSON** | `{base}/swagger/v1/swagger.json` |
| **Fichier Swagger complet** | [`docs/api/swagger.v1.json`](api/swagger.v1.json) (~839 Ko, 237 chemins, 417 schémas) |
| **Auth** | `Authorization: Bearer {token}` |
| **Endpoints** | 300 |

## Authentification

1. `POST /api/v1/auth/login` avec body JSON `userName` / `password`
2. Utiliser `data.accessToken` dans l’en-tête Bearer
3. Endpoints publics : login, refresh, health

## Mode Cloud (ReadOnly)

Sur l’API Cloud, les écritures (POST/PUT/PATCH/DELETE) sont refusées (**403**), sauf :
- `/api/v1/auth/*`
- `/api/v1/health`
- `/api/v1/grades/entries`

## Catalogue des endpoints

### Academic

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/academic/classrooms` | GetClassRooms | JWT | - |
| POST | `/api/v1/academic/classrooms` | CreateClassRoom | JWT | - |
| GET | `/api/v1/academic/courses` | GetCourses | JWT | - |
| POST | `/api/v1/academic/courses` | CreateCourse | JWT | - |
| DELETE | `/api/v1/academic/courses/{courseId:guid}` | DeleteCourse | JWT | - |
| PUT | `/api/v1/academic/courses/{courseId:guid}` | UpdateCourse | JWT | - |
| GET | `/api/v1/academic/enrollments` | GetEnrollments | JWT | - |
| POST | `/api/v1/academic/enrollments` | CreateEnrollment | JWT | - |
| GET | `/api/v1/academic/sections` | GetSections | JWT | - |

### Accounting

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/accounting/expense-balances` | GetExpenseBalances | JWT | - |
| GET | `/api/v1/accounting/expense-payments` | SearchExpensePayments | JWT | - |
| POST | `/api/v1/accounting/expense-payments` | CreateExpensePayment | JWT | - |
| GET | `/api/v1/accounting/expense-payments/{id:guid}` | GetExpensePayment | JWT | - |
| GET | `/api/v1/accounting/expense-requests` | SearchExpenseRequests | JWT | - |
| POST | `/api/v1/accounting/expense-requests` | CreateExpenseRequest | JWT | - |
| POST | `/api/v1/accounting/expense-requests/{id:guid}/approve` | ApproveExpenseRequest | JWT | - |
| POST | `/api/v1/accounting/expense-requests/{id:guid}/submit` | SubmitExpenseRequest | JWT | - |

### Admin

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| POST | `/api/v1/admin/reset-enrollment-data` | ResetEnrollmentData | JWT | - |
| GET | `/api/v1/admin/roles` | GetRoles | JWT | - |
| GET | `/api/v1/admin/teachers` | GetTeachers | JWT | - |
| POST | `/api/v1/admin/teachers` | CreateTeacher | JWT | - |
| PUT | `/api/v1/admin/teachers/{id:guid}` | UpdateTeacher | JWT | - |
| GET | `/api/v1/admin/users` | GetUsers | JWT | - |
| POST | `/api/v1/admin/users` | CreateUser | JWT | - |
| PUT | `/api/v1/admin/users/{id:guid}` | UpdateUser | JWT | - |
| PUT | `/api/v1/admin/users/{id:guid}/roles` | SetUserRoles | JWT | - |

### Auth

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| POST | `/api/v1/auth/change-password` | ChangePassword | JWT | - |
| POST | `/api/v1/auth/login` | Login | JWT | - |
| POST | `/api/v1/auth/logout` | Logout | Anonymous | - |
| GET | `/api/v1/auth/me` | GetProfile | JWT | - |
| POST | `/api/v1/auth/refresh` | Refresh | Anonymous | - |

### CardTemplates

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/card-templates` | List | JWT | - |
| POST | `/api/v1/card-templates` | Create | JWT | - |
| DELETE | `/api/v1/card-templates/{id:guid}` | Delete | JWT | - |
| GET | `/api/v1/card-templates/{id:guid}` | Get | JWT | - |
| PUT | `/api/v1/card-templates/{id:guid}` | Update | JWT | - |
| POST | `/api/v1/card-templates/preview` | Preview | JWT | - |

### CloudSync

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/cloud-sync/status` | GetStatus | JWT | - |
| POST | `/api/v1/cloud-sync/synchronize` | SynchronizeNow | JWT | - |

### CourseConfiguration

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/course-configuration` | GetConfiguration | JWT | - |
| PUT | `/api/v1/course-configuration` | SaveConfiguration | JWT | - |
| GET | `/api/v1/course-configuration/available-courses` | GetAvailableCourses | JWT | - |
| GET | `/api/v1/course-configuration/branches` | GetBranches | JWT | - |
| POST | `/api/v1/course-configuration/courses` | CreateCatalogCourse | JWT | - |

### Currencies

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/currencies` | Search | JWT | - |
| POST | `/api/v1/currencies` | Create | JWT | - |
| PUT | `/api/v1/currencies/{id:guid}` | Update | JWT | - |
| POST | `/api/v1/currencies/{id:guid}/activate` | Activate | JWT | - |
| POST | `/api/v1/currencies/{id:guid}/deactivate` | Deactivate | JWT | - |
| GET | `/api/v1/currencies/main` | GetMain | JWT | - |

### Dashboard

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/dashboard/activities` | GetActivities | JWT | - |
| GET | `/api/v1/dashboard/alerts` | GetAlerts | JWT | - |
| GET | `/api/v1/dashboard/debtors` | GetDebtors | JWT | - |
| GET | `/api/v1/dashboard/distribution` | GetDistribution | JWT | - |
| GET | `/api/v1/dashboard/enrolled-students` | GetEnrolledStudents | JWT | - |
| GET | `/api/v1/dashboard/expenses` | GetExpenses | JWT | - |
| GET | `/api/v1/dashboard/fund-movements` | GetFundMovements | JWT | - |
| GET | `/api/v1/dashboard/overview` | GetOverview | JWT | - |
| GET | `/api/v1/dashboard/payments` | GetPayments | JWT | - |
| GET | `/api/v1/dashboard/receivables-breakdown` | GetReceivablesBreakdown | JWT | - |
| GET | `/api/v1/dashboard/repartition` | GetRepartition | JWT | - |
| GET | `/api/v1/dashboard/revenue` | GetRevenue | JWT | - |
| GET | `/api/v1/dashboard/summary` | GetSummary | JWT | - |

### DocumentBranding

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/document-branding/configuration` | GetConfiguration | JWT | - |
| PUT | `/api/v1/document-branding/footer` | SaveFooter | JWT | - |
| POST | `/api/v1/document-branding/headers` | CreateHeader | JWT | - |
| DELETE | `/api/v1/document-branding/headers/{headerId:guid}` | DeleteHeader | JWT | - |
| PUT | `/api/v1/document-branding/headers/{headerId:guid}` | UpdateHeader | JWT | - |
| POST | `/api/v1/document-branding/logos` | CreateLogo | JWT | - |
| DELETE | `/api/v1/document-branding/logos/{logoId:guid}` | DeleteLogo | JWT | - |
| PUT | `/api/v1/document-branding/logos/{logoId:guid}` | UpdateLogo | JWT | - |
| POST | `/api/v1/document-branding/logos/{logoId:guid}/set-primary` | SetPrimaryLogo | JWT | - |
| GET | `/api/v1/document-branding/logos/primary/file` | GetPrimaryLogoFile | JWT | - |
| GET | `/api/v1/document-branding/lookups` | GetLookups | JWT | - |
| GET | `/api/v1/document-branding/print/{documentType}` | ResolvePrintBranding | JWT | - |
| POST | `/api/v1/document-branding/signatures` | CreateSignature | JWT | - |
| DELETE | `/api/v1/document-branding/signatures/{signatureId:guid}` | DeleteSignature | JWT | - |
| PUT | `/api/v1/document-branding/signatures/{signatureId:guid}` | UpdateSignature | JWT | - |
| POST | `/api/v1/document-branding/stamps` | CreateStamp | JWT | - |
| DELETE | `/api/v1/document-branding/stamps/{stampId:guid}` | DeleteStamp | JWT | - |
| PUT | `/api/v1/document-branding/stamps/{stampId:guid}` | UpdateStamp | JWT | - |

### Documents

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/documents` | List | JWT | - |
| POST | `/api/v1/documents` | Upload | JWT | - |
| DELETE | `/api/v1/documents/{id:guid}` | Delete | JWT | - |
| GET | `/api/v1/documents/{id:guid}/download` | Download | JWT | - |

### EnrollmentWizard

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/enrollment-wizard/class-capacity` | GetClassCapacity | JWT | - |
| POST | `/api/v1/enrollment-wizard/complete` | Complete | JWT | - |
| GET | `/api/v1/enrollment-wizard/fees` | CalculateFees | JWT | - |
| GET | `/api/v1/enrollment-wizard/fiche-inscription/{enrollmentId:guid}` | GetEnrollmentForm | JWT | - |
| GET | `/api/v1/enrollment-wizard/prerequisites` | GetPrerequisites | JWT | - |
| GET | `/api/v1/enrollment-wizard/registration-number` | GenerateRegistrationNumber | JWT | - |
| GET | `/api/v1/enrollment-wizard/search-guardians` | SearchGuardians | JWT | - |
| GET | `/api/v1/enrollment-wizard/search-students` | SearchStudents | JWT | - |
| POST | `/api/v1/enrollment-wizard/store-file` | StoreEnrollmentFile | JWT | - |
| GET | `/api/v1/enrollment-wizard/structure-options` | GetStructureOptions | JWT | - |
| PUT | `/api/v1/enrollment-wizard/student-dossier/{enrollmentId:guid}` | UpdateStudentDossier | JWT | - |
| POST | `/api/v1/enrollment-wizard/student-dossier/{enrollmentId:guid}/validate` | ValidateStudentDossierUpdate | JWT | - |
| GET | `/api/v1/enrollment-wizard/student-dossier/{studentId:guid}` | GetStudentDossierForEdit | JWT | - |
| POST | `/api/v1/enrollment-wizard/validate` | Validate | JWT | - |

### ExchangeRates

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/exchange-rates` | Search | JWT | - |
| POST | `/api/v1/exchange-rates` | Create | JWT | - |
| GET | `/api/v1/exchange-rates/{id:guid}` | GetById | JWT | - |
| PUT | `/api/v1/exchange-rates/{id:guid}` | Update | JWT | - |
| POST | `/api/v1/exchange-rates/{id:guid}/activate` | Activate | JWT | - |
| POST | `/api/v1/exchange-rates/{id:guid}/deactivate` | Deactivate | JWT | - |
| GET | `/api/v1/exchange-rates/active` | GetActive | JWT | - |
| POST | `/api/v1/exchange-rates/convert` | Convert | JWT | - |
| GET | `/api/v1/exchange-rates/for-date` | GetForDate | JWT | - |
| GET | `/api/v1/exchange-rates/history` | History | JWT | - |

### ExchangeRateTypes

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/exchange-rate-types` | List | JWT | - |
| POST | `/api/v1/exchange-rate-types` | Create | JWT | - |
| PUT | `/api/v1/exchange-rate-types/{id:guid}` | Update | JWT | - |
| POST | `/api/v1/exchange-rate-types/{id:guid}/activate` | Activate | JWT | - |
| POST | `/api/v1/exchange-rate-types/{id:guid}/deactivate` | Deactivate | JWT | - |

### Finance

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/finance/payment-situations` | SearchPaymentSituations | JWT | - |
| GET | `/api/v1/finance/payment-situations/{enrollmentId:guid}/installment-plan` | GetInstallmentPaymentPlan | JWT | - |
| GET | `/api/v1/finance/pricing-assignments` | SearchPricingAssignments | JWT | - |
| PUT | `/api/v1/finance/pricing-assignments/{enrollmentId:guid}` | UpdatePricingAssignment | JWT | - |
| GET | `/api/v1/finance/pricing-assignments/{enrollmentId:guid}/applicable-fees` | GetApplicableFees | JWT | - |
| GET | `/api/v1/finance/pricing-assignments/{enrollmentId:guid}/history` | GetPricingAssignmentHistory | JWT | - |

### Geography

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/geography/addresses/{id:guid}` | GetAddress | JWT | - |
| GET | `/api/v1/geography/cities` | GetCities | JWT | - |
| GET | `/api/v1/geography/communes` | GetCommunes | JWT | - |
| GET | `/api/v1/geography/countries` | GetCountries | JWT | - |
| GET | `/api/v1/geography/provinces` | GetProvinces | JWT | - |

### GeographyAdmin

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/geography/admin/cities` | GetCities | JWT | - |
| POST | `/api/v1/geography/admin/cities` | CreateCity | JWT | - |
| DELETE | `/api/v1/geography/admin/cities/{id:guid}` | DeactivateCity | JWT | - |
| PUT | `/api/v1/geography/admin/cities/{id:guid}` | UpdateCity | JWT | - |
| GET | `/api/v1/geography/admin/communes` | GetCommunes | JWT | - |
| POST | `/api/v1/geography/admin/communes` | CreateCommune | JWT | - |
| DELETE | `/api/v1/geography/admin/communes/{id:guid}` | DeactivateCommune | JWT | - |
| PUT | `/api/v1/geography/admin/communes/{id:guid}` | UpdateCommune | JWT | - |
| GET | `/api/v1/geography/admin/countries` | GetCountries | JWT | - |
| POST | `/api/v1/geography/admin/countries` | CreateCountry | JWT | - |
| DELETE | `/api/v1/geography/admin/countries/{id:guid}` | DeactivateCountry | JWT | - |
| PUT | `/api/v1/geography/admin/countries/{id:guid}` | UpdateCountry | JWT | - |
| POST | `/api/v1/geography/admin/import` | ImportExcel | JWT | - |
| GET | `/api/v1/geography/admin/import/template` | DownloadImportTemplate | JWT | - |
| GET | `/api/v1/geography/admin/provinces` | GetProvinces | JWT | - |
| POST | `/api/v1/geography/admin/provinces` | CreateProvince | JWT | - |
| DELETE | `/api/v1/geography/admin/provinces/{id:guid}` | DeactivateProvince | JWT | - |
| PUT | `/api/v1/geography/admin/provinces/{id:guid}` | UpdateProvince | JWT | - |

### Grades

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| POST | `/api/v1/grades/entries` | SubmitGrades | JWT | - |
| GET | `/api/v1/grades/evaluations` | GetEvaluations | JWT | - |
| POST | `/api/v1/grades/evaluations` | CreateEvaluation | JWT | - |
| GET | `/api/v1/grades/evaluations/{evaluationId:guid}/entries` | GetGradeEntries | JWT | - |
| GET | `/api/v1/grades/evaluation-types` | GetEvaluationTypes | JWT | - |
| GET | `/api/v1/grades/period-results` | GetPeriodResults | JWT | - |
| POST | `/api/v1/grades/period-results/calculate` | CalculatePeriodResults | JWT | - |

### MobileSubscription

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/mobile/subscription` | GetSubscription | JWT | - |

### MobileSubscriptionPayment

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| POST | `/api/v1/mobile/subscription/payment/callback` | Callback | JWT | - |
| POST | `/api/v1/mobile/subscription/payment/initiate` | Initiate | JWT | - |
| POST | `/api/v1/mobile/subscription/payment/status` | Status | JWT | - |
| GET | `/api/v1/mobile/subscription/payments` | History | JWT | - |
| GET | `/api/v1/mobile/subscription/payments/{paymentId:guid}/invoice/pdf` | InvoicePdf | JWT | - |

### Parent

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/parent/children` | GetChildren | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/attendance` | GetChildAttendance | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/bulletins` | GetChildBulletins | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/bulletins/{academicPeriodId:guid}/pdf` | ExportChildBulletinPdf | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/communications` | GetChildCommunications | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/fee-situations` | GetChildFeeSituations | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/grades` | GetChildGrades | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/payments` | GetChildPayments | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/payment-summary` | GetChildPaymentSummary | JWT | - |
| GET | `/api/v1/parent/children/{studentId:guid}/photo` | GetChildPhoto | JWT | - |
| GET | `/api/v1/parent/payments/{paymentId:guid}/receipt/pdf` | ExportPaymentReceiptPdf | JWT | - |

### Payments

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/payments` | Search | JWT | - |
| POST | `/api/v1/payments` | Create | JWT | - |
| GET | `/api/v1/payments/{id:guid}` | GetById | JWT | - |
| PUT | `/api/v1/payments/{id:guid}/amount` | UpdateAmount | JWT | - |
| POST | `/api/v1/payments/{id:guid}/cancel` | Cancel | JWT | - |
| GET | `/api/v1/payments/{id:guid}/fee-type-statement` | GetFeeTypeStatement | JWT | - |
| GET | `/api/v1/payments/{id:guid}/fee-type-statement/pdf` | ExportFeeTypeStatementPdf | JWT | - |
| PUT | `/api/v1/payments/{id:guid}/notes` | UpdateNotes | JWT | - |
| GET | `/api/v1/payments/fee-type-statement` | GetFeeTypeStatementForStudent | JWT | - |
| GET | `/api/v1/payments/fee-type-statement/pdf` | ExportFeeTypeStatementPdfForStudent | JWT | - |
| GET | `/api/v1/payments/mutation-gate` | GetMutationGate | JWT | - |
| GET | `/api/v1/payments/student/{studentId:guid}/summary` | GetStudentSummary | JWT | - |

### PedagogicalStructure

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/schools/current/pedagogical-structure/classes` | GetClasses | JWT | - |
| PUT | `/api/v1/schools/current/pedagogical-structure/classes` | BulkUpdateClasses | JWT | - |
| PUT | `/api/v1/schools/current/pedagogical-structure/classes/{classId:guid}` | UpdateClass | JWT | - |
| GET | `/api/v1/schools/current/pedagogical-structure/classes/{classId:guid}/locals` | GetLocals | JWT | - |
| POST | `/api/v1/schools/current/pedagogical-structure/initialize` | Initialize | JWT | - |
| POST | `/api/v1/schools/current/pedagogical-structure/locals` | CreateLocal | JWT | - |
| DELETE | `/api/v1/schools/current/pedagogical-structure/locals/{localId:guid}` | DeleteLocal | JWT | - |
| PUT | `/api/v1/schools/current/pedagogical-structure/locals/{localId:guid}` | UpdateLocal | JWT | - |
| GET | `/api/v1/schools/current/pedagogical-structure/summary` | GetSummary | JWT | - |

### Personnel

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/personnel` | GetPersonnel | JWT | - |
| POST | `/api/v1/personnel` | CreatePersonnel | JWT | - |
| GET | `/api/v1/personnel/{id:guid}` | GetPersonnelById | JWT | - |
| PUT | `/api/v1/personnel/{id:guid}` | UpdatePersonnel | JWT | - |
| GET | `/api/v1/personnel/departments` | GetDepartments | JWT | - |
| POST | `/api/v1/personnel/departments` | CreateDepartment | JWT | - |
| GET | `/api/v1/personnel/functions` | GetJobFunctions | JWT | - |
| POST | `/api/v1/personnel/functions` | CreateJobFunction | JWT | - |
| GET | `/api/v1/personnel/kpis` | GetKpis | JWT | - |

### Reports

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/reports/class-averages` | GetClassAverages | JWT | - |
| GET | `/api/v1/reports/dashboard` | GetDashboard | JWT | - |
| GET | `/api/v1/reports/enrollment-by-class` | GetEnrollmentByClass | JWT | - |
| GET | `/api/v1/reports/financial-realized-receipts` | GetRealizedReceipts | JWT | - |
| GET | `/api/v1/reports/financial-realized-receipts/export/excel` | ExportRealizedReceiptsExcel | JWT | - |
| GET | `/api/v1/reports/financial-realized-receipts/export/pdf` | ExportRealizedReceiptsPdf | JWT | - |
| GET | `/api/v1/reports/financial-summary` | GetFinancialSummary | JWT | - |
| GET | `/api/v1/reports/payment-situations` | GetPaymentSituationReport | JWT | - |
| GET | `/api/v1/reports/payment-situations/export/excel` | ExportPaymentSituationReportExcel | JWT | - |
| GET | `/api/v1/reports/payment-situations/export/pdf` | ExportPaymentSituationReportPdf | JWT | - |

### RevenueAllocation

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/revenue-allocation/destinations` | GetDestinations | JWT | - |
| POST | `/api/v1/revenue-allocation/destinations` | CreateDestination | JWT | - |
| PUT | `/api/v1/revenue-allocation/destinations/{id:guid}` | UpdateDestination | JWT | - |
| POST | `/api/v1/revenue-allocation/destinations/{id:guid}/deactivate` | DeactivateDestination | JWT | - |
| GET | `/api/v1/revenue-allocation/entries` | SearchEntries | JWT | - |
| GET | `/api/v1/revenue-allocation/entries/cash-flow` | GetCashFlow | JWT | - |
| GET | `/api/v1/revenue-allocation/entries/export/excel` | ExportExcel | JWT | - |
| GET | `/api/v1/revenue-allocation/entries/export/pdf` | ExportPdf | JWT | - |
| GET | `/api/v1/revenue-allocation/entries/summary-by-fee-type` | GetSummaryByFeeType | JWT | - |
| GET | `/api/v1/revenue-allocation/entries/withholdings` | GetWithholdings | JWT | - |
| GET | `/api/v1/revenue-allocation/keys` | GetKeys | JWT | - |
| POST | `/api/v1/revenue-allocation/keys` | CreateKey | JWT | - |
| DELETE | `/api/v1/revenue-allocation/keys/{id:guid}` | DeleteKey | JWT | - |
| GET | `/api/v1/revenue-allocation/keys/{id:guid}` | GetKey | JWT | - |
| PUT | `/api/v1/revenue-allocation/keys/{id:guid}` | UpdateKey | JWT | - |
| POST | `/api/v1/revenue-allocation/keys/{id:guid}/activate` | ActivateKey | JWT | - |
| POST | `/api/v1/revenue-allocation/keys/{id:guid}/close` | CloseKey | JWT | - |
| POST | `/api/v1/revenue-allocation/keys/{id:guid}/deactivate` | DeactivateKey | JWT | - |

### SchoolCurrencies

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/school-currencies` | List | JWT | - |
| POST | `/api/v1/school-currencies` | Upsert | JWT | - |
| DELETE | `/api/v1/school-currencies/{id:guid}` | Remove | JWT | - |

### SchoolFee

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/school-fees/catalog` | GetCatalog | JWT | - |
| GET | `/api/v1/school-fees/fee-types` | GetFeeTypes | JWT | - |
| POST | `/api/v1/school-fees/fee-types` | CreateFeeType | JWT | - |
| GET | `/api/v1/school-fees/fee-types/{feeTypeId:guid}/installments` | GetFeeTypeInstallments | JWT | - |
| PUT | `/api/v1/school-fees/fee-types/{feeTypeId:guid}/installments` | SaveFeeTypeInstallments | JWT | - |
| DELETE | `/api/v1/school-fees/fee-types/{id:guid}` | DeleteFeeType | JWT | - |
| PUT | `/api/v1/school-fees/fee-types/{id:guid}` | UpdateFeeType | JWT | - |
| GET | `/api/v1/school-fees/installments` | GetInstallments | JWT | - |
| POST | `/api/v1/school-fees/installments` | CreateInstallment | JWT | - |
| DELETE | `/api/v1/school-fees/installments/{id:guid}` | DeleteInstallment | JWT | - |
| PUT | `/api/v1/school-fees/installments/{id:guid}` | UpdateInstallment | JWT | - |
| GET | `/api/v1/school-fees/pricing-categories` | GetPricingCategories | JWT | - |
| POST | `/api/v1/school-fees/pricing-categories` | CreatePricingCategory | JWT | - |
| DELETE | `/api/v1/school-fees/pricing-categories/{id:guid}` | DeletePricingCategory | JWT | - |
| PUT | `/api/v1/school-fees/pricing-categories/{id:guid}` | UpdatePricingCategory | JWT | - |
| GET | `/api/v1/school-fees/schedule` | GetSchedule | JWT | - |
| PUT | `/api/v1/school-fees/schedule` | SaveSchedule | JWT | - |
| PUT | `/api/v1/school-fees/schedule/bulk` | SaveScheduleBulk | JWT | - |
| POST | `/api/v1/school-fees/schedule/copy-from-previous` | CopyScheduleFromPrevious | JWT | - |
| POST | `/api/v1/school-fees/schedule/copy-from-previous/bulk` | CopyScheduleFromPreviousBulk | JWT | - |
| GET | `/api/v1/school-fees/schedule/signatures` | GetScheduleSignatures | JWT | - |

### Schools

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/schools/current` | GetCurrent | JWT | - |
| PUT | `/api/v1/schools/current` | UpdateCurrent | JWT | - |
| GET | `/api/v1/schools/current/academic-years` | GetAcademicYears | JWT | - |
| POST | `/api/v1/schools/current/academic-years` | CreateAcademicYear | JWT | - |
| PUT | `/api/v1/schools/current/academic-years/{yearId:guid}/set-current` | SetCurrentYear | JWT | - |
| GET | `/api/v1/schools/current/lookups` | GetLookups | JWT | - |
| GET | `/api/v1/schools/current/regulation` | GetRegulation | JWT | - |
| PUT | `/api/v1/schools/current/regulation` | UpdateRegulation | JWT | - |

### StudentCards

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/cards` | Search | JWT | - |
| POST | `/api/v1/cards` | Create | JWT | - |
| DELETE | `/api/v1/cards/{id:guid}` | Delete | JWT | - |
| GET | `/api/v1/cards/{id:guid}` | GetById | JWT | - |
| PUT | `/api/v1/cards/{id:guid}` | Update | JWT | - |
| POST | `/api/v1/cards/{id:guid}/deactivate` | Deactivate | JWT | - |
| POST | `/api/v1/cards/{id:guid}/lost` | DeclareLost | JWT | - |
| POST | `/api/v1/cards/{id:guid}/renew` | Renew | JWT | - |
| POST | `/api/v1/cards/{id:guid}/reprint` | Reprint | JWT | - |
| POST | `/api/v1/cards/{id:guid}/stolen` | DeclareStolen | JWT | - |
| POST | `/api/v1/cards/bulk` | BulkCreate | JWT | - |
| GET | `/api/v1/cards/dashboard` | Dashboard | JWT | - |
| POST | `/api/v1/cards/print` | Print | JWT | - |
| POST | `/api/v1/cards/resolve-qr` | ResolveQr | JWT | - |
| GET | `/api/v1/cards/settings` | GetSettings | JWT | - |
| PUT | `/api/v1/cards/settings` | SaveSettings | JWT | - |

### Students

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/students` | Search | JWT | - |
| POST | `/api/v1/students` | Create | JWT | - |
| DELETE | `/api/v1/students/{id:guid}` | Archive | JWT | - |
| GET | `/api/v1/students/{id:guid}` | GetById | JWT | - |
| PUT | `/api/v1/students/{id:guid}` | Update | JWT | - |
| GET | `/api/v1/students/{id:guid}/dossier-files` | ListDossierFiles | JWT | - |
| POST | `/api/v1/students/{id:guid}/exclude-current-year` | ExcludeFromCurrentYear | JWT | - |
| GET | `/api/v1/students/{id:guid}/profile` | GetProfile | JWT | - |
| POST | `/api/v1/students/{id:guid}/withdraw-current-year` | WithdrawFromCurrentYear | JWT | - |
| GET | `/api/v1/students/withdrawal-reasons` | GetWithdrawalReasons | JWT | - |

### Teacher

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| GET | `/api/v1/teacher/assignments` | GetAssignments | JWT | - |
| GET | `/api/v1/teacher/classes/{classRoomId:guid}/students` | GetClassStudents | JWT | - |
| GET | `/api/v1/teacher/periods` | GetPeriods | JWT | - |

### Withholdings

| Methode | Route | Action | Auth | Permission |
|---------|-------|--------|------|------------|
| POST | `/api/v1/withholdings/calculate` | Calculate | JWT | - |
| GET | `/api/v1/withholdings/configurations` | SearchConfigurations | JWT | - |
| POST | `/api/v1/withholdings/configurations` | CreateConfiguration | JWT | - |
| DELETE | `/api/v1/withholdings/configurations/{id:guid}` | DeleteConfiguration | JWT | - |
| GET | `/api/v1/withholdings/configurations/{id:guid}` | GetConfiguration | JWT | - |
| PUT | `/api/v1/withholdings/configurations/{id:guid}` | UpdateConfiguration | JWT | - |
| POST | `/api/v1/withholdings/configurations/{id:guid}/deactivate` | DeactivateConfiguration | JWT | - |
| GET | `/api/v1/withholdings/configurations/export/excel` | ExportExcel | JWT | - |
| GET | `/api/v1/withholdings/configurations/export/pdf` | ExportPdf | JWT | - |
| POST | `/api/v1/withholdings/resolve` | Resolve | JWT | - |
| GET | `/api/v1/withholdings/types` | GetTypes | JWT | - |
| POST | `/api/v1/withholdings/types` | CreateType | JWT | - |
| PUT | `/api/v1/withholdings/types/{id:guid}` | UpdateType | JWT | - |
| POST | `/api/v1/withholdings/types/{id:guid}/deactivate` | DeactivateType | JWT | - |

