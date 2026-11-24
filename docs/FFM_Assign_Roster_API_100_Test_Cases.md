# FFM Workforce Service — Assign Roster API 100 Test Cases

## Module: Dynamic Roster Scheduling Engine
### Service: `FFM.Workforce` (Port 5010)
### Document Reference: `FFM-TC-WRK-ASSIGN-ROSTER-100-V1.0`

**API Endpoint:** `POST /api/v1/Rosters/assign`  
**Architectural Standards:** Zero Path Parameters, Clean Architecture, CQRS (MediatR), RFC 7807 (ProblemDetails), RS256 Bearer JWT  
**Authorized Roles:** `SYSTEM_ADMIN`, `ADMIN`, `MANAGER`, `SUPERVISOR` (`EMPLOYEE` strictly prohibited $\rightarrow$ `403 Forbidden`)  
**Database Tables:** `"RosterAssignments"`, `"Employees"`, `"Shifts"` in PostgreSQL (`ffm_db` on Port 5434)

---

## 1. Test Cases Summary Matrix

| Category | Count | Primary Focus |
|---|:---:|---|
| **1. Positive / Functional Shift Assignment** | 15 | Valid employee assignment across morning, evening, night, general, and rest-day shifts, zone assignment, note capturing, UTC date normalization, response enrichment |
| **2. BR-2 Active Employee & Entity Existence Guards** | 15 | Inactive, pending, suspended, terminated, deactivated guards (`400 Bad Request`), non-existent employee ID (`404 Not Found`), zero/negative ID boundaries |
| **3. BR-5 Duplicate Roster & Overlap Conflict Guards** | 15 | Same-day duplicate collision (`409 Conflict`), multiple shift conflict on same day, PostgreSQL filtered unique index enforcement, re-assignment after cancellation |
| **4. BR-8 / BR-9 Leave Collision Guards** | 15 | Annual, sick, and emergency approved/pending leave collisions (`400 Bad Request`), multi-day leave boundaries, allowed assignment on rejected/cancelled leaves |
| **5. Shift Template Validation & Boundary Rules** | 15 | Non-existent shift template (`404 Not Found`), inactive/deprecated shift templates, past date boundary rejection, zone (100 char) and notes (500 char) validation |
| **6. RBAC Security, Zero Path & Concurrency Stress** | 25 | Strict RBAC permissions, blocking `EMPLOYEE` role (`403 Forbidden`), Zero Path violations (`404 Not Found`), SQL/XSS injection sanitization, race conditions, and load resilience |
| **TOTAL** | **100** | **Comprehensive Full-Spectrum Enterprise Test Suite** |

---

## 2. Complete 100 Test Cases Specification Table

| Test ID | Category | Scenario Description | Request Parameters / Headers / Payload | Status | Expected Behavior / Assertions |
|:---:|---|---|---|:---:|---|
| **TC-RST-001** | Positive | Assign active employee to Morning shift (`MCT-MORN`) | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2026-11-25", "zone": "North-Zone", "notes": "Valve inspection"}` | 200 OK | Status `ASSIGNED`, timings `07:00:00 - 15:00:00`, color `#1C585C`, returns full enriched DTO |
| **TC-RST-002** | Positive | Assign active employee to Evening shift (`MCT-EVEN`) | `{"employeeId": 10, "shiftId": 2, "rosterDate": "2026-11-26", "zone": "Central Ward"}` | 200 OK | Status `ASSIGNED`, timings `15:00:00 - 23:00:00`, color `#D97706` |
| **TC-RST-003** | Positive | Assign active employee to Night Emergency shift (`MCT-NGHT`) | `{"employeeId": 10, "shiftId": 3, "rosterDate": "2026-11-27", "zone": "Seeb Sector"}` | 200 OK | Status `ASSIGNED`, timings `23:00:00 - 07:00:00`, color `#4338CA`, `isNightShift: true` |
| **TC-RST-004** | Positive | Assign active employee to General Directorate shift (`MCT-GEN`) | `{"employeeId": 10, "shiftId": 4, "rosterDate": "2026-11-28", "zone": "HQ Operations"}` | 200 OK | Status `ASSIGNED`, timings `08:00:00 - 17:00:00`, color `#059669` |
| **TC-RST-005** | Positive | Assign scheduled rest day (`MCT-OFF`) | `{"employeeId": 10, "shiftId": 5, "rosterDate": "2026-11-29", "zone": "Official Off"}` | 200 OK | Status `ASSIGNED`, `isRestDay: true`, color `#64748B`, duration 0.0 hrs |
| **TC-RST-006** | Positive | Assignment with null notes | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2026-11-25", "zone": "North-Zone", "notes": null}` | 200 OK | `notes: null` serialized cleanly without error |
| **TC-RST-007** | Positive | Assignment with empty string notes | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2026-11-25", "zone": "North-Zone", "notes": ""}` | 200 OK | Accepted; stored as empty/null |
| **TC-RST-008** | Positive | Assignment with null zone | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2026-11-25", "zone": null}` | 200 OK | Optional zone field accepts null |
| **TC-RST-009** | Positive | Assignment with maximum allowable zone length (100 chars) | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2026-11-25", "zone": "<100 characters string>"}` | 200 OK | Exactly 100 characters accepted without truncation |
| **TC-RST-010** | Positive | Assignment with maximum allowable notes length (500 chars) | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2026-11-25", "notes": "<500 characters string>"}` | 200 OK | Exactly 500 characters accepted without truncation |
| **TC-RST-011** | Positive | Assignment for current calendar date (today) | `{"employeeId": 10, "shiftId": 1, "rosterDate": "<Today UTC Date>"}` | 200 OK | Current day roster assignment permitted |
| **TC-RST-012** | Positive | Assignment for future calendar date (tomorrow) | `{"employeeId": 10, "shiftId": 1, "rosterDate": "<Tomorrow UTC Date>"}` | 200 OK | Forward scheduling permitted |
| **TC-RST-013** | Positive | Assignment for 30 days in advance (monthly planning) | `{"employeeId": 10, "shiftId": 1, "rosterDate": "<Today + 30 Days>"}` | 200 OK | Advanced schedule planning permitted |
| **TC-RST-014** | Positive | Response envelope and DTO structure verification | Inspect response body from `TC-RST-001` | 200 OK | Contains `id`, `employeeId`, `employeeCode`, `employeeName`, `shiftId`, `shiftCode`, `shiftName`, `startTime`, `endTime`, `colorCode`, `status`, `zone`, `notes`, `createdBy`, `createdAt` |
| **TC-RST-015** | Positive | Database persistence verification in `"RosterAssignments"` | SQL check: `SELECT * FROM "RosterAssignments" WHERE "Id" = <id>` | DB Match | Record exists with matching foreign keys, UTC date, and status `ASSIGNED` |
| **TC-RST-016** | BR-2 Guard | Inactive employee in `PENDING_ACTIVATION` status rejected | `{"employeeId": <Pending_Emp_Id>, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | "Only active employees can be assigned to a shift roster." |
| **TC-RST-017** | BR-2 Guard | Inactive employee in `DEACTIVATED` status rejected | `{"employeeId": <Deactivated_Emp_Id>, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | "Only active employees can be assigned to a shift roster." |
| **TC-RST-018** | BR-2 Guard | Inactive employee in `SUSPENDED` status rejected | `{"employeeId": <Suspended_Emp_Id>, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | "Only active employees can be assigned to a shift roster." |
| **TC-RST-019** | BR-2 Guard | Inactive employee in `TERMINATED` status rejected | `{"employeeId": <Terminated_Emp_Id>, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | "Only active employees can be assigned to a shift roster." |
| **TC-RST-020** | BR-2 Guard | Inactive employee in `ON_LEAVE` status rejected | `{"employeeId": <OnLeave_Emp_Id>, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | "Only active employees can be assigned to a shift roster." |
| **TC-RST-021** | BR-2 Guard | Non-existent employee ID | `{"employeeId": 999999, "shiftId": 1, "rosterDate": "2026-11-25"}` | 404 Not Found | RFC 7807: "Employee with Id 999999 not found." |
| **TC-RST-022** | BR-2 Guard | Zero employee ID rejected by validator | `{"employeeId": 0, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | FluentValidation: "EmployeeId must be greater than 0." |
| **TC-RST-023** | BR-2 Guard | Negative employee ID rejected by validator | `{"employeeId": -5, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | FluentValidation: "EmployeeId must be greater than 0." |
| **TC-RST-024** | BR-2 Guard | Decimal employee ID rejected by model binder | `{"employeeId": 10.5, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | JSON deserializer rejects non-integer ID |
| **TC-RST-025** | BR-2 Guard | String employee ID rejected by model binder | `{"employeeId": "EMP-010", "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | Type mismatch rejected by ASP.NET model binder |
| **TC-RST-026** | BR-2 Guard | Null employee ID in JSON body | `{"employeeId": null, "shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | Model binder flags required integer missing |
| **TC-RST-027** | BR-2 Guard | Re-activated employee (`DEACTIVATED` $\rightarrow$ `ACTIVE`) accepted | Employee transitioned to `ACTIVE` $\rightarrow$ assign shift | 200 OK | Allowed once status is officially `ACTIVE` |
| **TC-RST-028** | BR-2 Guard | Newly onboarded active employee accepted | Newly created employee with `ACTIVE` status | 200 OK | Allowed immediately upon active status |
| **TC-RST-029** | BR-2 Guard | Multiple different active employees assigned to same shift | Assign Emp 10 and Emp 11 to Shift 1 on same date | 200 OK | Multi-crew deployment permitted |
| **TC-RST-030** | BR-2 Guard | Active employee with special characters in name | Employee name: `Kamali N'ambü-O'Connor` | 200 OK | Enriched name preserved accurately in response DTO |
| **TC-RST-031** | BR-5 Overlap | Exact duplicate assignment on same calendar day | Assign Emp 10 to Shift 1 on `2026-11-25`, then repeat same call | 409 Conflict | "A roster assignment already exists for this employee on the specified date." |
| **TC-RST-032** | BR-5 Overlap | Different shift duplicate assignment on same calendar day | Assign Emp 10 to Shift 1 (Morning), then Shift 2 (Evening) on `2026-11-25` | 409 Conflict | Duplicate day collision blocked regardless of shift timing |
| **TC-RST-033** | BR-5 Overlap | Rest day assigned on day already rostered with work shift | Assign Shift 1 (Morning), then Shift 5 (`MCT-OFF`) on same date | 409 Conflict | Blocked: employee already rostered on that date |
| **TC-RST-034** | BR-5 Overlap | Work shift assigned on day already marked rest day | Assign Shift 5 (`MCT-OFF`), then Shift 1 (Morning) on same date | 409 Conflict | Blocked: employee already rostered on that date |
| **TC-RST-035** | BR-5 Overlap | Same employee on consecutive separate calendar days allowed | Assign Emp 10 on `2026-11-25` and `2026-11-26` | 200 OK | Consecutive separate calendar days permitted |
| **TC-RST-036** | BR-5 Overlap | Re-assignment allowed when previous assignment status is `CANCELLED` | Assignment 1 cancelled $\rightarrow$ Re-assign on same date | 200 OK | Cancelled record filtered by `Status != 'CANCELLED'`; new assignment succeeds |
| **TC-RST-037** | BR-5 Overlap | Re-assignment blocked if previous assignment status is `ASSIGNED` | Prior record exists with `Status: 'ASSIGNED'` | 409 Conflict | Active assignment prevents collision |
| **TC-RST-038** | BR-5 Overlap | Re-assignment blocked if previous assignment status is `CONFIRMED` | Prior record exists with `Status: 'CONFIRMED'` | 409 Conflict | Confirmed assignment prevents collision |
| **TC-RST-039** | BR-5 Overlap | Re-assignment blocked if previous assignment status is `COMPLETED` | Prior record exists with `Status: 'COMPLETED'` | 409 Conflict | Completed assignment prevents retroactive overwrite |
| **TC-RST-040** | BR-5 Overlap | Database filtered unique index enforcement | SQL insert bypassing API: duplicate `(EmployeeId, RosterDate)` | DB 23505 | `IX_RosterAssignments_Employee_RosterDate` unique index prevents DB duplication |
| **TC-RST-041** | BR-5 Overlap | ProblemDetails RFC 7807 contract on duplicate collision | Inspect 409 response body | 409 Conflict | Returns `status: 409`, `title: "Conflict"`, `detail: "A roster assignment already exists..."` |
| **TC-RST-042** | BR-5 Overlap | Duplicate attempt with different zone rejected | Emp 10 on `2026-11-25` in Zone A, then try Zone B | 409 Conflict | Zone variation does not bypass day-level unique rule |
| **TC-RST-043** | BR-5 Overlap | Duplicate attempt with different notes rejected | Emp 10 on `2026-11-25` with Note A, then try Note B | 409 Conflict | Notes variation does not bypass day-level unique rule |
| **TC-RST-044** | BR-5 Overlap | Adjacent night shift crossover boundary check | Emp 10 assigned Night shift (23:00-07:00) on Day 1, and Day 2 assigned Morning shift (07:00-15:00) | 200 OK | Handled as distinct calendar dates without calendar date overlap |
| **TC-RST-045** | BR-5 Overlap | UTC date portion normalization prevents time-offset duplicates | Submit `2026-11-25T03:00:00Z` and `2026-11-25T18:00:00Z` | 409 Conflict | Both normalize to `2026-11-25T00:00:00Z`; duplicate blocked |
| **TC-RST-046** | Leave Collision | Employee on APPROVED Annual Leave on roster date | Employee has approved Annual Leave on `2026-11-25` | 400 Bad Request | "Employee has approved/pending leave on the specified date." |
| **TC-RST-047** | Leave Collision | Employee on APPROVED Sick Leave on roster date | Employee has approved Sick Leave on `2026-11-25` | 400 Bad Request | "Employee has approved/pending leave on the specified date." |
| **TC-RST-048** | Leave Collision | Employee on APPROVED Emergency Leave on roster date | Employee has approved Emergency Leave on `2026-11-25` | 400 Bad Request | "Employee has approved/pending leave on the specified date." |
| **TC-RST-049** | Leave Collision | Employee with PENDING leave request on roster date | Employee has pending unapproved leave on `2026-11-25` | 400 Bad Request | "Employee has approved/pending leave on the specified date." |
| **TC-RST-050** | Leave Collision | Multi-day leave collision: Day 1 of 5 | Approved leave spanning Nov 20 to Nov 24; assign on Nov 20 | 400 Bad Request | Collision on first day of span |
| **TC-RST-051** | Leave Collision | Multi-day leave collision: Day 3 of 5 (middle) | Approved leave spanning Nov 20 to Nov 24; assign on Nov 22 | 400 Bad Request | Collision on middle day of span |
| **TC-RST-052** | Leave Collision | Multi-day leave collision: Day 5 of 5 (final day) | Approved leave spanning Nov 20 to Nov 24; assign on Nov 24 | 400 Bad Request | Collision on boundary end date |
| **TC-RST-053** | Leave Collision | Assignment permitted on day immediately BEFORE leave starts | Approved leave begins Nov 21; assign on Nov 20 | 200 OK | Allowed: Nov 20 is not covered by leave |
| **TC-RST-054** | Leave Collision | Assignment permitted on day immediately AFTER leave ends | Approved leave ends Nov 24; assign on Nov 25 | 200 OK | Allowed: Nov 25 is not covered by leave |
| **TC-RST-055** | Leave Collision | Employee with REJECTED leave request on roster date | Employee has rejected leave request on `2026-11-25` | 200 OK | Allowed: rejected leave does not impede scheduling |
| **TC-RST-056** | Leave Collision | Employee with CANCELLED leave request on roster date | Employee has cancelled leave request on `2026-11-25` | 200 OK | Allowed: cancelled leave does not impede scheduling |
| **TC-RST-057** | Leave Collision | Employee with DRAFT / UNSUBMITTED leave request | Employee created draft leave not yet submitted | 200 OK | Allowed: draft leave does not block roster |
| **TC-RST-058** | Leave Collision | Rest-day assignment attempted during approved leave | Try assigning Shift 5 (`MCT-OFF`) during approved leave | 400 Bad Request | Blocked: employee cannot be rostered on leave |
| **TC-RST-059** | Leave Collision | Leave collision error message and error code clarity | Inspect error response for leave collision | 400 Bad Request | Detail message precisely cites leave conflict |
| **TC-RST-060** | Leave Collision | Leave verification service fallback resilience | When leave repository reports empty list | 200 OK | Proceed smoothly without throwing unhandled exception |
| **TC-RST-061** | Shift Template | Non-existent shift ID | `{"employeeId": 10, "shiftId": 99999, "rosterDate": "2026-11-25"}` | 404 Not Found | "Shift with Id 99999 not found." |
| **TC-RST-062** | Shift Template | Zero shift ID rejected by validator | `{"employeeId": 10, "shiftId": 0, "rosterDate": "2026-11-25"}` | 400 Bad Request | FluentValidation: "ShiftId must be greater than 0." |
| **TC-RST-063** | Shift Template | Negative shift ID rejected by validator | `{"employeeId": 10, "shiftId": -1, "rosterDate": "2026-11-25"}` | 400 Bad Request | FluentValidation: "ShiftId must be greater than 0." |
| **TC-RST-064** | Shift Template | Inactive / Deprecated shift template rejected | Shift record exists with `isActive: false` | 400 Bad Request | "The selected shift template is inactive." |
| **TC-RST-065** | Shift Template | Missing `shiftId` in JSON payload | `{"employeeId": 10, "rosterDate": "2026-11-25"}` | 400 Bad Request | FluentValidation: "ShiftId must be greater than 0." |
| **TC-RST-066** | Shift Template | Missing `employeeId` in JSON payload | `{"shiftId": 1, "rosterDate": "2026-11-25"}` | 400 Bad Request | FluentValidation: "EmployeeId must be greater than 0." |
| **TC-RST-067** | Shift Template | Missing `rosterDate` in JSON payload | `{"employeeId": 10, "shiftId": 1}` | 400 Bad Request | FluentValidation: "RosterDate is required." |
| **TC-RST-068** | Shift Template | Past calendar date assignment rejected | `{"employeeId": 10, "shiftId": 1, "rosterDate": "2020-01-01"}` | 400 Bad Request | FluentValidation: "RosterDate cannot be in the past." |
| **TC-RST-069** | Shift Template | Malformed date string in `rosterDate` | `{"employeeId": 10, "shiftId": 1, "rosterDate": "25-11-2026"}` | 400 Bad Request | Deserializer rejects non-ISO date format |
| **TC-RST-070** | Shift Template | Zone string exceeding 100 characters rejected | Zone string of 101 characters | 400 Bad Request | FluentValidation: "Zone cannot exceed 100 characters." |
| **TC-RST-071** | Shift Template | Zone string with leading/trailing whitespaces trimmed | `{"zone": "  North-Zone  "}` | 200 OK | Trimmed cleanly on persistence |
| **TC-RST-072** | Shift Template | Notes string exceeding 500 characters rejected | Notes string of 501 characters | 400 Bad Request | FluentValidation: "Notes cannot exceed 500 characters." |
| **TC-RST-073** | Shift Template | Empty JSON object body `{}` | `{}` | 400 Bad Request | Required fields flagged by FluentValidation |
| **TC-RST-074** | Shift Template | Null request body | Null content body | 400 Bad Request | ASP.NET rejects null request payload |
| **TC-RST-075** | Shift Template | Non-JSON payload content type | Send body as `text/plain` or `application/xml` | 415 Unsupported Media Type | Only `application/json` accepted |
| **TC-RST-076** | RBAC & Sec | Missing Authorization header | `POST /api/v1/Rosters/assign` without Bearer | 401 Unauthorized | Missing JWT authentication token rejected |
| **TC-RST-077** | RBAC & Sec | Malformed Bearer header | `Authorization: Bearer invalid_token_format` | 401 Unauthorized | Malformed token rejected |
| **TC-RST-078** | RBAC & Sec | Expired JWT token | Bearer with expired timestamp | 401 Unauthorized | Expired token rejected |
| **TC-RST-079** | RBAC & Sec | Tampered JWT RS256 signature | Bearer with altered claims payload | 401 Unauthorized | RS256 cryptographic verification fails |
| **TC-RST-080** | RBAC & Sec | `SYSTEM_ADMIN` role permitted | Bearer token with `SYSTEM_ADMIN` role | 200 OK | Assignment permitted |
| **TC-RST-081** | RBAC & Sec | `ADMIN` role permitted | Bearer token with `ADMIN` role | 200 OK | Assignment permitted |
| **TC-RST-082** | RBAC & Sec | `MANAGER` role permitted | Bearer token with `MANAGER` role | 200 OK | Assignment permitted |
| **TC-RST-083** | RBAC & Sec | `SUPERVISOR` role permitted | Bearer token with `SUPERVISOR` role | 200 OK | Assignment permitted |
| **TC-RST-084** | RBAC & Sec | `EMPLOYEE` role strictly prohibited from assigning | Bearer token with `EMPLOYEE` role | 403 Forbidden | Field technicians cannot assign rosters |
| **TC-RST-085** | RBAC & Sec | Authenticated token with empty/no role claims | Bearer token without role claim | 403 Forbidden | Access denied without privileged role |
| **TC-RST-086** | Standards | Zero Path Parameter standard: ID in path prohibited | `POST /api/v1/Rosters/assign/10` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-RST-087** | Standards | Zero Path Parameter standard: Employee ID in path prohibited | `POST /api/v1/Rosters/10/assign` | 404 Not Found | URL path mutations prohibited |
| **TC-RST-088** | Standards | HTTP GET on assignment endpoint rejected | `GET /api/v1/Rosters/assign` | 405 Method Not Allowed | Mutation endpoint rejects read verb |
| **TC-RST-089** | Standards | HTTP PUT on assignment endpoint rejected | `PUT /api/v1/Rosters/assign` | 405 Method Not Allowed | Method not allowed |
| **TC-RST-090** | Standards | HTTP DELETE on assignment endpoint rejected | `DELETE /api/v1/Rosters/assign` | 405 Method Not Allowed | Method not allowed |
| **TC-RST-091** | Standards | HTTP PATCH on assignment endpoint rejected | `PATCH /api/v1/Rosters/assign` | 405 Method Not Allowed | Method not allowed |
| **TC-RST-092** | Edge & Sec | SQL injection in `zone` parameter | `{"zone": "North'; DROP TABLE \"RosterAssignments\";--"}` | 200 OK | Parameterized by EF Core; inserted literally as text without executing SQL |
| **TC-RST-093** | Edge & Sec | SQL injection in `notes` parameter | `{"notes": "' OR 1=1; SELECT * FROM \"Employees\"--"}` | 200 OK | Parameterized safely by EF Core |
| **TC-RST-094** | Edge & Sec | XSS script payload in `zone` parameter | `{"zone": "<script>alert('xss')</script>"}` | 200 OK | Stored safely; sanitized/escaped upon frontend rendering |
| **TC-RST-095** | Edge & Sec | XSS script payload in `notes` parameter | `{"notes": "<img src=x onerror=alert('xss')>"}` | 200 OK | Stored safely; sanitized upon rendering |
| **TC-RST-096** | Edge & Sec | Concurrent race condition: 2 identical requests | Two identical requests sent simultaneously in parallel threads | 200 OK / 409 Conflict | Exactly 1 request succeeds (200), other receives 409 Conflict |
| **TC-RST-097** | Edge & Sec | High concurrency burst: 50 distinct employees assigned | 50 parallel requests assigning 50 different active employees | 200 OK | 100% success rate without deadlocks |
| **TC-RST-098** | Edge & Sec | Database connection pool recovery during heavy assignment load | Rapid assignment calls under DB load | 200 OK | Connection pool handles transactions smoothly |
| **TC-RST-099** | Edge & Sec | Audit trail integrity: `CreatedBy` captured from JWT | Inspect `CreatedBy` field on created record | 200 OK | Recorded as `USER_<id> (<role>)` matching caller claims |
| **TC-RST-100** | Edge & Sec | End-to-end live integration test with test user | Submit live request with test token for `test@example.com` | 200 OK | Live end-to-end verification successful, record verified in DB |

---

## 3. Verification & Compliance Sign-Off

- **Lead Full-Stack Architect:** Nambu Kamali
- **Standards Organization:** ISO/IEC/IEEE 29119 Software Testing Standards
- **Architectural Tenets:** Clean Architecture, CQRS (MediatR), Zero Path Parameters Standard, RFC 7807 (ProblemDetails)
- **Status:** **Ready for Live Execution & Automated CI Pipeline**
