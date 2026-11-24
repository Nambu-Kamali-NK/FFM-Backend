# Frontend Technical Specification Document (Frontend SDD)

## Module: Field Operations – Field Tasks Explorer & Work Order Management
### System: Field Force Management (FFM) Platform
### Design Prototype Reference: [Muscat Municipality Management Portal](https://taste-detach-41038153.figma.site/)

| Document Attribute | Specification Detail |
| :--- | :--- |
| **Document Reference** | `FFM-FE-SPEC-TASKS-V1.0` |
| **System Version** | FFM Web Portal 2.0 (`FFM-Frontend-22`) |
| **Backend Integration** | `FFM.FieldOperations.Api` (Port 5030) – `GET /api/v1/tasks` |
| **Standard Compliance** | Zero Path Parameters Convention, Bearer Token Auth (RFC 6750), WCAG 2.1 AA |
| **Module Classification** | Tier-1 Core Operational Business Module |
| **UI Framework & Stack** | React (v19) + JavaScript (JSX) + Vite + Tailwind CSS |
| **Status** | **Approved Specification** |

---

## 1. Executive Summary & Purpose

The **Field Tasks Explorer Module** is the central operational interface where dispatch supervisors and regional administrators review, filter, and track field work orders across Muscat Municipality sites.

Following successful authentication via `FFM.Identity.Access` (Port 5000), the frontend attaches the acquired RSA-256 JWT access token to query `FFM.FieldOperations.Api` (Port 5030) via `GET /api/v1/tasks`. The module provides real-time visibility into the 140+ active field tasks stored in the PostgreSQL database, supporting status filters (`ALL`, `CREATED`, `ASSIGNED`, `EN_ROUTE`, `COMPLETED`, `CANCELLED`), priority badges, pagination, and direct action triggers.

---

## 2. Backend API Contract (`GET /api/v1/tasks`)

### 2.1 Endpoint Overview
* **URL:** `http://localhost:5030/api/v1/tasks`
* **HTTP Method:** `GET`
* **Authentication:** Required (`Authorization: Bearer <accessToken>`)
* **Service:** `FFM.FieldOperations.Api`
* **Controller:** `TasksController.cs` (Method: `GetTasks`)

### 2.2 Request Query Parameters (Zero Path Parameters)
Adhering to the project's strict **Zero Path Parameters** standard, all filtering and pagination criteria are transmitted solely via URL query strings:

| Parameter | Type | Required | Default | Description |
|---|---|:---:|:---:|---|
| `Page` | `integer` | No | `1` | 1-based page index. |
| `PageSize` | `integer` | No | `20` | Number of tasks per page (e.g. 10, 20, 50). |
| `Status` | `string` | No | `null` | Filter by status: `CREATED`, `ASSIGNED`, `EN_ROUTE`, `COMPLETED`, `CANCELLED`. |
| `Priority` | `string` | No | `null` | Filter by priority: `LOW`, `MEDIUM`, `HIGH`, `URGENT`. |
| `SearchTerm` | `string` | No | `null` | Free-text search matching Task Number, Title, or Site Name. |
| `AssignedEmployeeId`| `integer` | No | `null` | Filter tasks assigned to a specific technician. |

### 2.3 Success Response Schema (HTTP 200 OK)
```json
{
  "success": true,
  "message": "Tasks retrieved successfully.",
  "data": {
    "items": [
      {
        "id": 14,
        "taskNumber": "TSK-202610-0014",
        "title": "Hardware inspection to kgisl",
        "description": "Need to repair the hardware in kgisl computer server lab",
        "priority": "HIGH",
        "status": "ASSIGNED",
        "clientSiteId": 7,
        "clientSiteName": "Kgisl lab",
        "clientSiteCode": "SITE-198",
        "address": "kgisl, coimbatore",
        "latitude": 90.0,
        "longitude": 180.0,
        "radiusMeters": 1000,
        "assignedEmployeeId": 558,
        "scheduledStart": "2026-10-28T00:00:00.000Z",
        "scheduledEnd": "2026-10-29T00:00:00.000Z",
        "createdByUserId": 12,
        "createdAt": "2026-10-28T05:41:30.063Z",
        "updatedAt": "2026-10-28T05:41:30.063Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalCount": 147,
    "totalPages": 8,
    "hasNextPage": true,
    "hasPreviousPage": false
  },
  "error": null,
  "timestamp": "2026-11-17T04:47:32.128Z"
}
```

---

## 3. UI / UX Design Specifications (Muscat Municipality Theme)

### 3.1 Design System & Typography
* **Primary Brand Teal:** `#1C585C` (Headers, active tabs, action CTA buttons)
* **Surface Background:** `#F8FAFB` / Light Slate `#F1F5F9`
* **Card Container:** Pure White (`#FFFFFF`), `rounded-xl`, subtle border `border-[#D8E4E5]`
* **Typography:**
  * Titles & Metrics: Serif `Fraunces` (`font-display`)
  * Body, Table, & Labels: Sans `Outfit` (`font-sans`)
  * Task Numbers & Coordinates: Mono `DM Mono` (`font-mono`)

### 3.2 Visual Status & Priority Badging
| Status Code | Badge Style (Tailwind) | Visual Indicator |
|---|---|---|
| `CREATED` | `bg-slate-100 text-slate-700 border-slate-200` | ⚪ Unassigned |
| `ASSIGNED` | `bg-blue-50 text-blue-700 border-blue-200` | 🔵 Assigned |
| `EN_ROUTE` | `bg-amber-50 text-amber-700 border-amber-200` | 🟡 In Transit |
| `COMPLETED` | `bg-emerald-50 text-emerald-700 border-emerald-200` | 🟢 Completed |
| `CANCELLED` | `bg-rose-50 text-rose-700 border-rose-200` | 🔴 Cancelled |

| Priority Code | Badge Style |
|---|---|
| `LOW` | `text-slate-600 bg-slate-50` |
| `MEDIUM` | `text-blue-700 bg-blue-50` |
| `HIGH` | `text-orange-700 bg-orange-50 font-semibold` |
| `URGENT` | `text-red-700 bg-red-100 font-bold animate-pulse` |

---

## 4. Component Architecture & State Management

### 4.1 Wireframe Layout Structure
```
┌────────────────────────────────────────────────────────────────────────┐
│  [م] Muscat Municipality - Field Force Management       [User Profile] │
├────────────────────────────────────────────────────────────────────────┤
│  Field Operations > Task Explorer                                      │
│                                                                        │
│  [ All (147) ]  [ Assigned ]  [ In Progress ]  [ Completed ] [ Cancel] │
│                                                                        │
│  [ 🔍 Search task #, title, site... ]          [ + Create New Task ]   │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Task Number │ Title & Description │ Site / Location │ Priority   │  │
│  ├─────────────┼─────────────────────┼─────────────────┼────────────┤  │
│  │ TSK-0014    │ Hardware inspection │ Kgisl lab       │ [ HIGH ]   │  │
│  │ TSK-0106    │ Routine Meter Check │ HDFC Reg Office │ [ LOW ]    │  │
│  │ TSK-0102    │ AC Substation Repair│ Seeb Municipal  │ [ HIGH ]   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  Showing 1 - 20 of 147 tasks               [ Previous ] [ Page 1 ] [ Next ]
└────────────────────────────────────────────────────────────────────────┘
```

### 4.2 State Variables (`TasksList.jsx`)
```javascript
const [tasks, setTasks] = useState([]);
const [loading, setLoading] = useState(true);
const [error, setError] = useState(null);
const [page, setPage] = useState(1);
const [pageSize, setPageSize] = useState(20);
const [totalCount, setTotalCount] = useState(0);
const [statusFilter, setStatusFilter] = useState('ALL');
const [searchTerm, setSearchTerm] = useState('');
```

### 4.3 Data Fetching Hook
```javascript
useEffect(() => {
  fetchTasks();
}, [page, statusFilter]);

const fetchTasks = async () => {
  setLoading(true);
  setError(null);
  try {
    const token = localStorage.getItem('ffm_access_token');
    const params = { page, pageSize };
    if (statusFilter !== 'ALL') params.status = statusFilter;
    if (searchTerm.trim()) params.searchTerm = searchTerm.trim();

    const response = await axios.get('http://localhost:5030/api/v1/tasks', {
      params,
      headers: {
        Authorization: `Bearer ${token}`
      }
    });

    if (response.data?.success) {
      setTasks(response.data.data.items || []);
      setTotalCount(response.data.data.totalCount || 0);
    }
  } catch (err) {
    console.error('Failed to fetch tasks:', err);
    setError(err.response?.data?.message || 'Error loading field tasks.');
  } finally {
    setLoading(false);
  }
};
```

---

## 5. Error Handling & Edge Cases

1. **Token Expiry (HTTP 401 Unauthorized):**
   * If the JWT token expires, clear `localStorage` and redirect the user back to the Login screen with an alert: *"Session expired. Please log in again."*
2. **Network Timeout / Microservice Down:**
   * If `http://localhost:5030` is unreachable, render an alert banner with a **"Retry Connection"** button without breaking the UI.
3. **Empty Results State:**
   * If a filter returns 0 tasks, display a clear Muscat Municipality empty-state card: *"No field tasks found matching the selected filter."* with a **"Reset Filters"** action.

---

## 6. Verification & Test Plan

| Test ID | Scenario | Expected Outcome |
|---|---|---|
| `TC-FE-TSK-01` | Initial Mount with valid token | Fetches Page 1, displays 20 tasks, updates total count to 147. |
| `TC-FE-TSK-02` | Status Filter Click (`COMPLETED`) | Calls `GET /api/v1/tasks?status=COMPLETED`, shows only completed tasks. |
| `TC-FE-TSK-03` | Pagination (`Next` button) | Advances to Page 2, loads items 21–40. |
| `TC-FE-TSK-04` | Search query (`kgisl`) | Calls `GET /api/v1/tasks?searchTerm=kgisl`, displays `TSK-202610-0014`. |
| `TC-FE-TSK-05` | Missing/Invalid Token | Gracefully prompts to re-authenticate via `/login`. |

---
*Document approved for implementation under FFM Web Portal Sprint 2.*
