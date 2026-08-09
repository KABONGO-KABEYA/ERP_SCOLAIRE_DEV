# Rapport de tests d'isolation multi-école

Généré le 2026-08-07 10:13 par `CrossTenantIsolationTests.Cross_tenant_access_is_denied_on_every_business_resource`.

## Environnement

Deux écoles complètes sont créées en base SQL au début du test, puis supprimées à la fin.
Chaque école possède son propre utilisateur authentifié par JWT (revendication `school_id`)
et un marqueur unique présent dans tous ses libellés : sa présence dans une réponse
destinée à l'autre école constituerait une fuite.

- École A : `MTAA6F221`
- École B : `MTBA6F221`

## Résultats

| Indicateur | Valeur |
|---|---|
| Scénarios exécutés | 108 |
| Scénarios réussis (accès refusé) | 108 |
| Échecs (fuite de données) | 0 |
| Non concluants | 0 |
| Ressources couvertes | 16 |
| Endpoints couverts | 54 |

## Codes de refus observés

| Code HTTP | Nombre de scénarios |
|---|---|
| 200 OK | 34 |
| 404 NotFound | 74 |

## Ressources testées

- **Cartes élèves** — 10/10 scénarios refusés
- **Classes et salles** — 4/4 scénarios refusés
- **Cours** — 8/8 scénarios refusés
- **Délibérations** — 6/6 scénarios refusés
- **Documents élève** — 4/4 scénarios refusés
- **Élèves** — 10/10 scénarios refusés
- **Enseignants** — 4/4 scénarios refusés
- **Frais scolaires** — 14/14 scénarios refusés
- **Inscriptions** — 4/4 scénarios refusés
- **Mentions** — 6/6 scénarios refusés
- **Modèles de carte** — 8/8 scénarios refusés
- **Notes** — 10/10 scénarios refusés
- **Paiements** — 8/8 scénarios refusés
- **Personnel** — 6/6 scénarios refusés
- **Sections** — 2/2 scénarios refusés
- **Utilisateurs** — 4/4 scénarios refusés

## Endpoints testés

- `DELETE api/v1/academic/courses/{courseId}`
- `DELETE api/v1/card-templates/{cardTemplateId}`
- `DELETE api/v1/cards/{studentCardId}`
- `DELETE api/v1/documents/{studentDocumentId}`
- `DELETE api/v1/grades/evaluations/{evaluationId}`
- `DELETE api/v1/mentions/{mentionId}`
- `DELETE api/v1/school-fees/fee-types/{feeTypeId}`
- `DELETE api/v1/school-fees/pricing-categories/{pricingCategoryId}`
- `GET (liste) api/v1/academic/classrooms`
- `GET (liste) api/v1/academic/courses`
- `GET (liste) api/v1/academic/sections`
- `GET (liste) api/v1/admin/teachers`
- `GET (liste) api/v1/admin/users`
- `GET (liste) api/v1/card-templates`
- `GET (liste) api/v1/cards?pageSize=200`
- `GET (liste) api/v1/mentions`
- `GET (liste) api/v1/payments?pageSize=200`
- `GET (liste) api/v1/personnel`
- `GET (liste) api/v1/school-fees/fee-types`
- `GET (liste) api/v1/school-fees/installments`
- `GET (liste) api/v1/school-fees/pricing-categories`
- `GET (liste) api/v1/students?includeAll=true&pageSize=200`
- `GET api/v1/academic/classrooms?academicYearId={academicYearId}`
- `GET api/v1/academic/courses?classRoomId={classRoomId}`
- `GET api/v1/academic/enrollments?classRoomId={classRoomId}`
- `GET api/v1/card-templates/{cardTemplateId}`
- `GET api/v1/cards/{studentCardId}`
- `GET api/v1/deliberation/decision?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&studentId={studentId}`
- `GET api/v1/deliberation/minutes?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}`
- `GET api/v1/deliberation/sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}`
- `GET api/v1/documents/{studentDocumentId}/download`
- `GET api/v1/grades/evaluations/{evaluationId}/entries`
- `GET api/v1/grades/evaluations?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}`
- `GET api/v1/payments/{paymentId}`
- `GET api/v1/personnel/{teacherId}`
- `GET api/v1/students/{studentId}`
- `GET api/v1/students/{studentId}/profile`
- `POST api/v1/academic/enrollments`
- `POST api/v1/cards/{studentCardId}/reprint`
- `POST api/v1/grades/entries`
- `POST api/v1/payments/{paymentId}/cancel`
- `POST api/v1/students/{studentId}/withdraw-current-year`
- `PUT api/v1/academic/courses/{courseId}`
- `PUT api/v1/admin/teachers/{teacherId}`
- `PUT api/v1/admin/users/{userId}`
- `PUT api/v1/card-templates/{cardTemplateId}`
- `PUT api/v1/cards/{studentCardId}`
- `PUT api/v1/grades/evaluations/{evaluationId}`
- `PUT api/v1/mentions/{mentionId}`
- `PUT api/v1/payments/{paymentId}/notes`
- `PUT api/v1/personnel/{teacherId}`
- `PUT api/v1/school-fees/fee-types/{feeTypeId}`
- `PUT api/v1/school-fees/pricing-categories/{pricingCategoryId}`
- `PUT api/v1/students/{studentId}`

## Détail complet

| Ressource | Méthode | Endpoint | Sens | Code | Verdict |
|---|---|---|---|---|---|
| Élèves | GET | `api/v1/students/{studentId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Élèves | GET | `api/v1/students/{studentId}/profile` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Élèves | PUT | `api/v1/students/{studentId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Élèves | POST | `api/v1/students/{studentId}/withdraw-current-year` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Documents élève | GET | `api/v1/documents/{studentDocumentId}/download` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Documents élève | DELETE | `api/v1/documents/{studentDocumentId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Inscriptions | GET | `api/v1/academic/enrollments?classRoomId={classRoomId}` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Inscriptions | POST | `api/v1/academic/enrollments` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Classes et salles | GET | `api/v1/academic/classrooms?academicYearId={academicYearId}` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Cours | GET | `api/v1/academic/courses?classRoomId={classRoomId}` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Cours | PUT | `api/v1/academic/courses/{courseId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Cours | DELETE | `api/v1/academic/courses/{courseId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Notes | GET | `api/v1/grades/evaluations?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Notes | GET | `api/v1/grades/evaluations/{evaluationId}/entries` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Notes | PUT | `api/v1/grades/evaluations/{evaluationId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Notes | DELETE | `api/v1/grades/evaluations/{evaluationId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Notes | POST | `api/v1/grades/entries` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Paiements | GET | `api/v1/payments/{paymentId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Paiements | PUT | `api/v1/payments/{paymentId}/notes` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Paiements | POST | `api/v1/payments/{paymentId}/cancel` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Frais scolaires | PUT | `api/v1/school-fees/fee-types/{feeTypeId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Frais scolaires | DELETE | `api/v1/school-fees/fee-types/{feeTypeId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Frais scolaires | PUT | `api/v1/school-fees/pricing-categories/{pricingCategoryId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Frais scolaires | DELETE | `api/v1/school-fees/pricing-categories/{pricingCategoryId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Cartes élèves | GET | `api/v1/cards/{studentCardId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Cartes élèves | PUT | `api/v1/cards/{studentCardId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Cartes élèves | DELETE | `api/v1/cards/{studentCardId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Cartes élèves | POST | `api/v1/cards/{studentCardId}/reprint` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Modèles de carte | GET | `api/v1/card-templates/{cardTemplateId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Modèles de carte | PUT | `api/v1/card-templates/{cardTemplateId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Modèles de carte | DELETE | `api/v1/card-templates/{cardTemplateId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Utilisateurs | PUT | `api/v1/admin/users/{userId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Enseignants | PUT | `api/v1/admin/teachers/{teacherId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Personnel | GET | `api/v1/personnel/{teacherId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Personnel | PUT | `api/v1/personnel/{teacherId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Délibérations | GET | `api/v1/deliberation/sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Délibérations | GET | `api/v1/deliberation/minutes?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Délibérations | GET | `api/v1/deliberation/decision?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&studentId={studentId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Mentions | PUT | `api/v1/mentions/{mentionId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Mentions | DELETE | `api/v1/mentions/{mentionId}` | MTAA6F221 → MTBA6F221 | 404 | REFUSÉ |
| Élèves | GET | `api/v1/students/{studentId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Élèves | GET | `api/v1/students/{studentId}/profile` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Élèves | PUT | `api/v1/students/{studentId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Élèves | POST | `api/v1/students/{studentId}/withdraw-current-year` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Documents élève | GET | `api/v1/documents/{studentDocumentId}/download` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Documents élève | DELETE | `api/v1/documents/{studentDocumentId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Inscriptions | GET | `api/v1/academic/enrollments?classRoomId={classRoomId}` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Inscriptions | POST | `api/v1/academic/enrollments` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Classes et salles | GET | `api/v1/academic/classrooms?academicYearId={academicYearId}` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Cours | GET | `api/v1/academic/courses?classRoomId={classRoomId}` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Cours | PUT | `api/v1/academic/courses/{courseId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Cours | DELETE | `api/v1/academic/courses/{courseId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Notes | GET | `api/v1/grades/evaluations?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Notes | GET | `api/v1/grades/evaluations/{evaluationId}/entries` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Notes | PUT | `api/v1/grades/evaluations/{evaluationId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Notes | DELETE | `api/v1/grades/evaluations/{evaluationId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Notes | POST | `api/v1/grades/entries` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Paiements | GET | `api/v1/payments/{paymentId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Paiements | PUT | `api/v1/payments/{paymentId}/notes` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Paiements | POST | `api/v1/payments/{paymentId}/cancel` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Frais scolaires | PUT | `api/v1/school-fees/fee-types/{feeTypeId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Frais scolaires | DELETE | `api/v1/school-fees/fee-types/{feeTypeId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Frais scolaires | PUT | `api/v1/school-fees/pricing-categories/{pricingCategoryId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Frais scolaires | DELETE | `api/v1/school-fees/pricing-categories/{pricingCategoryId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Cartes élèves | GET | `api/v1/cards/{studentCardId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Cartes élèves | PUT | `api/v1/cards/{studentCardId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Cartes élèves | DELETE | `api/v1/cards/{studentCardId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Cartes élèves | POST | `api/v1/cards/{studentCardId}/reprint` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Modèles de carte | GET | `api/v1/card-templates/{cardTemplateId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Modèles de carte | PUT | `api/v1/card-templates/{cardTemplateId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Modèles de carte | DELETE | `api/v1/card-templates/{cardTemplateId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Utilisateurs | PUT | `api/v1/admin/users/{userId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Enseignants | PUT | `api/v1/admin/teachers/{teacherId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Personnel | GET | `api/v1/personnel/{teacherId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Personnel | PUT | `api/v1/personnel/{teacherId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Délibérations | GET | `api/v1/deliberation/sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Délibérations | GET | `api/v1/deliberation/minutes?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Délibérations | GET | `api/v1/deliberation/decision?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&studentId={studentId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Mentions | PUT | `api/v1/mentions/{mentionId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Mentions | DELETE | `api/v1/mentions/{mentionId}` | MTBA6F221 → MTAA6F221 | 404 | REFUSÉ |
| Élèves | GET (liste) | `api/v1/students?includeAll=true&pageSize=200` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Classes et salles | GET (liste) | `api/v1/academic/classrooms` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Cours | GET (liste) | `api/v1/academic/courses` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Sections | GET (liste) | `api/v1/academic/sections` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Paiements | GET (liste) | `api/v1/payments?pageSize=200` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Frais scolaires | GET (liste) | `api/v1/school-fees/fee-types` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Frais scolaires | GET (liste) | `api/v1/school-fees/pricing-categories` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Frais scolaires | GET (liste) | `api/v1/school-fees/installments` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Cartes élèves | GET (liste) | `api/v1/cards?pageSize=200` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Modèles de carte | GET (liste) | `api/v1/card-templates` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Utilisateurs | GET (liste) | `api/v1/admin/users` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Enseignants | GET (liste) | `api/v1/admin/teachers` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Personnel | GET (liste) | `api/v1/personnel` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Mentions | GET (liste) | `api/v1/mentions` | MTAA6F221 → MTBA6F221 | 200 | REFUSÉ |
| Élèves | GET (liste) | `api/v1/students?includeAll=true&pageSize=200` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Classes et salles | GET (liste) | `api/v1/academic/classrooms` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Cours | GET (liste) | `api/v1/academic/courses` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Sections | GET (liste) | `api/v1/academic/sections` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Paiements | GET (liste) | `api/v1/payments?pageSize=200` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Frais scolaires | GET (liste) | `api/v1/school-fees/fee-types` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Frais scolaires | GET (liste) | `api/v1/school-fees/pricing-categories` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Frais scolaires | GET (liste) | `api/v1/school-fees/installments` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Cartes élèves | GET (liste) | `api/v1/cards?pageSize=200` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Modèles de carte | GET (liste) | `api/v1/card-templates` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Utilisateurs | GET (liste) | `api/v1/admin/users` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Enseignants | GET (liste) | `api/v1/admin/teachers` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Personnel | GET (liste) | `api/v1/personnel` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
| Mentions | GET (liste) | `api/v1/mentions` | MTBA6F221 → MTAA6F221 | 200 | REFUSÉ |
