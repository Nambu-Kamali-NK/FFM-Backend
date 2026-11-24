# Formal Technical Specification Document (Backend SDD)

## Module: Workforce Service
### System: Field Force Management (FFM) Platform

| Document Attribute | Specification Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-WORKFORCE-V1.0` |
| **Standard Compliance** | ISO/IEC/IEEE 29148, aligned with `FFM-SPEC-AUTH-LOGIN-V1.1` (Identity & Access Service) |
| **Module Classification** | Tier‑1 Core Business Service |
| **Status** | Draft Formal Specification — Pending Review and Approval |
| **Target Clients** | FFM Web Portal (Admin/Manager/Supervisor) & FFM Mobile / PWA Client (Field Employee) |
| **Perspective** | **Backend / Server‑Side Only** |
| **Parent Document** | FFM System‑Level Technical Specification, Section 13.2 |

---

## 1️⃣ Pre‑development Checklist

- All NuGet packages restore cleanly (`dotnet restore`).
- PostgreSQL connection string in `appsettings.json` points to a reachable DB; role `ffmwf_user` has LOGIN rights.
- EF Core migrations are applied (`dotnet ef database update`) before the service starts.
- Identity & Access Service is reachable; JWKS/OIDC discovery endpoint is configured for token validation.
- Redis connection is configured for caching (org‑hierarchy lookups, shift/roster lookups).
- RabbitMQ connection is configured for publishing/consuming Workforce domain events.
- File/media storage (receipts) credentials are valid.
- `ASPNETCORE_ENVIRONMENT` set appropriately.
- Docker images built; service port free (default `5010`).

---

## 2️⃣ Technical Glossary

- **Workforce Service** – The backend service owning employee, attendance, shift/roster, leave/permission, and organization/hierarchy data.
- **Employee** – A user of the FFM platform who performs or manages field work; linked 1:1 to an Identity & Access Service account (`userId`).
- **Organization Unit (Org Unit)** – A node in the company's structural hierarchy (e.g., Region, Branch, Department, Team).
- **Reporting Line** – The manager/supervisor relationship an employee is attached to for approvals and visibility.
- **Shift** – A defined working time template (start/end time, break rules) that can be assigned to employees.
- **Roster** – The calendar assignment of shifts to employees across a date range.
- **Attendance Event** – A discrete check‑in, check‑out, break‑start, break‑end, or errand record tied to an employee and a timestamp.
- **Leave** – A planned, approvable absence spanning one or more full/half days, deducted from a leave balance.
- **Permission** *(HR sense)* – A short, same‑day approvable absence (e.g., 1–3 hours), distinct from RBAC "permissions" defined by Identity & Access Service.
- **Missed Punch** – An attendance day lacking an expected check‑in or check‑out, requiring regularization.
- **Approval Workflow** – The configurable chain (e.g., Supervisor → Manager) through which leave and permission requests are approved or rejected.
- **Read‑Through Cache** – A caching pattern where Redis is checked first and populated from PostgreSQL on a miss; PostgreSQL remains the source of truth.

---

## 3️⃣ Scope & System Overview

### 3.1 Purpose

The Workforce Service is the system of record for **who the field workforce is, how it is organized, when it works, and what it costs to run**. Per the FFM System‑Level Specification (§13.2), it is responsible for:

- Employee Management
- Attendance Management
- Shift & Roster Management
- Leave & Permission Management
- Organization & Hierarchy Management

### 3.2 In‑Scope (Backend)

- Employee profile creation, activation, deactivation, and role/team assignment (in coordination with Identity & Access Service).
- Organization hierarchy definition (regions, branches, departments, teams) and reporting‑line assignment.
- Shift template definition and roster assignment.
- Attendance recording (check‑in/out, breaks, errands), missed‑punch detection, and regularization workflow.
- Leave type configuration, leave balance tracking, leave request/approval workflow.
- Short‑duration permission request/approval workflow.
- Publishing domain events for consumption by Field Operations, Reporting, Dashboard, and Notification services.

### 3.3 Out‑of‑Scope (External Modules)

- **Authentication, tokens, MFA, device binding, and RBAC permission enforcement** — owned by Identity & Access Service. Workforce Service trusts validated JWTs and enforces authorization using roles/permissions carried in the token.
- **GPS/geofence validation of attendance location** — owned by GPS Tracking Service. Workforce Service stores the attendance record and, where location is supplied, a reference to the validated location result; it does not compute geofence membership itself.
- **Field task/visit execution** — owned by Field Operations Service.
- **Report generation (Excel/PDF/CSV) and dashboard read models** — owned by Reporting Service and Dashboard/Read Model respectively; Workforce Service publishes the source events/data they consume.
- **Notification delivery (email/SMS/push)** — owned by Notification Service; Workforce Service requests notifications via events, it does not send them directly.
- **Receipt/photo binary storage** — owned by File and Media Storage; Workforce Service stores only file references.

---

## 4️⃣ Architectural Overview

- **Deployment**: Stateless ASP.NET Core Web API running in a Docker container, backed by PostgreSQL, following Clean Architecture boundaries consistent with other FFM services.
- **Identity Trust Boundary**: Workforce Service validates incoming JWTs against Identity & Access Service's JWKS; it does not independently authenticate users.
- **Data Ownership**: Workforce Service owns Employee, Organization, Shift, Roster, Attendance, Leave, and Permission data. It does not write to the Identity, Field Operations, or GPS databases.
- **Synchronous vs. Asynchronous**:
  - Synchronous REST is used for direct reads/writes initiated by a client (e.g., check‑in, submit leave, get roster).
  - Asynchronous events are published for cross‑service effects (e.g., `EmployeeActivated` triggering downstream provisioning, `AttendanceCheckedIn` feeding the Dashboard read model).
- **Caching**: Redis is used as a read‑through cache for the organization hierarchy tree and the active roster window; it is never the source of truth.
- **Background Processing**: Missed‑punch detection, leave‑balance accrual, and roster‑generation‑ahead jobs run as scheduled background workers, not inline with API requests.

---

## 5️⃣ Functional Requirements (FR)

| ID | Domain | Description |
|---|---|---|
| **FR1 – Employee Lifecycle** | Employee Management | Create, update, activate, deactivate, and reactivate employee profiles; link each employee to exactly one Identity & Access Service `userId`; assign primary role and organization/team. |
| **FR2 – Organization & Hierarchy** | Org & Hierarchy | Define org units (Region/Branch/Department/Team or configurable levels), assign employees to org units, define manager/supervisor reporting lines, and expose the resolved hierarchy for authorization‑scope and approval‑routing use. |
| **FR3 – Shift Definition** | Shift & Roster | Define reusable shift templates (start time, end time, break windows, grace periods, overnight‑shift flag). |
| **FR4 – Roster Assignment** | Shift & Roster | Assign shifts to employees across date ranges; support fixed, rotating, and split‑shift rostering; detect and reject overlapping shift assignments for the same employee/day. |
| **FR5 – Attendance Recording** | Attendance | Record check‑in, check‑out, break‑start, break‑end, and errand events; compute worked duration, late‑arrival, and early‑departure flags against the employee's assigned shift for that day. |
| **FR6 – Missed‑Punch Handling** | Attendance | Detect days with an expected but missing check‑in/check‑out; allow the employee to submit, and an approver to accept/reject, a regularization request. |
| **FR7 – Leave Type & Balance** | Leave & Permission | Configure leave types (e.g., Casual, Sick, Earned/Annual, Unpaid) with accrual rules, and maintain a per‑employee, per‑leave‑type balance. |
| **FR8 – Leave Request Workflow** | Leave & Permission | Accept employee leave requests (type, date range, half‑day flag, reason), validate against available balance and overlapping requests, and route through the configured approval workflow. |
| **FR9 – Permission Request Workflow** | Leave & Permission | Accept employee short‑duration (same‑day, hour‑based) permission requests and route them through the configured approval workflow, independent of full‑day leave balances. |
| **FR10 – Domain Event Publication** | Cross‑cutting | Publish domain events for employee, attendance, leave, and permission state transitions for consumption by other FFM services. |
| **FR11 – Audit Trail** | Cross‑cutting | Record an immutable audit entry for every state‑changing operation (who, what, when, before/after status). |

---

## 6️⃣ Use Cases (UC)

### UC1 – Employee Onboarding
1. An authorized `ADMIN`/`SYSTEM_ADMIN` creates an employee profile referencing an existing Identity & Access Service `userId` (FR1).
2. Workforce Service validates the `userId` is not already linked to another employee record.
3. The employee is assigned to an org unit and, where applicable, a reporting manager (FR2).
4. Employee status is set to `PENDING_ACTIVATION`; on completion of required setup, an authorized user activates the employee (`ACTIVE`).
5. An `EmployeeCreated` event, and later an `EmployeeActivated` event, are published (FR13).

### UC2 – Daily Check‑In / Check‑Out (Happy Path)
1. Employee calls `POST /api/v1/attendance/check-in` with optional device/location reference.
2. Workforce Service verifies the employee is `ACTIVE` and has a shift assigned for the current day (FR5).
3. Workforce Service records the check‑in timestamp and computes late‑arrival status against the shift start time plus configured grace period.
4. Later, employee calls `POST /api/v1/attendance/check-out`; worked duration and early‑departure status are computed.
5. `AttendanceCheckedIn` / `AttendanceCheckedOut` events are published (FR13) and an audit entry is recorded for each (FR14).

### UC3 – Missed‑Punch Regularization
1. A scheduled background job detects an attendance day with a check‑in but no check‑out (or neither) once the shift window plus grace period has elapsed (FR6).
2. The day is marked `MISSED_PUNCH` and the employee is notified (via a published event consumed by Notification Service).
3. Employee submits a regularization request with a reason and claimed times.
4. The employee's approver reviews and approves or rejects the request; approval updates the attendance record to `REGULARIZED` with the corrected times and a reference to the original raw event.

### UC4 – Leave Request and Approval
1. Employee calls `POST /api/v1/leave/requests` with leave type, start date, end date, half‑day flag, and reason (FR8).
2. Workforce Service validates sufficient leave balance for the type and checks for overlapping approved/pending leave.
3. Request is created in `PENDING` status and routed to the employee's resolved approver (FR2 hierarchy).
4. Approver calls `POST /api/v1/leave/requests/{id}/decision` with `APPROVE` or `REJECT` and optional comments.
5. On approval, the leave balance is decremented and a `LeaveApproved` event is published; on rejection, no balance change occurs and a `LeaveRejected` event is published.

### UC5 – Short‑Duration Permission Request
1. Employee calls `POST /api/v1/permissions/requests` with a date, start time, end time, and reason (FR9).
2. Workforce Service validates the requested window does not exceed the configured maximum duration and does not overlap an existing approved permission or leave for that day.
3. Request is routed to the resolved approver; approval/rejection follows the same pattern as UC4 but does not affect leave balances.

### UC6 – Organization Hierarchy Change
1. An authorized `ADMIN` reassigns an employee from one org unit/manager to another (FR2).
2. Workforce Service validates the new org unit exists and is active, and that the change does not create a reporting cycle (an employee cannot become their own ancestor in the hierarchy).
3. Pending leave/permission approvals already routed to the previous approver remain with that approver unless explicitly reassigned by policy; new requests route to the new approver.
4. An `EmployeeReassigned` event is published.

---

## 7️⃣ Business Rules (BR)

| Rule ID | Domain | Rule | Enforcement |
|---|---|---|---|
| **BR‑1** | Employee | One Identity account maps to exactly one employee record. | Unique constraint on `user_id`; creation rejected on duplicate. |
| **BR‑2** | Employee | Only `ACTIVE` employees may check in or request leave/permission. | Status check at the start of every write operation. |
| **BR‑3** | Org & Hierarchy | An org unit cannot be its own ancestor or descendant (no cycles). | Cycle check performed before persisting a hierarchy change. |
| **BR‑4** | Org & Hierarchy | Every active employee must have a resolvable approver for leave/permission routing, unless explicitly exempted by role policy (e.g., top‑level `SYSTEM_ADMIN`). | Validation at employee activation and at hierarchy‑change time. |
| **BR‑5** | Shift & Roster | An employee cannot have two overlapping shift assignments on the same calendar day. | Overlap check at roster‑assignment time. |
| **BR‑6** | Attendance | Check‑out is only accepted after a corresponding open check‑in exists for the employee. | State check; otherwise reject with a structured error. |
| **BR‑7** | Attendance | Late‑arrival/early‑departure flags are computed against the shift assigned for that specific day, including its configured grace period. | Computed at event‑recording time; not client‑supplied. |
| **BR‑8** | Leave | A leave request cannot exceed the employee's current available balance for that leave type unless the leave type is configured to allow negative balance (e.g., unpaid leave). | Balance check at request‑submission time. |
| **BR‑9** | Leave | Overlapping leave requests (approved or pending) for the same employee and date range are rejected. | Overlap check at request‑submission time. |
| **BR‑10** | Permission | A same‑day permission request cannot exceed the configured maximum duration (default configurable, e.g., 4 hours). | Duration check at request‑submission time. |
| **BR‑11** | Approval | Only the resolved approver (or a role with an explicit override permission) may approve/reject a given leave or permission request. | Authorization check using resolved hierarchy plus RBAC permission. |
| **BR‑12** | Cross‑cutting | Every state‑changing operation produces an audit entry; audit entries are immutable. | Enforced at the persistence layer; no update/delete API exists for audit records. |

---

## 8️⃣ API Contracts (OpenAPI 3.1 Summary)

All endpoints require a valid Access Token issued by Identity & Access Service (`Authorization: Bearer <token>`). Authorization is additionally enforced per endpoint using RBAC permissions (see §13.3).

### 8.1 Employee Management

- `POST /api/v1/employees` — create employee (`ADMIN`/`SYSTEM_ADMIN`).
- `GET /api/v1/employees/{id}` — retrieve employee profile.
- `PUT /api/v1/employees/{id}` — update profile fields.
- `POST /api/v1/employees/{id}/activate` — set status `ACTIVE`.
- `POST /api/v1/employees/{id}/deactivate` — set status `DEACTIVATED`.
- `GET /api/v1/employees?orgUnitId=&status=&managerId=` — list/filter employees.

### 8.2 Organization & Hierarchy

- `POST /api/v1/org-units` — create org unit.
- `GET /api/v1/org-units/tree` — retrieve resolved hierarchy tree.
- `POST /api/v1/employees/{id}/reassign` — change org unit / reporting manager.

### 8.3 Shift & Roster

- `POST /api/v1/shifts` — define a shift template.
- `POST /api/v1/rosters` — assign shift(s) to employee(s) over a date range.
- `GET /api/v1/rosters?employeeId=&from=&to=` — retrieve roster for an employee/date range.

### 8.4 Attendance

- `POST /api/v1/attendance/check-in`
```json
{ "checkInNotes": "string or null", "latitude": 0.0, "longitude": 0.0 }
```
- `POST /api/v1/attendance/check-out`
```json
{ "checkOutNotes": "string or null", "latitude": 0.0, "longitude": 0.0 }
```
- `POST /api/v1/attendance/break-start` / `POST /api/v1/attendance/break-end`
- `GET /api/v1/attendance?employeeId=&from=&to=` — attendance history.
- `POST /api/v1/attendance/{id}/regularize` — submit regularization request.
- `POST /api/v1/attendance/{id}/regularize/decision` — approver decision.

**Responses**: `200 OK` on success; `409 Conflict` for BR‑6 violations (e.g., duplicate check‑in); `422 Unprocessable Entity` for validation failures.

### 8.5 Leave & Permission

- `GET /api/v1/leave/types` — list configured leave types.
- `GET /api/v1/leave/balance?employeeId=` — current balances.
- `POST /api/v1/leave/requests`
```json
{ "employeeId": "uuid", "leaveTypeId": "uuid", "startDate": "date", "endDate": "date", "halfDay": false, "reason": "string" }
```
- `POST /api/v1/leave/requests/{id}/decision`
```json
{ "decision": "APPROVE | REJECT", "comments": "string or null" }
```
- `POST /api/v1/permissions/requests`
```json
{ "employeeId": "uuid", "date": "date", "startTime": "HH:mm", "endTime": "HH:mm", "reason": "string" }
```
- `POST /api/v1/permissions/requests/{id}/decision` — same shape as leave decision.

All error responses use the consistent structured error format defined in §11.

---

## 9️⃣ Data Model Overview (ER Summary)

- **EMPLOYEES** → `ORG_UNITS` (via `org_unit_id`), self‑referential `manager_id`, → `SHIFT_ASSIGNMENTS`, `ATTENDANCE_RECORDS`, `LEAVE_REQUESTS`, `PERMISSION_REQUESTS`.
  - Key fields: `user_id` (FK to Identity, unique), `employee_code`, `org_unit_id`, `manager_id`, `role_snapshot`, `status`, `hired_at`.
- **ORG_UNITS** — `id`, `name`, `type` (Region/Branch/Department/Team/…), `parent_org_unit_id`, `is_active`.
- **SHIFTS** — `id`, `name`, `start_time`, `end_time`, `break_windows` (structured), `grace_period_minutes`, `is_overnight`.
- **SHIFT_ASSIGNMENTS (Roster)** — `id`, `employee_id`, `shift_id`, `date`, `status`.
- **ATTENDANCE_RECORDS** — `id`, `employee_id`, `date`, `check_in_at`, `check_out_at`, `break_events` (structured), `worked_minutes`, `status` (`PRESENT`, `ON_BREAK`, `COMPLETED`, `CORRECTED`, `INCOMPLETE`), `check_in_notes`, `check_out_notes`, `location_ref`, `latitude`, `longitude`, `is_geofence_validated`.
- **LEAVE_TYPES** — `id`, `name`, `accrual_rule`, `allow_negative_balance`.
- **LEAVE_BALANCES** — `employee_id`, `leave_type_id`, `available_days`, `updated_at`.
- **LEAVE_REQUESTS** — `id`, `employee_id`, `leave_type_id`, `start_date`, `end_date`, `half_day`, `reason`, `status` (`PENDING`, `APPROVED`, `REJECTED`, `CANCELLED`), `approver_id`, `decided_at`.
- **PERMISSION_REQUESTS** — `id`, `employee_id`, `date`, `start_time`, `end_time`, `reason`, `status`, `approver_id`, `decided_at`.
- **WORKFORCE_AUDIT_LOGS** — `id`, `entity_type`, `entity_id`, `action`, `actor_user_id`, `before_state`, `after_state`, `correlation_id`, `created_at`.

All primary keys are UUIDs. Foreign keys, uniqueness constraints, and referential integrity are mandatory, consistent with the Identity & Access Service data‑model conventions.

---

## 🔟 Security & Non‑Functional Requirements (NFR)

- **AuthN/AuthZ**: All endpoints require a valid Identity & Access Service JWT; authorization uses RBAC roles/permissions from the token (§13.3). Workforce Service performs no password or credential handling.
- **Transport**: TLS 1.2 minimum, TLS 1.3 preferred, consistent with platform‑wide security concepts.
- **Data Scoping**: Managers/Supervisors may only read/act on employees within their resolved org‑hierarchy scope unless granted an elevated permission.
- **Performance**: 95th‑percentile latency ≤ 300 ms for check‑in/check‑out and leave submission endpoints under nominal load.
- **Scalability**: Stateless service; horizontal scaling via additional instances; Redis used for shared read‑through caching of hierarchy/roster lookups.
- **Availability**: Target 99.9% SLA, consistent with a Tier‑1 core business service.
- **Fail‑Secure**: Failure of downstream dependencies (Notification, GPS, File Storage) must not silently mark an attendance or leave record as approved/valid; the affected operation must fail explicitly or queue for retry.
- **Idempotency**: Check‑in/check‑out and decision endpoints must be idempotent with respect to duplicate client retries (e.g., using a client‑supplied idempotency key or natural state checks).

---

## 11️⃣ Error Dictionary

| Error Code | HTTP | User Message | Internal Reason |
|---|---|---|---|
| `WF_EMPLOYEE_NOT_ACTIVE` | 403 | Your account is not active for this action. | Employee status is not `ACTIVE` |
| `WF_EMPLOYEE_ALREADY_LINKED` | 409 | This account is already linked to an employee profile. | Duplicate `user_id` on creation |
| `WF_ORG_HIERARCHY_CYCLE` | 422 | This change would create an invalid reporting structure. | Cycle detected in org hierarchy |
| `WF_NO_APPROVER_RESOLVED` | 422 | No approver is configured for this employee. | Hierarchy resolution failed (BR‑4) |
| `WF_SHIFT_OVERLAP` | 409 | This employee already has a shift assigned for this day. | Overlapping roster assignment (BR‑5) |
| `WF_ATTENDANCE_NO_OPEN_CHECKIN` | 409 | No open check‑in found for this employee. | Check‑out without prior check‑in (BR‑6) |
| `WF_ATTENDANCE_DUPLICATE_CHECKIN` | 409 | You are already checked in. | Duplicate check‑in for the day |
| `WF_LEAVE_INSUFFICIENT_BALANCE` | 422 | Insufficient leave balance for this request. | Balance check failed (BR‑8) |
| `WF_LEAVE_OVERLAP` | 409 | You already have a leave request for these dates. | Overlapping leave (BR‑9) |
| `WF_PERMISSION_DURATION_EXCEEDED` | 422 | Requested permission exceeds the allowed duration. | Duration check failed (BR‑10) |
| `WF_APPROVAL_NOT_AUTHORIZED` | 403 | You are not authorized to approve this request. | Actor is not the resolved approver (BR‑11) |
| `WF_VALIDATION_FAILED` | 400 | The request contains invalid or missing fields. | General input validation failure |

---

## 12️⃣ Acceptance Criteria

| AC | Requirement | Verification |
|---|---|---|
| **AC‑01** | Employee creation links exactly one Identity `userId` | Integration test rejects a second employee record for the same `userId`. |
| **AC‑02** | Only `ACTIVE` employees can check in | Test verifies `403 WF_EMPLOYEE_NOT_ACTIVE` for `PENDING_ACTIVATION`/`DEACTIVATED` employees. |
| **AC‑03** | Org hierarchy cannot form a cycle | Test attempts a circular reassignment and expects `422 WF_ORG_HIERARCHY_CYCLE`. |
| **AC‑04** | Overlapping shift assignments are rejected | Test assigns two overlapping shifts to the same employee/day and expects `409`. |
| **AC‑05** | Check‑out requires a prior open check‑in | Test calls check‑out with no open check‑in and expects `409`. |
| **AC‑06** | Late‑arrival/early‑departure are computed server‑side | Test submits a check‑in after shift start plus grace period and verifies `is_late = true`. |
| **AC‑07** | Missed punches are detected and regularizable | Scheduled‑job test creates a missing check‑out scenario; verifies status `MISSED_PUNCH` and a successful regularization flow to `REGULARIZED`. |
| **AC‑08** | Leave requests respect available balance | Test submits a request exceeding balance for a non‑negative‑balance leave type and expects `422`. |
| **AC‑09** | Overlapping leave requests are rejected | Test submits two overlapping requests and expects the second to return `409`. |
| **AC‑10** | Leave approval decrements balance; rejection does not | Test verifies balance change only on `APPROVE`. |
| **AC‑11** | Permission requests enforce max duration | Test submits a request exceeding the configured maximum and expects `422`. |
| **AC‑12** | Only the resolved approver can decide a request | Test attempts a decision from a non‑approver user and expects `403`. |
| **AC‑13** | Every state change produces an audit entry | DB query confirms an audit record for each create/update/decision operation. |
| **AC‑14** | Domain events are published for all major transitions | Integration test consumes `EmployeeCreated`, `AttendanceCheckedIn`, `AttendanceCheckedOut`, `LeaveApproved`, `LeaveRejected` from the message broker. |
| **AC‑15** | Authorization is enforced server‑side on every endpoint | Test verifies `401` for missing/invalid token and `403` for insufficient permission, independent of client‑side UI restrictions. |

---

## 13. Approved Implementation Baseline and Resolved Clarifications

This section records decisions to be confirmed during review. Where finalized, it takes precedence over earlier sections, consistent with the pattern used in the Login Module specification. Items below are **proposed defaults pending approval**, not yet finalized requirements, unless the parent System‑Level Specification already mandates them.

### 13.1 Completion Rule

The implementer shall review and implement the entire Workforce Service specification before declaring the module complete. A successful compilation, health check, or single successful API call is not feature completion. Completion requires all applicable acceptance criteria in §12 to pass.

### 13.2 Canonical Roles Referenced

Workforce Service reuses the canonical roles defined by Identity & Access Service: `SYSTEM_ADMIN`, `ADMIN`, `MANAGER`, `SUPERVISOR`, `EMPLOYEE`. Workforce Service does not define its own role set; it maps permission checks onto these roles plus the resolved org‑hierarchy scope.

### 13.3 Permission Model (Proposed)

Consistent with Identity & Access Service's `Resource.Action.Scope` model, proposed Workforce permissions include:

- `Employee.View.Own` / `Employee.View.Team` / `Employee.View.All`
- `Employee.Manage.Team` / `Employee.Manage.All`
- `Attendance.View.Own` / `Attendance.View.Team`
- `Attendance.Regularize.Approve`
- `Leave.Request.Own`
- `Leave.Approve.Team`
- `Permission.Request.Own` / `Permission.Approve.Team`
- `OrgUnit.Manage.All`

These require confirmation against the platform‑wide permission catalog before implementation.

### 13.4 Employee Statuses

Canonical employee statuses: `PENDING_ACTIVATION`, `ACTIVE`, `DEACTIVATED`. (Note: this is distinct from, but coordinated with, the Identity & Access Service account statuses — an employee can only be `ACTIVE` in Workforce Service if the linked Identity account is also `ACTIVE`.)

### 13.5 Approval Routing Policy

Default proposed policy: a request routes to the employee's immediate `manager_id`. If unresolved, it escalates to that manager's manager, up to a configurable maximum depth, then to a designated fallback role (e.g., `ADMIN`). This policy must be confirmed before implementation.

### 13.6 Attendance and Location

Workforce Service accepts an optional `locationRef` on check‑in/check‑out but does not itself validate geofence membership; if location validation is required for a given deployment, GPS Tracking Service performs that validation and Workforce Service stores the resulting reference/outcome. This boundary must not be blurred in implementation.

### 13.7 Required Asynchronous Events

Events are asynchronous side effects and must be idempotent for consumers, following the outbox pattern used by Identity & Access Service where reliable delivery is required.

Proposed initial events: `EmployeeCreated`, `EmployeeActivated`, `EmployeeDeactivated`, `EmployeeReassigned`, `AttendanceCheckedIn`, `AttendanceCheckedOut`, `AttendanceMissedPunch`, `AttendanceRegularized`, `LeaveRequested`, `LeaveApproved`, `LeaveRejected`, `PermissionRequested`, `PermissionApproved`, `PermissionRejected`.

Events must contain approved identifiers, timestamps, and correlation IDs, and must never contain unnecessary sensitive personal data.

### 13.8 Dependency Boundaries

| Dependency | Responsibility |
|---|---|
| Identity & Access Service | Authentication, token issuance/validation, RBAC role/permission source |
| GPS Tracking Service | Location/geofence validation for attendance, when enabled |
| Notification Service | Delivery of approval/rejection and missed‑punch notifications |
| Reporting Service | Consumes Workforce events/data for attendance and leave reports |
| Dashboard / Read Model | Consumes Workforce events for live employee/attendance/leave metrics |
| Redis | Distributed cache for org‑hierarchy tree and active roster window |
| RabbitMQ | Asynchronous event transport only; not used for synchronous decision‑making |

### 13.9 Implementation and Quality Rules

- Follow Clean Architecture/modular boundaries; controllers shall not contain business logic.
- Use dependency injection and asynchronous I/O.
- Use EF Core migrations and PostgreSQL‑compatible mappings.
- Use centralized exception/error handling and strongly typed configuration/options.
- Use UTC timestamps throughout.
- Generate OpenAPI/Swagger documentation.
- Use structured logging with correlation IDs, metrics, distributed tracing, and health checks.
- Redis and RabbitMQ are infrastructure dependencies, not synchronous business‑decision makers.

### 13.10 Open Items Requiring Approval Before Implementation

- Final list and accrual rules for leave types.
- Maximum permission‑request duration default.
- Number and naming of organization‑unit hierarchy levels.
- Multi‑level approval chains vs. single‑approver default.
- Regularization time window (how long after a missed punch an employee may submit a correction).

*The implementation shall be considered complete only when Sections 1–13 are implemented, the open items in §13.10 are resolved and incorporated, and all acceptance criteria in Section 12 pass. A successful compilation alone is not completion.*

---

### 13.11 Related Specifications
- **Leave and Permission Management Module**: See [FFM_Leave_And_Permission_Management_Module_Spec.md](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/docs/FFM_Leave_And_Permission_Management_Module_Spec.md) for full ISO 29148 compliant schema, approval workflows, balance deduction rules, and API contracts.

