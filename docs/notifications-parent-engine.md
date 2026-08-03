# Moteur de notifications parent (ERP ↔ Mobile)

## Objectif

Chaque action importante sur un élève crée une notification pour les parents liés (`StudentGuardian` → `UserAccount.GuardianId`), consultable dans le centre mobile, diffusée en temps réel (SignalR **groupe privé**) et affichée dans la barre de notification Android.

## Point d'entrée unique

```csharp
await _notifications.NotifyStudentParentsAsync(
    schoolId,
    studentId,
    NotificationCategory.Payment,
    NotificationEventType.PaymentReceived,
    "💰 Paiement reçu",
    "Nous confirmons la réception de …",
    dataJson: "{\"paymentId\":\"…\"}",
    deepLink: "/parent/payments",
    cancellationToken);
```

Interface : `INotificationService` (`SchoolManagement.Application/Notifications`).

## Pipeline de livraison (évolutif)

```
NotificationService
  → SignalR groupe privé parent-{UserAccountId}   (app ouverte)
  → Foreground Service + GET /changes             (app minimisée)
  → IPushNotificationSender (FCM)                 (app tuée — stub prêt)
```

## Isolation des parents

- À la connexion SignalR, le JWT fournit `UserAccountId`.
- La connexion rejoint uniquement le groupe `parent-{UserAccountId}`.
- Publication : `Clients.Group($"parent-{userId}")` — **jamais** `Clients.All`.
- Un parent multi-enfants reçoit toutes les notifs de ses enfants dans **une** boîte (filtrée par `UserAccountId`).

## Tables

| Table | Rôle |
|-------|------|
| `SchoolNotifications` | Événement (titre, corps, catégorie, élève…) |
| `NotificationRecipients` | Destinataire + IsRead / DeliveredAt / PushSentAt |
| `ParentDeviceTokens` | Tokens FCM par appareil |

Créées au démarrage API via `NotificationSchemaInitializer`.

## Statuts ACK

| Statut | Champ | Déclencheur |
|--------|-------|-------------|
| Sent | création destinataire | NotificationService |
| Delivered | `DeliveredAt` | ACK client (hub `AcknowledgeDelivery` ou `POST …/delivered`) |
| Read | `IsRead` / `ReadAt` | `POST …/read` |

## API parent

- `GET /api/v1/parent/notifications?category=&q=`
- `GET /api/v1/parent/notifications/changes?afterId=&since=&take=` ← delta léger
- `GET /api/v1/parent/notifications/unread-count`
- `POST /api/v1/parent/notifications/{id}/delivered` (ACK)
- `POST /api/v1/parent/notifications/ack`
- `POST /api/v1/parent/notifications/{id}/read`
- `POST /api/v1/parent/notifications/read-all`
- `POST /api/v1/parent/notifications/device-token`
- `DELETE /api/v1/parent/notifications/device-token?token=`
- Hub SignalR : `/hubs/parent-notifications` (événement `notification`)

## Comportement mobile

| État app | Transport |
|----------|-----------|
| Ouverte + SignalR OK | SignalR seul (pas de polling UI) |
| SignalR coupé | Fallback `GET /changes` |
| Minimisée | Foreground Service + `/changes` |
| Tuée | FCM (à brancher via `IPushNotificationSender`) |

Déduplication conservée : id lowercase, contenu proche, seen ids partagés, seed initial, refresh sans re-alerte, canal `erp_parent_alerts_v2`.

## Hooks déjà branchés

- Paiement créé / annulé → `PaymentService`
- Inscription validée → `EnrollmentWizardService.CompleteAsync`
- Cotes soumises → `GradeService.SubmitGradesAsync`

À brancher de la même façon : absences, sanctions, mérites, bulletins, communiqués, devoirs…

## Feature flag

Centre notifications encore derrière Premium (`features.notifications`).
