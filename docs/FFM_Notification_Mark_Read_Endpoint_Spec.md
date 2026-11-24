# FFM Notification Service — Mark Notification as Read Specification

## 1. Document Overview
* **Document Reference:** `FFM-SPEC-NOTIF-READ-V1.0`
* **Service:** `FFM.Notification` (Port 5040)
* **Module:** Notification History & In-App Alerts Governance
* **HTTP Method & Route:** `PUT /api/v1/Notifications/read`
* **Architectural Standard:** Strictly Zero Path Parameters. All parameters and payload options encapsulated in the JSON Request Body.
* **Authentication:** Bearer JWT (RS256) / Development Bypass Mode.

---

## 2. Business Scope & Purpose
The `PUT /api/v1/Notifications/read` endpoint enables municipal workers, field inspectors, and portal supervisors to acknowledge and update the read state of incoming notifications:
1. **Single Notification Update:** Marks a specific notification record as read with an accurate UTC timestamp (`ReadAt`).
2. **Bulk / Batch Update:** Allows the client to mark an array of notification IDs as read in a single round-trip transaction.
3. **Mark-All Mode:** Allows a recipient employee to mark all their unread notifications as read simultaneously (e.g. clicking "Mark all as read" in the mobile app or web portal notification tray).
4. **Idempotency Guarantee:** If a notification is already marked as read, re-submitting the request succeeds without corrupting the original `ReadAt` timestamp.

---

## 3. API Contract

### Request: `PUT /api/v1/Notifications/read`
**Headers:**
* `Content-Type: application/json`
* `Authorization: Bearer <TOKEN>` *(Required in Production; Optional in DevBypass)*

**Request Payload Schema (`MarkNotificationReadRequest`):**
```json
{
  "notificationId": 104,
  "notificationIds": [104, 105, 106],
  "recipientEmployeeId": 1,
  "markAll": false
}
```

#### Field Specifications:
| Field Name | Type | Required? | Validation Rules | Description |
| :--- | :---: | :---: | :--- | :--- |
| `notificationId` | `int?` | Optional* | Must be $> 0$ if provided. | ID of a single notification to mark as read. |
| `notificationIds` | `List<int>?` | Optional* | Each item $> 0$; Max 100 items per batch. | Array of notification IDs to mark as read. |
| `recipientEmployeeId` | `int?` | Optional* | Must be $> 0$ if provided. | Recipient employee identifier (required when `markAll = true`). |
| `markAll` | `bool` | Optional | Defaults to `false`. | If `true`, marks all unread notifications for `recipientEmployeeId` as read. |

*\*Note: At least one targeting mechanism must be provided (`notificationId`, a non-empty `notificationIds` list, or `recipientEmployeeId` with `markAll = true`).*

---

### Response Payloads

#### 1. Success Response (`HTTP 200 OK`):
```json
{
  "success": true,
  "message": "Notifications marked as read successfully.",
  "data": {
    "updatedCount": 3,
    "readAt": "2026-11-17T11:25:30.1458920Z",
    "updatedIds": [104, 105, 106]
  },
  "error": null,
  "timestamp": "2026-11-17T11:25:30.2000000Z"
}
```

#### 2. Single Notification Success (`HTTP 200 OK`):
```json
{
  "success": true,
  "message": "Notification marked as read successfully.",
  "data": {
    "updatedCount": 1,
    "readAt": "2026-11-17T11:25:30.1458920Z",
    "updatedIds": [104]
  },
  "error": null,
  "timestamp": "2026-11-17T11:25:30.2000000Z"
}
```

#### 3. Validation Failure (`HTTP 400 Bad Request`):
```json
{
  "success": false,
  "message": "Validation failed: You must specify a notificationId, notificationIds, or recipientEmployeeId with markAll=true.",
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "You must specify a notificationId, notificationIds, or recipientEmployeeId with markAll=true.",
    "details": "Validation error"
  },
  "timestamp": "2026-11-17T11:25:30.2000000Z"
}
```

#### 4. Record Not Found (`HTTP 404 Not Found`):
```json
{
  "success": false,
  "message": "Notification with ID 9999 was not found.",
  "data": null,
  "error": {
    "code": "NOTIFICATION_NOT_FOUND",
    "message": "Notification with ID 9999 was not found.",
    "details": "Not Found"
  },
  "timestamp": "2026-11-17T11:25:30.2000000Z"
}
```

---

## 4. Database Schema Migration

### Table Alteration: `NotificationLogs`
To track read status with zero performance degradation on large log volumes:

```sql
-- 1. Add IsRead and ReadAt columns
ALTER TABLE "NotificationLogs" 
ADD COLUMN IF NOT EXISTS "IsRead" BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE "NotificationLogs" 
ADD COLUMN IF NOT EXISTS "ReadAt" TIMESTAMP WITH TIME ZONE NULL;

-- 2. Performance Index for Unread Inquiries & Batch Updates
CREATE INDEX IF NOT EXISTS "IX_NotificationLogs_Recipient_IsRead" 
ON "NotificationLogs" ("RecipientEmployeeId", "IsRead");
```

---

## 5. Domain & Clean Architecture Design

### 5.1 Domain Layer: `NotificationLog.cs`
Update entity to encapsulate business invariants for state transition:
```csharp
public class NotificationLog
{
    // Existing properties: Id, RecipientEmployeeId, TemplateCode, Channel, Title, Body, FcmMessageId, Status, DispatchedAt...
    
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public void MarkAsRead(DateTime? readAt = null)
    {
        if (!IsRead)
        {
            IsRead = true;
            ReadAt = readAt ?? DateTime.UtcNow;
        }
    }
}
```

### 5.2 Application Layer: MediatR Command & DTOs
* **Command:** `MarkNotificationReadCommand : IRequest<MarkNotificationReadResponse>`
* **Handler:** `MarkNotificationReadCommandHandler`
* **Validator:** `MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>`
  * Validates positive integer IDs.
  * Ensures batch size does not exceed 100 IDs.
  * Validates that `recipientEmployeeId` is provided if `markAll == true`.

### 5.3 Repository Layer: `INotificationLogRepository`
Add targeted update operations:
```csharp
Task<int> MarkAsReadAsync(IEnumerable<int> notificationIds, DateTime readAt, CancellationToken cancellationToken = default);
Task<int> MarkAllAsReadForRecipientAsync(int recipientEmployeeId, DateTime readAt, CancellationToken cancellationToken = default);
```

### 5.4 API Controller: `NotificationsController.cs` / `DispatchController.cs`
Expose the endpoint:
```csharp
[HttpPut("read")]
[ProducesResponseType(typeof(ApiResponse<MarkNotificationReadResponse>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
public async Task<IActionResult> MarkAsRead([FromBody] MarkNotificationReadRequest request, CancellationToken cancellationToken)
```

---

## 6. Test Plan (Unit, Integration & Concurrency)

1. **Single Notification Test:** Mark an unread notification as read $\to$ verifies `IsRead == true`, `ReadAt` set to UTC timestamp.
2. **Idempotency Test:** Mark an already read notification $\to$ verifies success without altering existing `ReadAt`.
3. **Batch Mark Test:** Pass `[10, 11, 12]` $\to$ verifies all 3 records transition to read and `updatedCount == 3`.
4. **Mark-All Test:** Pass `recipientEmployeeId: 1, markAll: true` $\to$ verifies all unread notifications for employee 1 become read.
5. **Non-Existent ID Test:** Pass `notificationId: 9999` $\to$ returns HTTP 404 Not Found.
6. **Zero-Parameter Payload:** Pass empty body `{}` $\to$ returns HTTP 400 Bad Request with explicit validation message.
7. **Cross-Tenant / Cross-User Isolation:** Ensure employee 1 cannot mark employee 2's notifications when caller context is validated.
