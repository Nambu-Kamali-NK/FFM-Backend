# Field Force Management (FFM) System — Requirement Specification Document for Field Service APIs

---

## 1. Scope & System Functionality

The **Field Operations Service** serves as the core operational dispatching and geofence governance hub for the Field Force Management (FFM) platform. It provides the capabilities required by dispatchers, supervisors, and operations managers to schedule, allocate, reassign, and monitor work orders, while maintaining authoritative geographic boundaries for customer facilities.

---

## 2. Field Visit Dispatch & Task Governance

### 2.1 Supervisor Task Assignment & Scheduling
The back-end system shall provide a centralized operational capability allowing supervisors and dispatchers to create, prioritize, describe, and schedule technical field tasks and work orders linked to verified client facilities.
* **Authorized System Roles:** `Admin`, `Manager`, `Supervisor` (Office Dispatchers & Field Leads. Blocked for `Employee`).

* **Field Visit Scheduling:** Interactive scheduling tool to plan and set mandatory start dates/times and completion deadlines, strictly enforcing that the deadline is chronologically later than the start time.
* **Priority Categorization:** Full-featured priority classification supporting `LOW`, `MEDIUM`, `HIGH`, and `URGENT` urgency levels to enable immediate emergency triage.
* **Flexible Work Order Allocation:** Capability to provision unassigned work orders (status: `CREATED`) for subsequent dispatching, or pre-assign directly to active technicians (status: `ASSIGNED`).
* **Automated Tracking Identifiers:** Systematic auto-generation of unique, sequential work order tracking numbers (`TSK-YYYYMM-XXXX`) for auditability and customer reference.
* **Client Site Binding:** Mandatory association with a verified customer premise from the active directory to ensure technicians receive validated destination telemetry.

---

### 2.2 Dynamic Task Reassignment & Workload Governance
The back-end system shall provide dynamic workload governance allowing supervisors to reallocate or transfer work orders between field technicians without disrupting customer service or altering historical task identifiers.
* **Authorized System Roles:** `Admin`, `Manager`, `Supervisor` (Office Dispatchers & Operations Managers. Blocked for `Employee`).

* **Roster & Schedule Synchronization:** Immediate reallocation of the work order to the replacement technician's daily mobile roster while releasing the calendar of the originally assigned employee.
* **In-Progress Work Safety Guard:** Administrative rule strictly prohibiting the reassignment of tasks that have entered on-site execution (`CHECKED_IN`) or terminal completion states (`COMPLETED`, `CANCELLED`) to prevent duplicate work or conflicting billing.
* **Historical Handover Audit Trails:** Comprehensive digital archiving of all task reassignment transactions, capturing the supervisory identity, handover timestamp, and operational reason.

---

### 2.3 Task Status Tracking & Kanban Monitoring
A centralized operational monitoring service allowing operations managers, dispatchers, and field technicians to track the live progress and status of all field deployments in real-time.
* **Authorized System Roles:** All Roles — `Employee` (Field Technicians view only their own daily schedule) and `Manager`, `Supervisor`, `Admin` (Team-wide live board).

* **Technician Mobile Daily Planner:** Enforced role-based data privacy on mobile interfaces ensuring field technicians access exclusively their own assigned tasks for the day, preventing unauthorized visibility into peer work.
* **Supervisory Monitoring Engine:** Multi-parameter filtering allowing operations managers to track work orders across the organization by status (`CREATED`, `ASSIGNED`, `EN_ROUTE`, `CHECKED_IN`, `COMPLETED`), priority tier, technician, and scheduled date ranges.
* **Overdue Task Identification:** Capability to immediately flag delayed, overdue, or unassigned tasks across active service zones.
* **High-Performance Data Pagination:** Standardized delivery of task records in lightweight paginated streams (default: 20 records per page) ensuring rapid loading across mobile networks.

---

## 3. Advanced Client Site & Geofencing Capabilities

### 3.1 Customer Site Registration & Boundary Provisioning
The back-end system shall serve as the authoritative registry for customer facilities, commercial premises, branch offices, and service kiosks where field visits take place.
* **Authorized System Roles:** `Admin`, `Manager` (Account Onboarding Leads. Blocked for `Employee`).

* **Geographic Coordinate Validation:** Mandatory validation of authoritative GPS coordinates (Latitude: -90.0 to +90.0, Longitude: -180.0 to +180.0), explicitly prohibiting invalid coordinates and placeholder values (0.0, 0.0).
* **Configurable Geofence Radius:** Granular control over radial geofence boundaries (from 50 meters to 5,000 meters, default: 150m) per customer location for automated on-site proximity verification.
* **Site Code Uniqueness Guarantee:** Strict enforcement of unique, case-normalized site codes (e.g., `SITE-CHN-001`) preventing duplicate client entity creation across the system.
* **Facility Contact Profiles:** Dedicated repository for complete physical street addresses, landmarks, facility managers, and direct emergency telephone contacts.
* **Automated Arrival Verification Enactment:** Authoritative storage of GPS boundaries to enable downstream mobile applications to automatically validate technician on-site presence prior to task check-in.

---

### 3.2 Client Sites Master Directory & Status Governance
A centralized, searchable facility directory allowing operations staff to browse, search, and verify customer locations during work order scheduling and territory planning.
* **Authorized System Roles:** `Admin`, `Manager`, `Supervisor`, `Employee` (All Authenticated Users).

* **Operational Status Filtering:** Default retrieval of active facilities, preventing dispatchers from mistakenly scheduling work orders to closed, decommissioned, or retired buildings.
* **Historical Facility Audit Retrieval:** Administrative capability to inspect historical and inactive customer sites for past visit audits and invoicing reconciliation.
* **Resilient Empty-State Governance:** Clean handling of new operational territories containing zero registered facilities without system degradation.

---

### 3.3 Individual Client Site Profile & Navigation Telemetry
A detailed site specification interface supplying critical customer contact information and geographic navigation telemetry for field personnel prior to departure.
* **Authorized System Roles:** `Employee` (Field Technician navigating to site), `Supervisor`, `Manager`, `Admin`.

* **Pre-Departure Site Inspection:** Comprehensive facility card detailing physical street addresses, regional landmarks, and on-site representative telephone contacts.
* **GPS Telemetry Integration:** Delivery of verified geographic coordinates to enable turn-by-turn mobile satellite navigation directly to the facility entrance.
* **Entity Integrity & Safety:** Safe, read-only consultation ensuring field personnel cannot inadvertently alter or corrupt customer facility records.

---

## 4. Requirements Summary Matrix

| Section | Requirement Title | Authorized Roles (Functional Title) | Operational Requirement |
|---|---|---|---|
| **2.1** | **Supervisor Task Assignment** | `Admin, Manager, Supervisor` *(Dispatcher / Supervisor)* | Create, prioritize, describe, and schedule technical field tasks with deadline enforcement. |
| **2.2** | **Dynamic Task Reassignment** | `Admin, Manager, Supervisor` *(Operations Manager)* | Reallocate work orders between technicians; block reassignment once on-site work begins. |
| **2.3** | **Task Status Tracking Control** | `Employee` *(Field Technician)* & `Manager, Supervisor, Admin` | Private daily schedule for technicians; real-time filtered monitoring board for supervisors. |
| **3.1** | **Customer Site Registration** | `Admin, Manager` *(Account Onboarding Lead)* | Onboard client sites with validated GPS coordinates and configurable geofence radii (50m–5000m). |
| **3.2** | **Client Sites Master Directory** | `Admin, Manager, Supervisor, Employee` *(All Roles / Dispatcher)* | Searchable directory of active customer sites; hides closed facilities by default. |
| **3.3** | **Client Site Profile & Telemetry** | `Employee, Supervisor, Manager, Admin` *(Field Technician)* | Access contact telephone and launch satellite GPS navigation to the site gate. |
