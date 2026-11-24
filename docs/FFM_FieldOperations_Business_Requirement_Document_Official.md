# Business Requirement Document (BRD) — Field Operations Service

## Document Control & Overview

| Attribute | Specification Detail |
| :--- | :--- |
| **Project** | Field Force Management (FFM) Platform |
| **Module Group** | Field Operations & Geofenced Visit Governance |
| **Document Type** | Business & Functional Requirement Document (BRD) |
| **Target Audience** | Product Owners, Operations Managers, Tech Leads, QA Engineers, Business Stakeholders |
| **Standard** | ISO/IEC/IEEE 29148 Standard for Requirements Engineering |

---

## 1. Requirement: REQ-FLD-01 — Task Creation & Scheduling

* **Requirement Objective:**  
  Enable dispatchers and supervisors to create, configure, prioritize, and schedule field tasks/work orders linked to verified customer locations.

* **User Story:**  
  *As a Dispatcher/Supervisor, I need to create a structured work order with a clear title, customer site location, priority level, and time window, so that technicians are dispatched efficiently to the correct customer location.*

### Functional Requirements:
* **FR-01.1 (Work Order Definition):** The system shall require a task title (between 5 and 200 characters) and an optional detailed description of the work to be performed.
* **FR-01.2 (Client Site Association):** The system shall require linking the task to an existing, active client site from the master directory.
* **FR-01.3 (Priority Management):** The system shall support four standardized priority levels: `LOW`, `MEDIUM`, `HIGH`, and `URGENT` (defaulting to `MEDIUM`).
* **FR-01.4 (Schedule Window Enactment):** The system shall require both a scheduled start date/time and a scheduled end date/time, strictly enforcing that the end time must be after the start time.
* **FR-01.5 (Flexible Assignment):** The system shall permit creating an unassigned work order (Status: `CREATED`) or directly assigning it to a technician (Status: `ASSIGNED`).
* **FR-01.6 (Automatic Tracking Code):** The system shall automatically generate a unique, sequential tracking code (format: `TSK-YYYYMM-XXXX`) for every created task.

### Business Input Requirements:
| Field Name | Mandatory? | Business Description |
| :--- | :---: | :--- |
| **Task Title** | Yes | Brief headline of the field work (e.g., 'Quarterly AC Maintenance'). |
| **Description** | No | Detailed technical instructions, tools required, or customer remarks. |
| **Client Site** | Yes | Selection of target customer location from the verified directory. |
| **Priority** | No (Def: MEDIUM) | Urgency tier: LOW, MEDIUM, HIGH, or URGENT. |
| **Assigned Technician** | No | Optional pre-assignment to an eligible field employee. |
| **Scheduled Start Time** | Yes | Date and time when technician is expected to commence the visit. |
| **Scheduled End Time** | Yes | Expected deadline for visit completion (must be >= Start Time). |

---

## 2. Requirement: REQ-FLD-02 — Task Assignment & Reassignment

* **Requirement Objective:**  
  Provide dispatchers and managers with the capability to dynamically reallocate or transfer work orders between field technicians.

* **User Story:**  
  *As an Operations Manager, I need to reassign a pending task to another available technician when the originally assigned technician is delayed, ill, or re-routed, so that customer SLA deadlines are not breached.*

### Functional Requirements:
* **FR-02.1 (Technician Transfer):** The system shall allow authorized supervisors to transfer an existing task to any active, eligible technician.
* **FR-02.2 (Roster Synchronization):** Upon reassignment, the task shall automatically transition to `ASSIGNED`, appearing on the new technician's schedule and releasing the previous technician's calendar.
* **FR-02.3 (Active Execution Guard):** The system shall strictly block reassignment if the task has already entered execution (`CHECKED_IN`) or terminal states (`COMPLETED`, `CANCELLED`) to prevent duplicate work.
* **FR-02.4 (Audit Traceability):** The system shall record an auditable timestamp and the identity of the supervisor who initiated the reassignment.

### Business Input Requirements:
| Field Name | Mandatory? | Business Description |
| :--- | :---: | :--- |
| **Task Identifier** | Yes | The unique identifier of the existing work order to reassign. |
| **New Technician** | Yes | Selection of the replacement active field technician. |

---

## 3. Requirement: REQ-FLD-03 — Work Order Listing & Filtering

* **Requirement Objective:**  
  Supply real-time, filtered work order visibility for executive dashboards, supervisor dispatch boards, and field technician mobile daily planners.

* **User Story:**  
  *As a Field Technician, I need to see only my assigned daily tasks in sequence. As a Manager, I need to filter team-wide tasks by status, priority, and date range to identify overdue or unassigned work.*

### Functional Requirements:
* **FR-03.1 (Role-Based Data Privacy):** The system shall enforce horizontal data isolation: field technicians shall strictly access only tasks assigned to themselves, preventing unauthorized viewing of peer work.
* **FR-03.2 (Supervisory Oversight):** Managers, supervisors, and administrators shall have permission to view all work orders across their assigned teams, zones, or entire organization.
* **FR-03.3 (Multi-Criteria Filtering):** The system shall provide dynamic filtering capabilities by Employee, Status (`CREATED`, `ASSIGNED`, `EN_ROUTE`, `CHECKED_IN`, `COMPLETED`), Priority (`LOW`, `MEDIUM`, `HIGH`, `URGENT`), Client Site, and Scheduled Date Ranges.
* **FR-03.4 (Pagination Standards):** The system shall deliver task records in paginated batches (default: 20 per page, capped at 100) to ensure rapid loading on mobile networks.

### Business Filter Parameters:
| Filter Parameter | Mandatory? | Business Purpose |
| :--- | :---: | :--- |
| **Technician (Employee)** | No | Filter tasks allocated to a specific field employee (auto-locked for technicians). |
| **Work Order Status** | No | Filter by operational stage (e.g., CREATED, ASSIGNED, COMPLETED). |
| **Priority Tier** | No | Filter by urgency (LOW, MEDIUM, HIGH, URGENT). |
| **Client Location** | No | Filter tasks associated with a specific client site. |
| **Date Range (From / To)** | No | Filter tasks scheduled within a specific calendar window. |
| **Page / Page Size** | No | Controls pagination index and records per page. |

---

## 4. Requirement: REQ-FLD-04 — Customer Site Onboarding & Geofencing

* **Requirement Objective:**  
  Register and maintain customer physical facilities with verified geographic GPS coordinates and an authoritative geofence boundary radius.

* **User Story:**  
  *As an Operations Manager, I need to register customer premises with verified GPS coordinates and an allowable geofence radius (e.g., 150m), so that the system can automatically verify that technicians are physically on-site before permitting work to begin.*

### Functional Requirements:
* **FR-04.1 (Unique Site Identification):** The system shall require a unique Site Code (e.g., `'SITE-CHN-001'`) and official facility Name, rejecting duplicate site codes across the platform.
* **FR-04.2 (GPS Coordinate Validation):** The system shall require valid Latitude (-90.0 to +90.0) and Longitude (-180.0 to +180.0) coordinates, explicitly disallowing default '0.0, 0.0' placeholder coordinates.
* **FR-04.3 (Configurable Geofence Radius):** The system shall support a configurable boundary radius between 50 meters (for compact ATMs/kiosks) and 5,000 meters (for large industrial campuses), defaulting to 150 meters.
* **FR-04.4 (Physical Address & Contact Profile):** The system shall capture complete street address, city, state, postal code, and the on-site contact person's name, email, and verified phone number.
* **FR-04.5 (Default Active Status):** Newly registered client sites shall default to Active status upon creation.

### Business Input Requirements:
| Field Name | Mandatory? | Business Description |
| :--- | :---: | :--- |
| **Site Code** | Yes | Unique alphanumeric identifier code (e.g., 'SITE-BLR-01'). |
| **Site Name** | Yes | Official business or branch name (e.g., 'Apollo Hospital Main Campus'). |
| **Latitude / Longitude** | Yes | Authoritative GPS geographic coordinates of the site entrance/center. |
| **Geofence Radius** | No (Def: 150m) | Allowable boundary tolerance radius in meters (50m to 5,000m). |
| **Street Address** | Yes | Full physical street address, building number, and landmark. |
| **Contact Person & Phone** | No | Name and contact phone number of the on-site customer representative. |
| **City / State / Postal Code** | No | Regional location categorization details. |

---

## 5. Requirement: REQ-FLD-05 — Client Sites Master Directory

* **Requirement Objective:**  
  Provide a searchable master directory of all registered customer locations for work order scheduling and territory planning.

* **User Story:**  
  *As a Dispatcher, I need to browse all active customer premises across the territory so that I can quickly select the verified destination when booking service visits.*

### Functional Requirements:
* **FR-05.1 (Active Sites Filtering):** The system shall by default return only active client sites, preventing dispatchers from mistakenly scheduling work orders to closed or decommissioned facilities.
* **FR-05.2 (Inactive Audit Retrieval):** The system shall provide an optional filter parameter (`activeOnly=false`) enabling managers to inspect decommissioned sites for historical task audits.
* **FR-05.3 (Clean Empty-State Contract):** If a new operational region contains zero registered sites, the system shall return an empty list without generating system errors.

### Business Query Parameters:
| Parameter | Mandatory? | Business Purpose |
| :--- | :---: | :--- |
| **Active Only Flag** | No (Def: true) | When true, filters only active locations. When false, includes historical/closed sites. |

---

## 6. Requirement: REQ-FLD-06 — Client Site Profile & Navigation Card

* **Requirement Objective:**  
  Provide detailed site specifications, emergency contacts, and GPS map coordinates for a single customer location.

* **User Story:**  
  *As a Field Technician, I need to view the full contact and address card of my assigned customer site before departure, so that I can call the on-site contact and launch satellite navigation to the facility.*

### Functional Requirements:
* **FR-06.1 (Individual Profile Retrieval):** The system shall return the comprehensive site profile (Site Code, Name, Coordinates, Radius, Street Address, Contact Phone, and Activation Status) matching a specific Site Identifier.
* **FR-06.2 (Missing Entity Handling):** If an invalid or non-existent site identifier is requested, the system shall return a clear error notification ('Client site with ID {id} not found').
* **FR-06.3 (Safe Idempotent Inspection):** Consulting site profile details shall be strictly read-only, producing zero modifications to site configuration or timestamps.

### Business Input Requirements:
| Parameter | Mandatory? | Business Purpose |
| :--- | :---: | :--- |
| **Site Identifier** | Yes | The unique numeric system ID of the client site to inspect. |

---

## 7. Requirements Traceability Matrix

| Req ID | Requirement Title | Primary Stakeholder | Core Business Rule / Safeguard |
| :--- | :--- | :--- | :--- |
| **REQ-FLD-01** | Task Creation & Scheduling | Dispatcher | End time must be after start time; Auto-generated TSK-YYYYMM-XXXX code. |
| **REQ-FLD-02** | Task Reassignment | Manager / Supervisor | Reassignment blocked once technician checks in on-site (`CHECKED_IN`). |
| **REQ-FLD-03** | Work Order Listing & Filtering | Field Technician & Manager | Horizontal data privacy: Technicians strictly restricted to their own tasks. |
| **REQ-FLD-04** | Client Site Onboarding | Operations Manager | Duplicate siteCode blocked; Geofence radius constrained between 50m and 5000m. |
| **REQ-FLD-05** | Client Sites Master Directory | Dispatcher | Active sites returned by default to avoid dispatching to closed facilities. |
| **REQ-FLD-06** | Client Site Profile & Directions | Field Technician | Read-only profile lookup for contact telephone and map navigation. |
