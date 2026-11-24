# Frontend Technical Specification Document (Frontend SDD)

## Module: Workforce – Roster & Shifts Management (`RosterSchedule.jsx`)
### System: Field Force Management (FFM) Platform
### Design Prototype Reference: [Muscat Municipality Management Portal](https://taste-detach-41038153.figma.site/)

| Document Attribute | Specification Detail |
| :--- | :--- |
| **Document Reference** | `FFM-FE-SPEC-ROSTER-V1.0` |
| **System Version** | FFM Web Portal 2.0 (`FFM-Frontend-22`) |
| **Backend Integration** | `FFM.Workforce.Api` (Port 5010) – `GET /api/v1/Shifts`, `POST /api/v1/Rosters/assign`, `GET /api/v1/employees` |
| **Standard Compliance** | Zero Path Parameters Convention, Bearer Token Auth (RFC 6750), WCAG 2.1 AA |
| **Module Classification** | Tier-1 Core Workforce Scheduling & Roster Engine |
| **UI Framework & Stack** | React (v19) + JavaScript (JSX) + Vite + Tailwind CSS + Lucide Icons |
| **Mount Location** | [PortalLayout.jsx](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Frontend-22/src/pages/PortalLayout.jsx) under `activeTab === 'roster'` |
| **Status** | **Approved Specification** |

---

## 1. Executive Summary & Purpose

The **Roster & Shifts Management Module** (`RosterSchedule.jsx`) provides municipal supervisors, dispatchers, and administrators with an interactive weekly calendar grid to manage, assign, and visualize field workforce schedules.

Following authentication via `FFM.Identity.Access` (Port 5000), the frontend attaches the Bearer JWT token to communicate with `FFM.Workforce.Api` (Port 5010):
1. **Fetches Shift Blueprints (`GET /api/v1/Shifts`):** Renders the official municipal shift palette with color-coded badges, timings, and grace periods.
2. **Loads Active Technicians (`GET /api/v1/employees`):** Populates the technician rows and shift assignment selector.
3. **Assigns Daily Shifts (`POST /api/v1/Rosters/assign`):** Provides a modal dialog allowing supervisors to assign a technician to a shift, date, and zone with real-time collision detection (handling `409 Conflict` gracefully).

---

## 2. Architecture & Backend Integrations

```
                     [FFM Web Portal (Port 5173)]
                                  │
                       [RosterSchedule.jsx]
                                  │
           ┌──────────────────────┼──────────────────────┐
           ▼                      ▼                      ▼
  GET /api/v1/Shifts    GET /api/v1/employees   POST /api/v1/Rosters/assign
   (Port 5010)            (Port 5010)              (Port 5010)
           │                      │                      │
           └──────────────────────┴──────────────────────┘
                                  ▼
                     [WorkforceDbContext / PostgreSQL]
```

### 2.1 Backend Endpoints Summary

| Endpoint | Method | Service Port | Purpose |
| :--- | :---: | :---: | :--- |
| `/api/v1/Shifts` | `GET` | `5010` | Retrieves master shift templates (`MCT-MORN`, `MCT-EVEN`, `MCT-NGHT`, `MCT-GEN`, `MCT-OFF`). |
| `/api/v1/Rosters/assign` | `POST` | `5010` | Assigns an employee to a shift, date, and operational zone. |
| `/api/v1/employees?status=ACTIVE` | `GET` | `5010` | Retrieves active technicians for row rendering and assignment dropdown. |

---

## 3. UI/UX Layout & Component Specifications

The interface adheres to the **Muscat Municipality Portal** design language (Dark Cyan `#1C585C`, Emerald Green, Amber Warning `#D97706`, and Clean Card Surfaces `#F3F7F7`).

### 3.1 Component Hierarchy
```text
RosterSchedule.jsx
├── RosterHeader (Title, Week Navigator, Quick Stats, "Assign Shift" Button)
├── ShiftPaletteBar (5 Color-Coded Shift Legend Badges with hours & grace period)
├── RosterFilterBar (Search by technician, Zone Filter Dropdown, Shift Filter)
├── WeeklyCalendarGrid
│   ├── GridHeader (7 Date Columns: Mon, Tue, Wed, Thu, Fri, Sat, Sun with Today highlight)
│   └── TechnicianRow (Repeated per employee: Name, Code, Role, Zone chip, 7 Daily Shift cells)
│       └── ShiftCardCell (Color-coded shift chip with click-to-edit or empty cell click-to-assign)
└── AssignShiftModal (Dialog for employee selection, date picker, shift card selection, zone, notes)
```

---

### 3.2 Shift Palette Legend (Master Seeds)

The palette dynamically reads from `GET /api/v1/Shifts` and renders badges:

| Shift Code | Shift Name | Timings | Grace | Hex Color | UI Display |
| :--- | :--- | :---: | :---: | :---: | :--- |
| `MCT-MORN` | Morning Field Shift | 07:00 – 15:00 | 15 min | `#1C585C` | Teal Pill with Sun icon |
| `MCT-EVEN` | Evening Field Shift | 15:00 – 23:00 | 15 min | `#D97706` | Amber Pill with Sunset icon |
| `MCT-NGHT` | Night Emergency Shift | 23:00 – 07:00 | 15 min | `#4338CA` | Indigo Pill with Moon icon |
| `MCT-GEN` | General Directorate | 08:00 – 17:00 | 15 min | `#059669` | Emerald Pill with Briefcase icon |
| `MCT-OFF` | Scheduled Rest Day | 00:00 – 00:00 | 0 min | `#64748B` | Slate Grey Pill with Coffee icon |

---

### 3.3 Weekly Calendar Grid Specification

* **Week Navigation:**
  * Displays active week banner (e.g. `Nov 23, 2026 – Nov 29, 2026`).
  * `[< Previous Week]` `[Today]` `[Next Week >]` navigation controls.
  * Today's column features an elevated visual highlight (e.g. Soft Teal border / accent pill).
* **Grid Columns:**
  * **Column 1:** Technician Profile (Avatar, Full Name, Employee Code, Assigned Org/Zone).
  * **Columns 2–8:** Day cells (Monday through Sunday).
* **Cell Interactions:**
  * **Assigned Shift:** Renders the shift code pill with start/end time. Hovering reveals tooltip with operational zone, notes, and creator.
  * **Empty Cell:** Renders a dashed subtle `+` button on hover, allowing a supervisor to click directly on that technician/day to open the Assign Modal with prefilled values!

---

### 3.4 "Assign Shift" Modal Specification

* **Trigger:** Click "+ Assign Shift" button in header OR click any empty grid cell.
* **Modal Fields:**
  1. **Technician Selector:** Searchable dropdown listing active employees with employee code.
  2. **Roster Date:** Date picker (pre-filled if clicked from grid).
  3. **Shift Template Selector:** Visual radio cards showing the 5 municipal shifts with their colored badges.
  4. **Operational Zone:** Input/dropdown (e.g. `North-Zone`, `Central Ward`, `Seeb Sector`, `Muttrah`).
  5. **Operational Notes:** Optional text area for special instructions (max 500 characters).
* **Feedback & Validation Handling:**
  * **409 Conflict:** Shows error toast: *"This technician is already assigned to a shift on this date."*
  * **400 Leave Collision:** Shows error toast: *"Cannot assign roster: technician has an approved or pending leave on this date."*
  * **200 OK:** Closes modal, displays green success notification, and immediately refreshes the calendar grid state.

---

## 4. State Management & Lifecycle

```javascript
// Core States in RosterSchedule.jsx:
const [shifts, setShifts] = useState([]);               // Array of shift blueprints from GET /api/v1/Shifts
const [employees, setEmployees] = useState([]);         // Active employees from GET /api/v1/employees
const [rosterAssignments, setRosters] = useState([]);   // List of assignments
const [currentWeekStart, setCurrentWeekStart] = useState(getMonday(new Date()));
const [searchTerm, setSearchTerm] = useState('');
const [selectedZone, setSelectedZone] = useState('ALL');
const [isAssignModalOpen, setIsAssignModalOpen] = useState(false);
const [modalInitialData, setModalInitialData] = useState(null);
const [loading, setLoading] = useState(true);
```

---

## 5. Error Handling & Edge Cases

1. **Token Expiry / 401:** Prompts user to re-login or automatically redirects via `onLogout()`.
2. **Network Disconnection / Port 5010 Offline:** Displays retry banner: *"Unable to reach Workforce Service on Port 5010. Retrying in 5s..."*.
3. **Empty Week:** If no rosters are assigned for a given week, the grid cleanly displays empty cells with clickable `+` prompts rather than a blank page.
4. **Timezone Normalization:** All dates are formatted with ISO-8601 strings and zeroed out at `00:00:00Z` to prevent local date slippage across timezones.
