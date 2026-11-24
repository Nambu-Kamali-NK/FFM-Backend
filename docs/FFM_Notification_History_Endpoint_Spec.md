# FFM Notification Service — Notification History Endpoint Specification

## 1. Document Overview
* **Document Reference:** `FFM-SPEC-NOTIF-HIST-V1.0`
* **Service:** `FFM.Notification` (Port 5040)
* **Module:** Notification History & User Inbox Governance
* **HTTP Method & Route:** `GET /api/v1/Notifications/history`
* **Architectural Standard:** Strictly Zero Path Parameters. All filters, search terms, and pagination parameters passed exclusively via Query Strings.
* **Authentication:** Bearer JWT (RS256) / Development Bypass Mode.

---

## 2. Business Scope & Purpose
The `GET /api/v1/Notifications/history` endpoint serves as the single source of truth for in-app alert feeds, worker inbox notifications, and administrative dispatch audit logs:
1. **User Notification Feed:** Allows mobile apps and web portals to display unread/read alert feeds for specific workers (e.g. `recipientEmployeeId=1`).
2. **Unread Counter:** Provides real-time calculation of total unread notifications for badge rendering on bell icons.
3. **Multi-Criteria Filtering:** Enables filtering by read status (`isRead=false` for inbox, `isRead=true` for archived), delivery status (`DELIVERED`, `FAILED`, `NO_ACTIVE_DEVICE`), and delivery channel (`PUSH`, `EMAIL`, `SMS`).
4. **Keyword Search & Pagination:** Supports searching inside notification titles and message bodies with deterministic pagination (`page`, `pageSize`) and date sorting (`sortBy=dispatchedAt`, `sortOrder=desc`).

---

## 3. API Contract

### Request: `GET /api/v1/Notifications/history`
**Headers:**
* `Accept: application/json`
* `Authorization: Bearer <TOKEN>` *(Required in Production; Optional in DevBypass)*

**Query Parameters (Zero Path Parameters):**
| Parameter | Type | Required? | Default | Validation & Constraints | Description |
| :--- | :---: | :---: | :---: | :--- | :--- |
| `recipientEmployeeId` | `int?` | Optional | `null` | Must be $> 0$ if provided. | Filters history for a specific worker. If omitted, returns organization-wide history (Admin mode). |
| `isRead` | `bool?` | Optional | `null` | `true`, `false`, or omitted. | If `false`, returns unread notifications only. If `true`, returns read notifications. If omitted, returns both. |
| `status` | `string?` | Optional | `null` | `DELIVERED`, `FAILED`, `NO_ACTIVE_DEVICE` | Filters by delivery status. |
| `channel` | `string?` | Optional | `null` | `PUSH`, `EMAIL`, `SMS`, `IN_APP` | Filters by notification delivery channel. |
| `searchTerm` | `string?` | Optional | `null` | Max 100 characters. | Free-text search matching `Title` or `Body`. |
| `page` | `int` | Optional | `1` | Must be $\ge 1$. | Page number. |
| `pageSize` | `int` | Optional | `15` | $1 \le \text{pageSize} \le 100$. | Number of records per page. |
| `sortBy` | `string` | Optional | `dispatchedat` | `dispatchedat`, `id`, `recipientemployeeid` | Sorting column. |
| `sortOrder` | `string` | Optional | `desc` | `asc` or `desc`. | Sort direction (newest alerts first by default). |

---

### Response Payloads

#### 1. Success Response (`HTTP 200 OK`):
```json
{
  "success": true,
  "message": "Notification history retrieved successfully.",
  "data": {
    "items": [
      {
        "id": 105,
        "recipientEmployeeId": 1,
        "templateCode": "TASK_ASSIGNED",
        "channel": "PUSH",
        "title": "New Task Assigned: TSK-202610-0014",
        "body": "Hi Ahmad, new task #TSK-202610-0014 at Boushar Directorate has been assigned to you. Priority: HIGH.",
        "fcmMessageId": "projects/ffm-notification-api/messages/2e4481e5-a85d-4d49-807d-096b691bdec4",
        "status": "DELIVERED",
        "errorMessage": null,
        "isRead": false,
        "readAt": null,
        "dispatchedAt": "2026-11-18T05:15:30.1245890Z"
      },
      {
        "id": 104,
        "recipientEmployeeId": 1,
        "templateCode": "CHECKIN_REMINDER",
        "channel": "PUSH",
        "title": "Upcoming Site Visit: Qurm Beach",
        "body": "Reminder: Scheduled visit at Qurm Beach in 15 minutes.",
        "fcmMessageId": "projects/ffm-notification-api/messages/1a2b3c4d-0000-4d49-807d-096b691bdec4",
        "status": "DELIVERED",
        "errorMessage": null,
        "isRead": true,
        "readAt": "2026-11-18T05:20:00.0000000Z",
        "dispatchedAt": "2026-11-18T04:45:00.0000000Z"
      }
    ],
    "page": 1,
    "pageSize": 15,
    "totalCount": 42,
    "totalPages": 3,
    "unreadCount": 8,
    "hasPreviousPage": false,
    "hasNextPage": true
  },
  "error": null,
  "timestamp": "2026-11-18T05:25:30.2000000Z"
}
```

#### 2. Validation Failure (`HTTP 400 Bad Request`):
```json
{
  "success": false,
  "message": "Validation failed: Page must be greater than or equal to 1.",
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Page must be greater than or equal to 1.",
    "details": "Validation error"
  },
  "timestamp": "2026-11-18T05:25:30.2000000Z"
}
```

---

## 4. Database Query Strategy

Queries operate directly on the indexed `NotificationLogs` table:

```sql
-- Optimal query plan utilizing composite index IX_NotificationLogs_Recipient_IsRead
SELECT * 
FROM "NotificationLogs"
WHERE ("RecipientEmployeeId" = @recipientEmployeeId OR @recipientEmployeeId IS NULL)
  AND ("IsRead" = @isRead OR @isRead IS NULL)
  AND ("Status" = @status OR @status IS NULL)
  AND ("Channel" = @channel OR @channel IS NULL)
  AND (
      @searchTerm IS NULL 
      OR "Title" ILIKE '%' || @searchTerm || '%' 
      OR "Body" ILIKE '%' || @searchTerm || '%'
  )
ORDER BY "DispatchedAt" DESC
OFFSET @skip LIMIT @pageSize;
```

---

## 5. Domain & Clean Architecture Design

### 5.1 Application Layer: Query & DTOs
* **Query:** `GetNotificationHistoryQuery : IRequest<NotificationHistoryPagedResult>`
  ```csharp
  public class GetNotificationHistoryQuery : IRequest<NotificationHistoryPagedResult>
  {
      public int? RecipientEmployeeId { get; set; }
      public bool? IsRead { get; set; }
      public string? Status { get; set; }
      public string? Channel { get; set; }
      public string? SearchTerm { get; set; }
      public int Page { get; set; } = 1;
      public int PageSize { get; set; } = 15;
      public string SortBy { get; set; } = "dispatchedat";
      public string SortOrder { get; set; } = "desc";
  }
  ```
* **Response DTO:** `NotificationHistoryPagedResult`
  * Contains `IReadOnlyList<NotificationLogDto> Items`
  * Contains pagination metadata (`Page`, `PageSize`, `TotalCount`, `TotalPages`)
  * Contains `int UnreadCount` (live count of unread items for the recipient)

### 5.2 Application Layer: FluentValidation
* **Validator:** `GetNotificationHistoryQueryValidator : AbstractValidator<GetNotificationHistoryQuery>`
  * `Page >= 1`
  * `1 <= PageSize <= 100`
  * `RecipientEmployeeId > 0` when provided
  * `SortOrder` must be either `asc` or `desc`

### 5.3 Repository Layer: `INotificationLogRepository`
Add pagination method:
```csharp
Task<(IReadOnlyList<NotificationLog> Items, int TotalCount, int UnreadCount)> GetHistoryPagedAsync(
    int? recipientEmployeeId,
    bool? isRead,
    string? status,
    string? channel,
    string? searchTerm,
    int page,
    int pageSize,
    string sortBy,
    string sortOrder,
    CancellationToken cancellationToken = default);
```

### 5.4 API Layer: Controller Endpoint
Add `[HttpGet("history")]` to `DispatchController`:
```csharp
[HttpGet("history")]
[ProducesResponseType(typeof(ApiResponse<NotificationHistoryPagedResult>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetHistory(
    [FromQuery] GetNotificationHistoryRequest request,
    CancellationToken cancellationToken)
```

---

## 6. Test Plan

1. **Default Pagination:** Queries first 15 items ordered by `DispatchedAt` descending.
2. **Recipient Filter:** Queries alerts for employee `1` $\to$ only returns logs matching employee `1`.
3. **Unread Filter (`isRead=false`):** Only returns unread alerts and verifies `unreadCount` matches `items.Count`.
4. **Keyword Search:** Queries with `searchTerm=Boushar` $\to$ returns records whose title or body mentions "Boushar".
5. **Zero Records:** Queries non-existent employee ID $\to$ returns HTTP 200 OK with empty array `[]` and `totalCount: 0`.
6. **Negative Page / PageSize Validation:** Passes `page=0` or `pageSize=200` $\to$ returns HTTP 400 Bad Request with explicit validation message.
