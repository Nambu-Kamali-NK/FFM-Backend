# Get Shifts Module Specification

## 1. Document Metadata

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-WRK-GET-SHIFTS-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Service** | Workforce Management Service (`FFM.Workforce`) |
| **Module** | Module 3: Workforce Scheduling & Shifts Engine — Feature: Get Shifts |
| **Default Port** | `5010` |
| **Method & Route** | `GET /api/v1/Shifts` |
| **Compliance** | ISO/IEC/IEEE 29148, OpenAPI 3.1, RFC 7807 (ProblemDetails), Zero Path Parameters |
| **Authentication** | Bearer JWT RS256 (issued by Identity & Access Service) |
| **Classification** | Tier-1 Core Workforce Master Data & Operational Blueprint |
| **Status** | Approved Formal Specification |
| **Parent Document** | `FFM_Workforce_Service_Spec.md`, Section 5.1 |

---

## 2. Objective & Functional Scope

The **Get Shifts** module provides a centralized, high-performance query endpoint that retrieves all configured municipal shifts, operational time windows, grace periods, and scheduling metadata.

### Key Capabilities:
1. **Master Shift Retrieval:** Fetches standard municipal work shifts (Morning, Evening, Night Emergency, General Directorate, and Rest Day).
2. **Operational Time Bounds:** Delivers start and end times, overnight/night shift indicators, and grace period thresholds used by the Attendance and Check-in verification engines.
3. **UI Scheduling Palette:** Provides color codes and visual indicators to power the web portal's weekly Roster grid and the mobile technician's schedule views.
4. **Caching & Low-Latency Delivery:** Since shift definitions are enterprise master data that change infrequently, responses are optimized for in-memory and HTTP caching (HTTP 304 / ResponseCache).
5. **Zero Path Parameters Standard:** All query filters (e.g. `isActive`, `includeRestDays`) are delivered exclusively through query parameters.

---

## 3. Authorization & RBAC Policy

| Role | Allowed? | Enforcement Rationale |
| :--- | :---: | :--- |
| `SYSTEM_ADMIN` | ✅ Yes | Global authority to view and audit all shift definitions across tenants. |
| `ADMIN` | ✅ Yes | Full authority to view shifts for municipal resource allocation. |
| `MANAGER` | ✅ Yes | Authority to view shifts for zone and team scheduling. |
| `SUPERVISOR` | ✅ Yes | Authority to view shifts to assign daily rosters to field crews. |
| `EMPLOYEE` | ✅ Yes | Field technicians can view shift schedules to verify their working hours. |

* **Unauthenticated Requests:** Returns `HTTP 401 Unauthorized`.
* **Token Validation:** Verifies Bearer JWT signature, issuer, and non-expired lifetime.

---

## 4. Domain Model & Database Schema

### 4.1 PostgreSQL Table: `Shifts`

```sql
CREATE TABLE IF NOT EXISTS "Shifts" (
    "Id" SERIAL PRIMARY KEY,
    "ShiftCode" VARCHAR(50) NOT NULL UNIQUE,
    "Name" VARCHAR(100) NOT NULL,
    "Description" VARCHAR(255) NULL,
    "StartTime" TIME NOT NULL,
    "EndTime" TIME NOT NULL,
    "GracePeriodMinutes" INT NOT NULL DEFAULT 15,
    "IsNightShift" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsRestDay" BOOLEAN NOT NULL DEFAULT FALSE,
    "ColorCode" VARCHAR(20) NOT NULL DEFAULT '#1C585C',
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "CreatedBy" VARCHAR(100) NOT NULL DEFAULT 'SYSTEM'
);
```

### 4.2 Seed Data Matrix

| Id | ShiftCode | Name | StartTime | EndTime | Grace | IsNightShift | IsRestDay | ColorCode |
| :---: | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **1** | `MCT-MORN` | Morning Field Shift | `07:00:00` | `15:00:00` | 15 | `false` | `false` | `#1C585C` |
| **2** | `MCT-EVEN` | Evening Field Shift | `15:00:00` | `23:00:00` | 15 | `false` | `false` | `#D97706` |
| **3** | `MCT-NGHT` | Night Emergency Shift | `23:00:00` | `07:00:00` | 15 | `true` | `false` | `#4338CA` |
| **4** | `MCT-GEN` | General Directorate Hours | `08:00:00` | `17:00:00` | 15 | `false` | `false` | `#059669` |
| **5** | `MCT-OFF` | Scheduled Rest Day / Off | `00:00:00` | `00:00:00` | 0 | `false` | `true` | `#64748B` |

---

## 5. API Contract & Interface Specification

### 5.1 Request Details
* **HTTP Method:** `GET`
* **Route:** `/api/v1/Shifts`
* **Headers:**
  - `Authorization: Bearer <JWT_TOKEN>` (Mandatory)
  - `Accept: application/json`

### 5.2 Optional Query Parameters
| Parameter | Type | Default | Description |
| :--- | :---: | :---: | :--- |
| `isActive` | `boolean?` | `true` | When true, returns only active shifts. When null/omitted, returns active shifts. |
| `includeRestDays` | `boolean?` | `true` | Whether to include the `MCT-OFF` (Rest Day) blueprint in the response. |

---

### 5.3 Response Details

#### Success Response (`HTTP 200 OK`)
```json
{
  "success": true,
  "message": "Shifts retrieved successfully.",
  "data": [
    {
      "id": 1,
      "shiftCode": "MCT-MORN",
      "name": "Morning Field Shift",
      "description": "Standard daytime municipal maintenance & inspection",
      "startTime": "07:00:00",
      "endTime": "15:00:00",
      "durationHours": 8.0,
      "gracePeriodMinutes": 15,
      "isNightShift": false,
      "isRestDay": false,
      "colorCode": "#1C585C",
      "isActive": true
    },
    {
      "id": 2,
      "shiftCode": "MCT-EVEN",
      "name": "Evening Field Shift",
      "description": "Peak municipal operations & road repairs",
      "startTime": "15:00:00",
      "endTime": "23:00:00",
      "durationHours": 8.0,
      "gracePeriodMinutes": 15,
      "isNightShift": false,
      "isRestDay": false,
      "colorCode": "#D97706",
      "isActive": true
    },
    {
      "id": 3,
      "shiftCode": "MCT-NGHT",
      "name": "Night Emergency Shift",
      "description": "Water pipe bursts, drainage & urgent hazards",
      "startTime": "23:00:00",
      "endTime": "07:00:00",
      "durationHours": 8.0,
      "gracePeriodMinutes": 15,
      "isNightShift": true,
      "isRestDay": false,
      "colorCode": "#4338CA",
      "isActive": true
    },
    {
      "id": 4,
      "shiftCode": "MCT-GEN",
      "name": "General Directorate Hours",
      "description": "Office & administrative field leads",
      "startTime": "08:00:00",
      "endTime": "17:00:00",
      "durationHours": 9.0,
      "gracePeriodMinutes": 15,
      "isNightShift": false,
      "isRestDay": false,
      "colorCode": "#059669",
      "isActive": true
    },
    {
      "id": 5,
      "shiftCode": "MCT-OFF",
      "name": "Scheduled Rest Day / Off",
      "description": "Official weekly off / compensatory rest",
      "startTime": "00:00:00",
      "endTime": "00:00:00",
      "durationHours": 0.0,
      "gracePeriodMinutes": 0,
      "isNightShift": false,
      "isRestDay": true,
      "colorCode": "#64748B",
      "isActive": true
    }
  ],
  "error": null,
  "timestamp": "2026-11-23T10:30:00.000Z"
}
```

#### Error Responses

* **`401 Unauthorized`:**
  ```json
  {
    "success": false,
    "message": "Unauthorized access. Bearer token missing or invalid.",
    "data": null,
    "error": {
      "code": "UNAUTHORIZED",
      "details": "JWT token validation failed."
    },
    "timestamp": "2026-11-23T10:30:00.000Z"
  }
  ```

---

## 6. Clean Architecture Implementation Plan

```
[ Client (Portal / Mobile) ]
             │
             ▼  GET /api/v1/Shifts
┌────────────────────────────────────────────────────────┐
│  FFM.Workforce.Api                                     │
│  └── ShiftsController.cs                               │
└────────────────────────────────────────────────────────┘
             │  _mediator.Send(new GetAllShiftsQuery())
             ▼
┌────────────────────────────────────────────────────────┐
│  FFM.Workforce.Application                             │
│  ├── Queries/GetAllShiftsQuery.cs                      │
│  ├── Handlers/GetAllShiftsQueryHandler.cs              │
│  └── DTOs/ShiftDto.cs                                  │
└────────────────────────────────────────────────────────┘
             │  _dbContext.Shifts.AsNoTracking().ToListAsync()
             ▼
┌────────────────────────────────────────────────────────┐
│  FFM.Workforce.Infrastructure                          │
│  └── WorkforceDbContext.cs (DbSet<Shift> Shifts)       │
└────────────────────────────────────────────────────────┘
             │
             ▼
┌────────────────────────────────────────────────────────┐
│  FFM.Workforce.Domain                                  │
│  └── Entities/Shift.cs                                 │
└────────────────────────────────────────────────────────┘
```

---

## 7. Verification & Automated Test Scenarios

| Test ID | Category | Scenario | Expected Result |
| :--- | :--- | :--- | :--- |
| **TC-SHF-01** | Query | Standard `GET /api/v1/Shifts` with valid JWT | Returns `HTTP 200 OK` with 5 shifts ordered by Id |
| **TC-SHF-02** | Filter | `GET /api/v1/Shifts?includeRestDays=false` | Returns only operational shifts (excludes `MCT-OFF`) |
| **TC-SHF-03** | Auth | `GET /api/v1/Shifts` without Bearer token | Returns `HTTP 401 Unauthorized` |
| **TC-SHF-04** | Integrity | Verify `durationHours` calculation | Returns `8.0` for 07:00–15:00 and accounts for midnight crossover |
| **TC-SHF-05** | Performance | In-memory query latency | Response executes under `< 15ms` |
