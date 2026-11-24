# Get Rosters Module Specification

## 1. Document Metadata

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-WRK-GET-ROSTERS-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Service** | Workforce Management Service (`FFM.Workforce`) |
| **Module** | Module 3: Workforce Scheduling & Shifts Engine — Feature: Get Rosters |
| **Default Port** | `5010` |
| **Method & Route** | `GET /api/v1/Rosters` |
| **Compliance** | ISO/IEC/IEEE 29148, OpenAPI 3.1, RFC 7807 (ProblemDetails), Zero Path Parameters |
| **Authentication** | Bearer JWT RS256 (issued by Identity & Access Service) |
| **Classification** | Tier-1 Core Workforce Operational Calendar & Roster Engine |
| **Status** | Approved Formal Specification |
| **Parent Document** | `FFM_Workforce_Service_Spec.md`, Section 5.1 & Section 8.3 |

---

## 2. Objective & Functional Scope

The **Get Rosters** module delivers a centralized query endpoint that retrieves operational duty rosters, shift schedules, assigned zones, and technician allocations over a specified date range.

### Key Capabilities:
1. **Weekly/Monthly Calendar Delivery:** Powers the web portal's interactive 7-day or 30-day scheduling grid and the mobile technician's schedule views.
2. **Technician Personal Schedule:** Allows field technicians to view their daily assigned shifts, report times, rest days, and operational work zones.
3. **Supervisor/Manager Roster Review:** Enables field supervisors to inspect team coverage across municipal zones (e.g. North Zone, Central Ward) to detect unstaffed shifts.
4. **Automated Date Window Resolution:** When date parameters are omitted, automatically defaults to the current active 7-day weekly window (Monday through Sunday).
5. **Zero Path Parameters Standard:** All filtering (`employeeId`, `from`, `to`, `zone`) is delivered exclusively via HTTP query parameters.
6. **Role-Based Scope Enforcement:** Field technicians are securely restricted to viewing only their personal schedules, while managerial roles have full team/municipal visibility.

---

## 3. Authorization & RBAC Scoping Policy

| Role | Allowed? | Scoping & Enforcement Policy |
| :--- | :---: | :--- |
| `SYSTEM_ADMIN` | ✅ Yes | Global authority to view rosters across all employees, zones, and municipal departments. |
| `ADMIN` | ✅ Yes | Authority to view all municipal rosters for scheduling audits and workforce distribution. |
| `MANAGER` | ✅ Yes | Authority to view rosters for team members, department zones, or specific technicians. |
| `SUPERVISOR` | ✅ Yes | Authority to view crew rosters to manage daily on-ground shifts and dispatch. |
| `EMPLOYEE` | ✅ Yes | **Self-Scoping Guard:** Restricted to viewing only their own assigned rosters. If `employeeId` is omitted, it defaults to caller. If an employee attempts to query another technician's `employeeId`, the system automatically overrides or rejects with `403 Forbidden`. |

* **Unauthenticated Requests:** Returns `HTTP 401 Unauthorized`.
* **Token Validation:** Verifies Bearer JWT signature, issuer, and non-expired lifetime.

---

## 4. Business Rules & Validation (BR)

| Rule ID | Rule Statement | Enforcement Error |
| :--- | :--- | :--- |
| **BR-RST-01** | **Chronological Order:** `to` date must be equal to or greater than `from` date. | `400 Bad Request` ("'to' date must be greater than or equal to 'from' date.") |
| **BR-RST-02** | **Maximum Query Range:** The date span between `from` and `to` cannot exceed 31 days to avoid database load. | `400 Bad Request` ("Date range cannot exceed 31 days.") |
| **BR-RST-03** | **Default Window:** If `from` is omitted, it defaults to Monday of current week. If `to` is omitted, it defaults to `from` + 6 days. | Handled automatically by query resolver. |
| **BR-RST-04** | **Employee Isolation:** Users with role `EMPLOYEE` can only retrieve their own roster records. | System overrides `employeeId` with caller's employee profile. |
| **BR-RST-05** | **Cancelled Exclusions:** Cancelled rosters (`Status = 'CANCELLED'`) are excluded by default unless explicitly requested. | Query filter condition. |

---

## 5. Domain Model & Database Schema

### PostgreSQL Table: `RosterAssignments`
```sql
CREATE TABLE IF NOT EXISTS "RosterAssignments" (
    "Id" SERIAL PRIMARY KEY,
    "EmployeeId" INT NOT NULL REFERENCES "Employees"("Id") ON DELETE RESTRICT,
    "ShiftId" INT NOT NULL REFERENCES "Shifts"("Id") ON DELETE RESTRICT,
    "RosterDate" DATE NOT NULL,
    "Zone" VARCHAR(100) NULL,
    "Notes" VARCHAR(500) NULL,
    "Status" VARCHAR(50) NOT NULL DEFAULT 'ASSIGNED',
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedBy" VARCHAR(100) NOT NULL DEFAULT 'SYSTEM'
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RosterAssignments_Employee_RosterDate" 
ON "RosterAssignments" ("EmployeeId", "RosterDate")
WHERE "Status" != 'CANCELLED';
```

---

## 6. API Contract & Interface Specification

### 6.1 Request Details
* **HTTP Method:** `GET`
* **Route:** `/api/v1/Rosters`
* **Headers:**
  - `Authorization: Bearer <JWT_TOKEN>` (Mandatory)
  - `Accept: application/json`

### 6.2 Query Parameters (Zero Path Parameters Standard)

| Parameter | Type | Required? | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `from` | `date` | Optional | Current Week Monday | Start date of query range (e.g. `2026-11-23`). |
| `to` | `date` | Optional | `from` + 6 days | End date of query range (e.g. `2026-11-29`). |
| `employeeId` | `int?` | Optional | Caller ID (for EMPLOYEE) | Target employee ID to filter. |
| `zone` | `string?` | Optional | `null` | Operational zone filter (e.g. `North-Zone`). |

---

### 6.3 Response Contracts

#### Success Response (`HTTP 200 OK`)
```json
{
  "success": true,
  "message": "Rosters retrieved successfully.",
  "data": [
    {
      "id": 42,
      "employeeId": 10,
      "employeeCode": "EMP010",
      "employeeName": "Nambu Kamali",
      "shiftId": 1,
      "shiftCode": "MCT-MORN",
      "shiftName": "Morning Field Shift",
      "startTime": "07:00:00",
      "endTime": "15:00:00",
      "colorCode": "#1C585C",
      "rosterDate": "2026-11-25T00:00:00Z",
      "zone": "North-Zone",
      "notes": "Valve inspection and maintenance",
      "status": "ASSIGNED",
      "createdAt": "2026-11-23T05:26:00Z",
      "createdBy": "USER_12 (SYSTEM_ADMIN)"
    }
  ],
  "error": null,
  "timestamp": "2026-11-23T05:46:47.791Z"
}
```

#### Validation Error (`HTTP 400 Bad Request`)
```json
{
  "success": false,
  "message": "Validation failed",
  "data": null,
  "error": {
    "code": "VALIDATION_ERROR",
    "details": "ToDate must be greater than or equal to FromDate."
  },
  "timestamp": "2026-11-23T05:46:47.791Z"
}
```

#### Unauthorized (`HTTP 401 Unauthorized`)
* Missing or expired Bearer JWT token.

#### Forbidden (`HTTP 403 Forbidden`)
* Employee attempting to query other employees' records without managerial permissions.

---

## 7. Test Verification Scenarios

| Test ID | Scenario | Expected Outcome |
| :--- | :--- | :--- |
| **TC-ROST-01** | Query with empty parameters | Resolves current week (7 days) and returns `200 OK`. |
| **TC-ROST-02** | Filter by `employeeId` as Admin | Returns only rosters belonging to that technician (`200 OK`). |
| **TC-ROST-03** | Filter by `zone` | Returns only rosters assigned to the specified zone. |
| **TC-ROST-04** | Invalid range (`from > to`) | Fails with `400 Bad Request` (Chronological rule). |
| **TC-ROST-05** | Excessive range (> 31 days) | Fails with `400 Bad Request` (Max window rule). |
| **TC-ROST-06** | Self-scoping for `EMPLOYEE` | If Employee requests without `employeeId`, automatically filters to caller. |
