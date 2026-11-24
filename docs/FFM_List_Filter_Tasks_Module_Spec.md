# List & Filter Tasks Module Specification

## 1. Document Metadata

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-FLD-LIST-TASKS-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Service** | Field Operations Service (`FFM.FieldOperations`) |
| **Module** | Module 2: Task Dispatch — Feature: List & Filter Tasks |
| **Default Port** | `5030` |
| **Method & Route** | `GET /api/v1/Tasks` |
| **Compliance** | ISO/IEC/IEEE 29148, OpenAPI 3.1, RFC 7807 (ProblemDetails) |
| **Authentication** | Bearer JWT RS256 (issued by Identity & Access Service) |
| **Classification** | Tier‑1 Core Dispatch & Field Schedule Ingestion |
| **Status** | Approved Formal Specification |
| **Parent Document** | `FFM_Field_Operations_Service_Spec.md`, Section 7.2 |

---

## 2. Objective & Functional Scope

The **List & Filter Tasks** module provides the operational query backbone for both web dispatchers and mobile field technicians. It allows filtering, searching, sorting, and paginating tasks across diverse operational scenarios.

### Key Capabilities:
1. **Dispatcher Multi-Filter Ingestion:** Enables dispatchers, supervisors, and admins to filter tasks by `employeeId`, `status`, `clientSiteId`, `priority`, and scheduled execution date range (`startDate` to `endDate`).
2. **Technician Self-Scoping Security:** When a field technician (`Role: EMPLOYEE`) calls the endpoint, the service **automatically and securely scopes the query to their own authenticated `userId`**, preventing unauthorized surveillance of other technicians' itineraries.
3. **Keyword Search:** Supports partial matching across `title`, business `taskNumber` (`TSK-YYYYMM-XXXX`), and client site name.
4. **Structured Pagination & Metadata:** Standard pagination with configurable page sizes (default: `20`, maximum: `100`), returning total records, total pages, and navigation flags.
5. **Deterministic Sorting:** Default chronological sorting by `scheduledStart ASC`, with optional overrides by `createdAt`, `priority`, or `status`.

---

## 3. Authorization & RBAC Enforcement Policy

| Role | Query Scope Allowed | Security & Data Isolation Rule |
| :--- | :---: | :--- |
| `SYSTEM_ADMIN` | Global (All Tasks) | Can query all tasks, or filter by any arbitrary `employeeId` or `clientSiteId`. |
| `ADMIN` | Global (Tenant Tasks) | Can query all tasks, or filter by any arbitrary `employeeId` or `clientSiteId`. |
| `MANAGER` | Team / Region Scope | Can query unassigned tasks and tasks for all field technicians. |
| `SUPERVISOR` | Team Scope | Can query unassigned tasks and tasks for their supervised team. |
| `EMPLOYEE` | **Personal Tasks Only** | **Forced Self-Scoping:** The system ignores any passed `employeeId` parameter and strictly forces `AssignedEmployeeId == CurrentUser.GetUserId()`. Unassigned (`CREATED`) tasks are hidden from technicians. |

* **Unauthenticated Requests:** Returns `401 Unauthorized`.

---

## 4. Query Parameters Specification

`GET /api/v1/Tasks` accepts standard URL query string parameters:

| Parameter | Type | Required? | Default | Constraints & Description |
| :--- | :---: | :---: | :---: | :--- |
| **`employeeId`** | `int` | No | `null` | Filters tasks assigned to a specific technician ID. *(Overridden to current user for `EMPLOYEE` role).* |
| **`status`** | `string` | No | `null` | Filters by status: `CREATED`, `ASSIGNED`, `EN_ROUTE`, `CHECKED_IN`, `COMPLETED`, `CANCELLED`. |
| **`clientSiteId`** | `int` | No | `null` | Filters tasks linked to a specific physical client location. |
| **`priority`** | `string` | No | `null` | Filters by urgency: `LOW`, `MEDIUM`, `HIGH`, `URGENT`. |
| **`startDate`** | `DateTime` | No | `null` | ISO-8601 UTC date or string (`yyyy-MM-dd`); filters `ScheduledStart >= startDate`. |
| **`endDate`** | `DateTime` | No | `null` | ISO-8601 UTC date or string (`yyyy-MM-dd`); filters `ScheduledEnd <= endDate`. |
| **`search`** | `string` | No | `null` | Substring match against `Title`, `TaskNumber`, or `ClientSite.Name`. Max 100 characters. |
| **`page`** | `int` | No | `1` | Page number, minimum value: `1`. |
| **`pageSize`** | `int` | No | `20` | Page size, range: `1` to `100`. |
| **`sortBy`** | `string` | No | `"scheduledStart"` | Field to sort: `scheduledStart`, `createdAt`, `priority`, `taskNumber`. |
| **`sortOrder`** | `string` | No | `"asc"` | Sort direction: `asc` or `desc`. |

---

## 5. Business Rules & Validation Policies

| Rule ID | Rule Name | Business Constraint | Error Code | HTTP Status |
| :--- | :--- | :--- | :--- | :---: |
| **BR-LTSK-01** | Pagination Bounds | `page` must be $\ge 1$. `pageSize` must be between `1` and `100`. | `INVALID_PAGINATION` | `400 Bad Request` |
| **BR-LTSK-02** | Employee Role Self-Scoping | If the caller possesses only the `EMPLOYEE` role, the server must automatically override any `employeeId` parameter with the caller's JWT `userId`. | Automated Security Guard | `200 OK` |
| **BR-LTSK-03** | Status Whitelist | If `status` is provided, it must be one of: `CREATED`, `ASSIGNED`, `EN_ROUTE`, `CHECKED_IN`, `COMPLETED`, `CANCELLED`. | `INVALID_STATUS` | `400 Bad Request` |
| **BR-LTSK-04** | Priority Whitelist | If `priority` is provided, it must be one of: `LOW`, `MEDIUM`, `HIGH`, `URGENT`. | `INVALID_PRIORITY` | `400 Bad Request` |
| **BR-LTSK-05** | Date Range Chronology | If both `startDate` and `endDate` are supplied, `endDate` must be $\ge$ `startDate`. | `INVALID_DATE_RANGE` | `400 Bad Request` |
| **BR-LTSK-06** | UTC Normalization | Incoming date filters (`startDate`, `endDate`) must be parsed and evaluated using UTC timestamps. | Automated Logic | `200 OK` |
| **BR-LTSK-07** | Relational Loading | Result records must eagerly include the linked `ClientSite` (e.g. `ClientSiteName`, `SiteCode`, coordinates) without N+1 query overhead. | Architecture Constraint | `200 OK` |

---

## 6. API Response Contracts

### 6.1 Success Response (`HTTP 200 OK`)

```http
GET /api/v1/Tasks?employeeId=8&status=ASSIGNED&page=1&pageSize=20
Authorization: Bearer <access_token>
```

```json
{
  "success": true,
  "message": "Tasks retrieved successfully.",
  "data": {
    "items": [
      {
        "id": 1,
        "taskNumber": "TSK-202610-0001",
        "title": "Quarterly POS Hardware Maintenance",
        "description": "Inspect terminal, replace battery backup, run diagnostic pass.",
        "priority": "HIGH",
        "status": "ASSIGNED",
        "clientSiteId": 1,
        "clientSiteName": "HDFC Bank Regional Office",
        "clientSiteCode": "SITE-BLR-001",
        "address": "42 MG Road, Ashok Nagar, Bengaluru",
        "latitude": 12.9715987,
        "longitude": 77.5945627,
        "radiusMeters": 150,
        "assignedEmployeeId": 8,
        "scheduledStart": "2026-11-25T09:30:00.000Z",
        "scheduledEnd": "2026-11-25T12:00:00.000Z",
        "createdByUserId": 12,
        "createdAt": "2026-10-22T06:55:49.835Z",
        "updatedAt": "2026-10-22T06:55:49.835Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  },
  "error": null,
  "timestamp": "2026-10-22T10:30:00.000Z"
}
```

---

### 6.2 Error Responses (RFC 7807 Standard)

#### 1. Invalid Date Range (`HTTP 400 Bad Request`):
```json
{
  "success": false,
  "message": "EndDate cannot be earlier than StartDate.",
  "data": null,
  "error": {
    "code": "INVALID_DATE_RANGE",
    "details": "Validation error"
  },
  "timestamp": "2026-10-22T10:30:05.000Z"
}
```

#### 2. Invalid Status Filter (`HTTP 400 Bad Request`):
```json
{
  "success": false,
  "message": "Invalid status 'IN_PROGRESS'. Valid statuses: CREATED, ASSIGNED, EN_ROUTE, CHECKED_IN, COMPLETED, CANCELLED.",
  "data": null,
  "error": {
    "code": "INVALID_STATUS",
    "details": "Validation error"
  },
  "timestamp": "2026-10-22T10:30:10.000Z"
}
```

#### 3. Invalid Pagination Boundaries (`HTTP 400 Bad Request`):
```json
{
  "success": false,
  "message": "PageSize must be between 1 and 100.",
  "data": null,
  "error": {
    "code": "INVALID_PAGINATION",
    "details": "Validation error"
  },
  "timestamp": "2026-10-22T10:30:15.000Z"
}
```

---

## 7. Architecture & Implementation Blueprint

### 7.1 Layered Structure

```
FFM.FieldOperations
├── FFM.FieldOperations.Application
│   ├── DTOs/
│   │   ├── TaskDto.cs                    # TaskDto & PagedResult<T>
│   │   └── GetTasksFilterQuery.cs        # Input query parameters model
│   ├── Queries/
│   │   └── GetTasksQuery.cs              # IRequest<PagedResult<TaskDto>>
│   └── Handlers/
│       └── GetTasksQueryHandler.cs       # Filters, Self-scoping & DTO projection
├── FFM.FieldOperations.Domain
│   └── Interfaces/ITaskRepository.cs     # GetPagedTasksAsync
├── FFM.FieldOperations.Infrastructure
│   └── Repositories/TaskRepository.cs    # EF Core IQueryable expression builder
└── FFM.FieldOperations.Api
    └── Controllers/TasksController.cs    # GET /api/v1/Tasks
```

### 7.2 Generic `PagedResult<T>` Model

```csharp
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### 7.3 Technician Self-Scoping Algorithm

```csharp
// In GetTasksQueryHandler.cs
var roles = _currentUserService.GetRoles();
var isEmployee = roles.Contains("EMPLOYEE") && !roles.Any(r => r is "ADMIN" or "SYSTEM_ADMIN" or "MANAGER" or "SUPERVISOR");

if (isEmployee)
{
    // Strictly isolate technician to their own tasks
    query.EmployeeId = _currentUserService.GetUserId();
}
```

---

## 8. Test Cases & Acceptance Criteria Matrix

| TC ID | Test Scenario | Query Filter | Role / Caller | Expected HTTP | Expected Behavior |
| :---: | :--- | :--- | :---: | :---: | :--- |
| **TC-LTSK-01** | List all tasks | None (default pagination) | `ADMIN` | `200 OK` | Returns paginated list of all system tasks. |
| **TC-LTSK-02** | Filter by specific technician | `?employeeId=8` | `MANAGER` | `200 OK` | Only tasks with `AssignedEmployeeId == 8` returned. |
| **TC-LTSK-03** | Technician viewing own tasks | `GET /api/v1/Tasks` | `EMPLOYEE` (Id: 8) | `200 OK` | Automatically filters to `AssignedEmployeeId == 8`. |
| **TC-LTSK-04** | Technician attempting to view other's tasks | `?employeeId=97` | `EMPLOYEE` (Id: 8) | `200 OK` | Parameter overridden; only tasks for `8` returned. |
| **TC-LTSK-05** | Filter by Status | `?status=ASSIGNED` | `MANAGER` | `200 OK` | Only tasks with `Status == "ASSIGNED"` returned. |
| **TC-LTSK-06** | Filter by Date Range | `?startDate=2026-11-20&endDate=2026-11-30` | `ADMIN` | `200 OK` | Only tasks scheduled within the window returned. |
| **TC-LTSK-07** | Substring Search | `?search=HVAC` | `SUPERVISOR` | `200 OK` | Matches task with title "Monthly HVAC Filter...". |
| **TC-LTSK-08** | Inverted Date Range | `?startDate=2026-11-30&endDate=2026-11-20` | `ADMIN` | `400 Bad Request` | Returns `INVALID_DATE_RANGE`. |
| **TC-LTSK-09** | Invalid Status Value | `?status=DRAFT` | `ADMIN` | `400 Bad Request` | Returns `INVALID_STATUS`. |
| **TC-LTSK-10** | Exceed Max Page Size | `?pageSize=500` | `ADMIN` | `400 Bad Request` | Returns `INVALID_PAGINATION`. |
| **TC-LTSK-11** | Missing Authentication | No token | Anonymous | `401 Unauthorized` | Returns `UNAUTHORIZED`. |
