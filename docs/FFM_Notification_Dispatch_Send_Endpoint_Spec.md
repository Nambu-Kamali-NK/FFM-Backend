# FFM Notification Service — Send Notification Endpoint Specification

## 1. Document Overview
* **Document Reference:** `FFM-SPEC-NOTIF-SEND-V1.0`
* **Service:** `FFM.Notification` (Port 5040)
* **Module:** Module 3: Notification Dispatch Engine
* **Method & Route:** `POST /api/v1/Notifications/send`
* **Architectural Standard:** Strictly Zero Path Parameters. All identifiers and payload elements in JSON Request Body.
* **Authentication:** Bearer JWT (RS256) / Development Bypass Mode.

---

## 2. Business Scope & Purpose
The `POST /api/v1/Notifications/send` endpoint orchestrates the end-to-end delivery of notifications (e.g. task assignments, geofence breaches, check-in reminders, cancellations) across the FFM platform:
1. Validates recipient employee existence and loads their active device registration.
2. Resolves the requested notification template (`(TemplateCode, Channel)` compound key).
3. Interpolates dynamic placeholders (e.g. `{{TaskNumber}}`, `{{SiteName}}`, `{{EmployeeName}}`).
4. Dispatches the notification over the appropriate delivery provider (Google FCM for `PUSH`).
5. Persists an immutable delivery audit log in the PostgreSQL `NotificationLogs` table.

---

## 3. API Contract

### Request: `POST /api/v1/Notifications/send`
**Headers:**
* `Content-Type: application/json`
* `Authorization: Bearer <TOKEN>` *(Optional in DevBypass)*

**Request Body (`SendNotificationRequest`):**
```json
{
  "recipientEmployeeId": 1,
  "templateCode": "TASK_ASSIGNED",
  "channel": "PUSH",
  "placeholders": {
    "TaskNumber": "TSK-9901",
    "EmployeeName": "Kamali",
    "SiteName": "Apollo Hospital, RS Puram",
    "Priority": "URGENT"
  },
  "priority": "HIGH"
}
```

### Success Response (`HTTP 200 OK` - Delivered):
```json
{
  "success": true,
  "message": "Notification processed successfully.",
  "data": {
    "notificationLogId": 1,
    "recipientEmployeeId": 1,
    "templateCode": "TASK_ASSIGNED",
    "channel": "PUSH",
    "renderedTitle": "Urgent Assignment: TSK-9901",
    "renderedBody": "Hello Kamali, please immediately review task TSK-9901 at Apollo Hospital, RS Puram.",
    "status": "DELIVERED",
    "fcmMessageId": "projects/ffm-notification-api/messages/2e4481e5-a85d-4d49-807d-096b691bdec4",
    "errorMessage": null,
    "dispatchedAt": "2026-11-13T07:10:11.1138306Z"
  },
  "error": null,
  "timestamp": "2026-11-13T07:10:11.3077732Z"
}
```

### Response (`HTTP 200 OK` - No Active Device):
When recipient employee is registered in the organization but has logged out or not registered a device:
```json
{
  "success": true,
  "message": "Notification processed successfully.",
  "data": {
    "notificationLogId": 2,
    "recipientEmployeeId": 502,
    "templateCode": "TASK_ASSIGNED",
    "channel": "PUSH",
    "renderedTitle": "New Task Assigned: TSK-9902",
    "renderedBody": "Hi Ramesh, new task #TSK-9902 at Site A has been assigned to you.",
    "status": "NO_ACTIVE_DEVICE",
    "fcmMessageId": null,
    "errorMessage": "Recipient employee has no active registered device.",
    "dispatchedAt": "2026-11-13T07:12:00.0000000Z"
  },
  "error": null,
  "timestamp": "2026-11-13T07:12:00.1000000Z"
}
```

---

## 4. Database Schema: `NotificationLogs` Table
```sql
CREATE TABLE IF NOT EXISTS "NotificationLogs" (
    "Id" SERIAL PRIMARY KEY,
    "RecipientEmployeeId" INT NOT NULL,
    "TemplateCode" VARCHAR(50) NOT NULL,
    "Channel" VARCHAR(20) NOT NULL,
    "Title" VARCHAR(250) NULL,
    "Body" TEXT NOT NULL,
    "FcmMessageId" VARCHAR(250) NULL,
    "Status" VARCHAR(30) NOT NULL,
    "ErrorMessage" TEXT NULL,
    "DispatchedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX "IX_NotificationLogs_RecipientEmployeeId" ON "NotificationLogs" ("RecipientEmployeeId");
CREATE INDEX "IX_NotificationLogs_DispatchedAt" ON "NotificationLogs" ("DispatchedAt");
CREATE INDEX "IX_NotificationLogs_Status" ON "NotificationLogs" ("Status");
```

