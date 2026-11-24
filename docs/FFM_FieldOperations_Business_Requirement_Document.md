# Business Requirement Document (BRD) — Field Operations Service

## 1. Document Overview

| Item | Details |
| :--- | :--- |
| **Project Name** | Field Force Management (FFM) Platform |
| **Document Type** | Business & Functional Requirement Document (BRD / FRD) |
| **Module** | Field Operations (Tasks & Client Sites Management) |
| **Target Audience** | Product Managers, Tech Leads, QA Engineers, Business Stakeholders |
| **Scope** | Functional requirements for Core 6 Operations APIs |

---

## 2. Business Purpose & Problem Statement

Field service businesses frequently struggle with:
1. **Inefficient Task Scheduling:** Dispatchers struggle to assign work orders with clear deadlines and priorities.
2. **Lack of Location Verification:** No reliable way to ensure field technicians actually visit the customer site before marking work as done.
3. **Data Privacy & Confusion:** Technicians getting overwhelmed or seeing other technicians' tasks, leading to privacy and operational conflicts.
4. **Delayed Reassignments:** Inability to quickly transfer emergency jobs or reallocate tasks when a technician is absent.

### Solution Overview:
The **Field Operations Service** provides a centralized system to register customer client sites with GPS geofencing, create structured work orders, assign technicians, and track execution from creation to completion.

---

## 3. User Personas

| Persona | Role Description | Primary Goal in System |
| :--- | :--- | :--- |
| **Operations Manager** | Oversees field operations and team performance. | Monitor overall task completion and customer site coverage. |
| **Dispatcher** | Schedules work orders and assigns technicians daily. | Create tasks, assign available technicians, and reassign when needed. |
| **Field Technician** | Goes on-site to inspect, repair, or service equipment. | View daily assigned tasks, customer site address, and directions. |

---

## 4. Functional Requirements for the 6 Core Features

### Feature 1: Create & Schedule Task (Work Order Creation)

#### Business Need:
Dispatchers need to create tasks for maintenance, repairs, or customer visits and assign them to technicians with clear priority and time windows.

#### User Story:
> *"As a Dispatcher, I want to create a work order with a title, priority, scheduled start/end time, and customer site, so that field work can be planned and tracked."*

#### What Needs to be Done (Requirements):
1. **Title & Description:** Must capture a clear task title (e.g., "Quarterly AC Maintenance") and optional detailed instructions.
2. **Customer Site Association:** Every task must be linked to a registered customer site so the technician knows where to go.
3. **Priority Levels:** Must allow setting task priority (`LOW`, `MEDIUM`, `HIGH`, `URGENT`) to help technicians prioritize their day.
4. **Time Scheduling:** Must capture scheduled start and end dates/times. The deadline cannot be before the start time.
5. **Flexible Assignment:** 
   - Can be assigned immediately to a technician (status becomes `ASSIGNED`).
   - Or created without a technician (status becomes `CREATED`), allowing managers to assign it later.
6. **Automatic Work Order Number:** The system must generate a human-readable task tracking number (e.g., `TSK-202611-0001`).

---

### Feature 2: Reassign Task (Technician Reallocation)

#### Business Need:
Technicians may face emergencies, bike breakdowns, or heavy workload. Managers need the ability to quickly transfer tasks to another technician without losing history.

#### User Story:
> *"As a Dispatcher/Manager, I want to reassign a task to another technician, so that urgent customer work is completed without delay even if the originally assigned technician is unavailable."*

#### What Needs to be Done (Requirements):
1. **Task & Technician Selection:** The manager selects an existing task and chooses a new eligible technician.
2. **Automatic Status Reset:** When reassigned, the task status must automatically reset to `ASSIGNED` so the new technician receives a notification.
3. **Protection of In-Progress Work:** If a technician has already reached the site and checked in (`CHECKED_IN` or `COMPLETED`), the task cannot be reassigned to prevent confusion and double-work.
4. **Audit History:** The system must maintain an audit record of who reassigned the task, to whom, and when.

---

### Feature 3: List & Filter Tasks (Work Schedule & Task Board)

#### Business Need:
Dispatchers need a filtered view of all team tasks across different stages, while field technicians need a clean daily view of only their assigned work.

#### User Story:
> *"As a Field Technician, I want to see only my assigned tasks for today, so that I can plan my travel route without seeing other employees' tasks."*  
> *"As a Manager, I want to filter tasks by status, priority, and date, so that I can identify overdue or unassigned work orders."*

#### What Needs to be Done (Requirements):
1. **Role-Based Privacy (Horizontal Isolation):**
   - **Field Technicians:** Must **only** see tasks assigned to them. Even if they try to search for another employee's tasks, the system must protect privacy and show only their work.
   - **Managers & Dispatchers:** Can see all tasks across the team or filter by specific employees.
2. **Search & Filter Capabilities:**
   - Filter by Status (`CREATED`, `ASSIGNED`, `IN_PROGRESS`, `COMPLETED`).
   - Filter by Priority (`LOW`, `MEDIUM`, `HIGH`, `URGENT`).
   - Filter by Date Range (e.g., tasks scheduled for today, this week, or this month).
3. **Pagination:** Large lists must be paginated (e.g., 20 tasks per page) so mobile apps and web pages load fast without lagging.

---

### Feature 4: Register Client Site (Customer Premises Setup)

#### Business Need:
Before dispatching technicians, the company needs a master directory of customer premises, ATMs, hospitals, branches, and retail outlets with verified geographic boundaries.

#### User Story:
> *"As an Operations Manager, I want to register a customer site with GPS coordinates and a geofence radius, so that technicians' on-site presence can be automatically verified."*

#### What Needs to be Done (Requirements):
1. **Site Identification:** 
   - Unique Site Code (e.g., `SITE-CHN-001`) to avoid duplicate customer records.
   - Business / Customer Name (e.g., "Apollo Hospitals Main Campus").
2. **Precise GPS Location:** Capture exact Latitude and Longitude of the customer location.
3. **Geofence Boundary Radius:**
   - Define an allowable distance radius around the site (default: 150 meters, minimum 50 meters, maximum 5,000 meters for large campuses).
   - This radius is used later by the mobile app to verify whether the technician has physically arrived at the client site before starting work.
4. **Complete Contact & Address Details:** Capture street address, city, state, postal code, and on-site contact person name and phone number.

---

### Feature 5: Get All Client Sites (Customer Directory)

#### Business Need:
When dispatchers schedule work or technicians check customer directories, they need to view and select from the active customer locations.

#### User Story:
> *"As a Dispatcher, I want to browse all active client sites, so that I can quickly select the correct customer location when creating a task."*

#### What Needs to be Done (Requirements):
1. **Active Sites by Default:** Automatically display active client sites so dispatchers do not accidentally send technicians to closed or retired locations.
2. **Optional Inactive View:** Allow managers to view deactivated sites when auditing past completed work orders.
3. **Graceful Handling of Empty Directory:** If a new region has no sites registered yet, display a clean empty state without crashing or showing technical errors.

---

### Feature 6: Get Client Site by ID (Site Details & Directions)

#### Business Need:
When a technician opens a task on their mobile app, they need full details of the customer site (address, phone number, point of contact, and map coordinates) so they can call the customer and navigate there.

#### User Story:
> *"As a Field Technician, I want to open the customer site details, so that I can view the contact person's phone number and get directions to the site."*

#### What Needs to be Done (Requirements):
1. **Detailed Site View:** Return full site profile including site name, address, contact person, phone number, and GPS coordinates.
2. **Error Handling for Deleted/Missing Sites:** If a site ID does not exist, return a clear, friendly error message: *"Client site with ID {id} not found."*
3. **Safe Idempotent Lookup:** Viewing a site profile must never alter or modify the site data or change its status.

---

## 5. Summary Matrix: What Needs to be Done

| # | Business Capability | Who Uses It? | Key Business Outcome |
|---|---|---|---|
| **1** | **Create Task** | Dispatcher / Manager | Structured work order with auto-generated tracking number, site link, priority, and schedule. |
| **2** | **Reassign Task** | Dispatcher / Manager | Seamless workload reallocation to another technician without losing task continuity. |
| **3** | **List & Filter Tasks** | Technician / Manager | Technician sees personal daily tasks; Manager gets team-wide operational dashboard. |
| **4** | **Register Client Site** | Operations Manager | Customer premise registered with GPS coordinates and geofence radius for automated arrival verification. |
| **5** | **Get All Client Sites** | Dispatcher / Mobile App | Fast search and directory lookup of active customer destinations. |
| **6** | **Get Client Site by ID** | Field Technician | Complete customer site profile, on-site contact phone, and navigation details. |
