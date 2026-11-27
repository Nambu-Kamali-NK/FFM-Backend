# FFM Activity Log & Real-Time Audit Module Specification

## 1. Document Metadata

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-CORE-ACTIVITY-LOG-V1.0` |
| **System** | Field Force Management (FFM) Platform / Muscat Municipality |
| **Services Involved** | `FFM.Workforce` (Port 5010), `FFM.Notification` (Port 5040), `FFM-Frontend-22` |
| **Reference Architecture** | Muscat Municipality SmartWorkforce (`MM 1/MM/MM-SmartWorkforce`) |
| **Module** | Enterprise Activity Logging & Real-Time Audit Trail |
| **Endpoints** | `GET /api/v1/activity-log`, `GET /api/v1/my/activity`, `POST /api/v1/activity-log` |
| **WebSocket Hub** | `activity` event broadcast on `/hubs/tracking` (Port 5020) & `/hubs/notifications` |
| **Classification** | Tier-1 Governance, Security, & Operational Observability |
| **Status** | Approved Formal Specification |

---

## 2. Objective & Functional Scope

The **Activity Log Module** provides a unified, tamper-evident audit trail capturing every operational event across the municipal workforce in real time. It serves two distinct roles:

1. **System & Operational Audit (Administrators / Managers):**
   - Full visibility across every field technician, department, and zone.
   - Captures lifecycle events: attendance punches, GPS geofence breaches, AI regularizations, shift publishing, work order state changes, and security/device auth events.
2. **Personal Activity Stream (Field Technicians & Employees):**
   - Filtered view showing events concerning only the authenticated employee (`GET /api/v1/my/activity`).
3. **Real-Time Live Activity Streaming:**
   - Active WebSocket / SignalR push on `activity` channel so the portal updates instantaneously without polling.

---

## 3. Data Schema & Domain Model

### 3.1 Entity: `ActivityLogEntry`

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `id` | `UUID` / `text` | No | Primary key, uniquely identifies the event. |
| `type` | `varchar(30)` | No | Category: `attendance`, `task`, `device`, `employee`, `fraud`, `auth`, `api`, `error`. |
| `message` | `varchar(500)` | No | Human-readable audit narrative (Bilingual English & Arabic). |
| `status` | `varchar(20)` | No | Severity: `ok` (Green), `info` (Sky), `warn` (Amber), `danger` (Red). |
| `employee_id` | `integer` / `UUID` | Yes | Target employee involved in the event. |
| `employee_name` | `varchar(150)` | Yes | Denormalized employee name for fast rendering. |
| `occurred_at_utc`| `timestamp with time zone` | No | ISO 8601 UTC timestamp of occurrence. |

---

## 4. REST API Contracts

### 4.1 Get Full Audit Trail (Administrator / Manager)
* **Route:** `GET /api/v1/activity-log`
* **Query Parameters:**
  * `type` (optional): Filter by event type (`attendance`, `task`, `device`, etc.)
  * `status` (optional): Filter by severity (`ok`, `info`, `warn`, `danger`)
  * `pageNumber`: integer (default: `1`)
  * `pageSize`: integer (default: `20`)
* **Response (HTTP 200 OK):**
  ```json
  {
    "success": true,
    "data": {
      "items": [
        {
          "id": "e8a93c72-4d2b-42f1-bc84-9111de82e11a",
          "type": "attendance",
          "message": "Salim Al-Harthy checkout regularized to 15:00 based on GPS departure telemetry.",
          "status": "ok",
          "employeeId": 3,
          "employeeName": "Salim Al-Harthy",
          "occurredAtUtc": "2026-11-27T07:23:01.285Z"
        }
      ],
      "pageNumber": 1,
      "pageSize": 20,
      "totalItems": 142
    }
  }
  ```

### 4.2 Get My Activity (Technician Slice)
* **Route:** `GET /api/v1/my/activity`
* **Query Parameters:** `pageNumber`, `pageSize`
* **Response:** Returns only entries matching authenticated user's `employeeId`.

---

## 5. Status & Severity Mapping

| Status | Color Token | Background Token | Usage / Semantic Meaning |
| :--- | :--- | :--- | :--- |
| **`ok`** | `#15803D` (Emerald) | `#ECFDF5` | Normal successful events (Punch on time, task completed, verified check-in). |
| **`info`** | `#0369A1` (Sky) | `#E0F2FE` | Informational events (Roster published, report generated, shift assigned). |
| **`warn`** | `#C8892A` (Royal Gold) | `#FDF3E3` | Minor exceptions (Late check-in, missing punch anomaly detected, reschedule). |
| **`danger`** | `#DC2626` (Red) | `#FEF2F2` | Critical alerts (Geofence breach, device unbinding violation, failed login). |

---

## 6. Frontend UI Specification

Matching `MM 1/MM/MM-SmartWorkforce/src/app/features/activity-log`:
1. **Live Connection Indicator:** Pulsing green dot with `Live` status when SignalR is connected.
2. **Category Filter Tabs:**
   - *All Activity*, *Attendance & Punches*, *Field Tasks*, *Security & Geofence*, *Devices & Auth*, *System Governance*.
3. **Severity Badges:** Unified pill badges (`OK`, `Info`, `Warning`, `Alert`).
4. **Search & Date Filters:** Quick search bar and timestamp format (`dd-MMM HH:mm`).
5. **Dashboard Live Feed Card:** 6 most recent events rendered directly on the municipal command dashboard.
