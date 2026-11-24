# Field Force Management (FFM) Platform — Requirement Document for Field Operation Service APIs

## 1. Module 1: Create New Work Order (Task Creation)

* **What is the Module?**  
  Booking, configuring, and scheduling a new field service job or inspection for an operational team.

* **Who Uses It? (Authorized Roles):**  
  **Admin**, **Manager**, and **Supervisor** *(Office Dispatchers & Field Leads. Blocked for Employee).*

* **Real-World Business Scenario:**  
  A bank branch contact desk calls to report that an ATM cash sensor has failed. The dispatcher needs to officially create a service work order, attach it to that specific bank branch, set an urgent priority, and allocate it to a certified technician with a mandatory 2-hour completion window.

### What the Module Does (Functional Workflow):
1. **Job Title & Instructions:** Allows the dispatcher to enter a clear work description (e.g., *"Emergency ATM Cash Dispenser Sensor Fix"*) with detailed technical notes.
2. **Customer Site Linking:** Attaches the job to a verified customer branch or facility from the directory so the technician knows the exact physical destination.
3. **Priority Categorization:** Assigns urgency (**Low**, **Medium**, **High**, or **Urgent**) so technicians can prioritize life-safety or revenue-critical repairs first.
4. **Scheduled Window:** Sets a planned start date/time and a hard completion deadline. Prevents scheduling errors by requiring the end time to be later than the start time.
5. **Flexible Technician Allocation:** Enables direct assignment to an available technician (which immediately notifies them) or allows saving as an open task for later dispatching.
6. **Automatic Tracking Code:** Generates an auto-incremented, audit-ready work order number (e.g., `TSK-01`) for invoicing and customer reference.

* **Business Benefit:**  
  Eliminates lost work orders, verbal miscommunication, and missed SLAs by establishing a single source of truth for every field assignment.

---

## 2. Module 2: Reassign Work Order (Technician Handover)

* **What is the Module?**  
  Transferring an already scheduled work order from one technician to another colleague without disrupting customer service.

* **Who Uses It? (Authorized Roles):**  
  **Admin**, **Manager**, and **Supervisor** *(Blocked for Employee).*

* **Real-World Business Scenario:**  
  Technician Ramesh is scheduled to service a hospital generator at 2:00 PM, but his service vehicle breaks down on the highway. The dispatcher must quickly reallocate the critical hospital job to Suresh, who is already in that neighborhood, so the hospital's appointment is not missed.

### What the Module Does (Functional Workflow):
1. **One-Click Technician Transfer:** Allows the supervisor to select an existing task and choose an active, available technician.
2. **Immediate Schedule Synchronization:** Transfers the job to the new technician's daily mobile roster and frees up the original technician's calendar.
3. **Active Work Safety Guard:** If a technician has already physically checked in on-site and started the repair, the system blocks reassignment to prevent confusion, double billing, or duplicate work.
4. **Complete Audit Trail:** Maintains a full digital history showing who transferred the job, from whom, to whom, and at what exact timestamp for managerial governance.

* **Business Benefit:**  
  Guarantees business continuity and 100% SLA compliance during vehicle breakdowns, staff illness, or sudden peak workloads.

---

## 3. Module 3: View & Track Work Orders (Task Schedule & Board)

* **What is the Module?**  
  The centralized daily tracking dashboard for office staff and the private schedule viewer for field technicians.

* **Who Uses It? (Authorized Roles):**  
  **All Roles** — **Employee** *(Field Technicians view only their own daily schedule)* and **Manager**, **Supervisor**, **Admin** *(view full team-wide board).*

* **Real-World Business Scenario:**  
  Every morning at 8:00 AM, a field technician opens their smartphone to see their personal schedule of jobs for the day. At the same time, the operations director looks at a live office screen to monitor which jobs are pending, which technicians are en route, and which jobs are completed across the city.

### What the Module Does (Functional Workflow):
1. **Field Staff Privacy & Focus:** Field technicians see only their own assigned jobs for today in time order. They cannot view other technicians' jobs, preventing operational confusion and protecting customer privacy.
2. **Managerial 360-Degree Visibility:** Office managers can view all jobs across all teams or filter by specific technicians, zones, or branches.
3. **Multi-Criteria Filtering:** Enables instant filtering by work order status (*Created, Assigned, En Route, Checked-In, Completed*), priority level, or date range.
4. **Fast Screen Loading:** Delivers jobs in clean, lightweight pages (e.g., 20 jobs per page) so mobile devices load instantly even in areas with low mobile network signals.

* **Business Benefit:**  
  Gives field staff complete clarity on their daily route while giving executive management real-time visibility into overall field productivity.

---

## 4. Module 4: Register Customer Site & Location (Client Site Onboarding)

* **What is the Module?**  
  Onboarding customer facilities, offices, branches, and kiosks into the system with an invisible digital geofence boundary.

* **Who Uses It? (Authorized Roles):**  
  **Admin** and **Manager** *(Account Onboarding Teams. Blocked for Employee).*

* **Real-World Business Scenario:**  
  A commercial retail chain signs a maintenance contract for 25 supermarket stores. The operations lead must enter each store's address, the store manager's phone number, and establish a 150-meter digital GPS fence around each building so technicians can only log attendance when physically inside the store.

### What the Module Does (Functional Workflow):
1. **Unique Site Code & Profile:** Assigns an official identification code (e.g., ``site-01``) and customer business name to prevent accidental duplicate customer records.
2. **High-Precision GPS Coordinates:** Captures the exact Latitude and Longitude coordinates of the customer facility.
3. **Configurable Geofence Radius:** Establishes an invisible digital circle (between 50 meters for compact stores and 5,000 meters for large industrial campuses, default: 150m).
4. **Automated Arrival Verification:** Stores these coordinates so that later, when the technician visits, their mobile phone GPS verifies they are physically on-site before permitting work to begin.
5. **Emergency Contact Directory:** Captures on-site facility contact names, mobile phone numbers, and physical street addresses.

* **Business Benefit:**  
  Permanently eliminates fraudulent visit claims. Technicians cannot sit at a tea stall 1 km away and falsely claim they reached the client site.

---

## 5. Module 5: Customer Sites Directory (View All Sites)

* **What is the Module?**  
  The master address book and interactive directory of all customer locations available for service visits.

* **Who Uses It? (Authorized Roles):**  
  **Admin**, **Manager**, **Supervisor**, and **Employee** *(All authenticated users).*

* **Real-World Business Scenario:**  
  A customer calls the helpline requesting urgent elevator servicing. The dispatcher opens the customer directory, browses active branches in that city, verifies the exact site address, and immediately links it to a new work ticket.

### What the Module Does (Functional Workflow):
1. **Active Locations by Default:** Displays currently active and operational customer sites so dispatchers never send technicians to closed or demolished buildings.
2. **Audit & Inactive Facility Access:** Allows supervisors to view retired, seasonal, or temporarily closed sites when reviewing past service histories or financial invoices.
3. **Clean User Interface:** If a newly opened district has no customer sites registered yet, it displays a helpful empty prompt rather than crashing or displaying technical error screens.

* **Business Benefit:**  
  Prevents costly dispatch errors and provides a central, up-to-date customer address directory for the entire company.

---

## 6. Module 6: Customer Site Profile & Directions (View Single Site)

* **What is the Module?**  
  The comprehensive customer contact, facility guide, and GPS navigation profile card for a specific site.

* **Who Uses It? (Authorized Roles):**  
  **Employee** *(Field Technicians navigating to customer site)*, **Supervisor**, **Manager**, and **Admin**.

* **Real-World Business Scenario:**  
  A technician is dispatched to service medical equipment at an Apollo Hospital campus for the first time. Before driving, they open the site card to find out which gate to enter, view the facility manager's name and direct phone number to announce their arrival, and launch Google Maps directly to the site.

### What the Module Does (Functional Workflow):
1. **Complete Facility Card:** Shows full street address, city, state, postal code, and specific landmark directions.
2. **Direct Point-of-Contact:** Displays the on-site contact person's name and verified phone number so the technician can call directly from their smartphone before arrival.
3. **One-Tap Map Navigation:** Supplies precise GPS coordinates so the mobile phone opens satellite turn-by-turn driving directions directly to the facility gate.
4. **Safe Read-Only Access:** Technicians can freely consult site contact details without any risk of accidentally modifying, corrupting, or deleting customer profile information.

* **Business Benefit:**  
  Reduces travel delays, eliminates wrong-gate arrivals, and ensures technicians can always contact the right customer representative immediately.



