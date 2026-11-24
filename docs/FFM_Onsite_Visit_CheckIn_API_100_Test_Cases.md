# FFM Field Operations Service — 100 Test Cases Specification

### Module: On-Site Visit Check-In (`POST /api/v1/visits/check-in`)

> **Standards:** Haversine Geofencing, BOLA Defense, Single Active Visit Guard, Zero Path Parameters

---

## Summary Matrix

| Category | Count | Status |
| :--- | :---: | :---: |
| 1. Positive & Functional Scenarios | 15 | Approved |
| 2. Negative Validation & Boundary Scenarios | 30 | Approved |
| 3. Geofence Distance & Threshold Scenarios | 15 | Approved |
| 4. State Machine & Lifecycle Guards | 15 | Approved |
| 5. Security, BOLA & RBAC Defense Scenarios | 15 | Approved |
| 6. Concurrency, Telemetry & Resiliency Scenarios | 10 | Approved |
| **Total Comprehensive Test Cases** | **100** | **Ready for Review** |

---

## 100 Test Cases Master Table

| Test ID | Category | Test Scenario & Objective | Request Payload / Trigger | Expected HTTP | Expected System Behavior / Output |
| :---: | :---: | :--- | :--- | :---: | :--- |
| **TC-CIN-001** | Positive | Technician checks in within 10m of site center | `{"taskId":5,"latitude":12.97161,"longitude":77.59457,"accuracyMeters":5.5}` | **201 Created** | Visit created, status=CHECKED_IN, distanceMeters < 150m, isWithinGeofence=true |
| **TC-CIN-002** | Positive | Technician checks in at outer geofence boundary (148m of 150m) | `{"taskId":5,"latitude":12.97290,"longitude":77.59457}` | **201 Created** | Verified on-site within threshold, distance=148.2m |
| **TC-CIN-003** | Positive | Technician checks in with high-precision GPS (7 decimal places) | `{"taskId":5,"latitude":12.9715987,"longitude":77.5945627}` | **201 Created** | High-precision telemetry recorded with exact precision |
| **TC-CIN-004** | Positive | Technician checks in with optional accuracyMeters included | `{"taskId":5,"latitude":12.97161,"longitude":77.59457,"accuracyMeters":8.0}` | **201 Created** | Horizontal accuracy accepted and stored |
| **TC-CIN-005** | Positive | Technician checks in with accuracyMeters omitted (null) | `{"taskId":5,"latitude":12.97161,"longitude":77.59457}` | **201 Created** | Accuracy is optional; default accepted |
| **TC-CIN-006** | Positive | Technician checks in with isMockLocation explicit false | `{"taskId":5,"latitude":12.97161,"longitude":77.59457,"isMockLocation":false}` | **201 Created** | Verified authentic hardware GPS telemetry |
| **TC-CIN-007** | Positive | Technician checks in with isMockLocation omitted (null) | `{"taskId":5,"latitude":12.97161,"longitude":77.59457}` | **201 Created** | Omitted mock flag defaults safely to authentic |
| **TC-CIN-008** | Positive | Idempotent check-in retry on already CHECKED_IN visit | `{"taskId":5,"latitude":12.97161,"longitude":77.59457}` | **200 OK** | Returns existing visit details with isIdempotentReplay=true |
| **TC-CIN-009** | Positive | Direct arrival check-in from ASSIGNED status | `{"taskId":5,"latitude":12.97161,"longitude":77.59457}` | **201 Created** | Auto-advances travel state; task status becomes CHECKED_IN |
| **TC-CIN-010** | Positive | Standard operational check-in from EN_ROUTE status | `{"taskId":5,"latitude":12.97161,"longitude":77.59457}` | **201 Created** | Transitions EN_ROUTE -> CHECKED_IN smoothly |
| **TC-CIN-011** | Positive | Supervisor executes check-in on behalf of technician | `Payload with role SUPERVISOR token` | **201 Created** | Supervisor authorized for team operational override |
| **TC-CIN-012** | Positive | Branch Manager executes check-in on behalf of technician | `Payload with role MANAGER token` | **201 Created** | Manager authorized across all branch work orders |
| **TC-CIN-013** | Positive | Operations Admin executes check-in on behalf of technician | `Payload with role ADMIN token` | **201 Created** | Admin authorized globally across all work orders |
| **TC-CIN-014** | Positive | System Admin executes check-in with global authority | `Payload with role SYSTEM_ADMIN token` | **201 Created** | Platform admin override accepted |
| **TC-CIN-015** | Positive | Check-in on large industrial campus site (1000m radius) within 900m | `{"taskId":5,"latitude":12.97900,"longitude":77.59457}` | **201 Created** | Dynamic site radius evaluated; distance 890m <= 1000m permitted |
| **TC-CIN-016** | Validation | Missing request body (Empty POST request) | `{} or null body` | **400 Bad Request** | Rejected: Request body cannot be empty |
| **TC-CIN-017** | Validation | Malformed JSON payload syntax | `{taskId: 5, lat}` | **400 Bad Request** | Rejected: Malformed JSON syntax |
| **TC-CIN-018** | Validation | Missing mandatory taskId field | `{"latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Valid TaskId is required |
| **TC-CIN-019** | Validation | TaskId equals zero (0) | `{"taskId":0,"latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Valid TaskId is required |
| **TC-CIN-020** | Validation | TaskId negative integer (-1) | `{"taskId":-1,"latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Valid TaskId is required |
| **TC-CIN-021** | Validation | TaskId passed as string ('five') | `{"taskId":"five","latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Type mismatch on integer binding |
| **TC-CIN-022** | Validation | TaskId passed as floating point number (5.75) | `{"taskId":5.75,"latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Fractional task ID not allowed |
| **TC-CIN-023** | Validation | Missing mandatory latitude field | `{"taskId":5,"longitude":77.59}` | **400 Bad Request** | Rejected: Missing latitude |
| **TC-CIN-024** | Validation | Missing mandatory longitude field | `{"taskId":5,"latitude":12.97}` | **400 Bad Request** | Rejected: Missing longitude |
| **TC-CIN-025** | Validation | Latitude slightly below lower bound (-90.000001) | `{"taskId":5,"latitude":-90.000001,"longitude":77.59}` | **400 Bad Request** | Rejected: Latitude must be between -90.0 and +90.0 |
| **TC-CIN-026** | Validation | Latitude slightly above upper bound (+90.000001) | `{"taskId":5,"latitude":90.000001,"longitude":77.59}` | **400 Bad Request** | Rejected: Latitude must be between -90.0 and +90.0 |
| **TC-CIN-027** | Validation | Latitude extreme negative value (-150.0) | `{"taskId":5,"latitude":-150.0,"longitude":77.59}` | **400 Bad Request** | Rejected: Invalid coordinate range |
| **TC-CIN-028** | Validation | Latitude extreme positive value (+150.0) | `{"taskId":5,"latitude":150.0,"longitude":77.59}` | **400 Bad Request** | Rejected: Invalid coordinate range |
| **TC-CIN-029** | Validation | Longitude slightly below lower bound (-180.000001) | `{"taskId":5,"latitude":12.97,"longitude":-180.000001}` | **400 Bad Request** | Rejected: Longitude must be between -180.0 and +180.0 |
| **TC-CIN-030** | Validation | Longitude slightly above upper bound (+180.000001) | `{"taskId":5,"latitude":12.97,"longitude":180.000001}` | **400 Bad Request** | Rejected: Longitude must be between -180.0 and +180.0 |
| **TC-CIN-031** | Validation | Longitude extreme negative value (-250.0) | `{"taskId":5,"latitude":12.97,"longitude":-250.0}` | **400 Bad Request** | Rejected: Invalid coordinate range |
| **TC-CIN-032** | Validation | Longitude extreme positive value (+250.0) | `{"taskId":5,"latitude":12.97,"longitude":250.0}` | **400 Bad Request** | Rejected: Invalid coordinate range |
| **TC-CIN-033** | Validation | Latitude passed as text string ('north') | `{"taskId":5,"latitude":"north","longitude":77.59}` | **400 Bad Request** | Rejected: Model binder deserialization error |
| **TC-CIN-034** | Validation | Longitude passed as text string ('east') | `{"taskId":5,"latitude":12.97,"longitude":"east"}` | **400 Bad Request** | Rejected: Model binder deserialization error |
| **TC-CIN-035** | Validation | AccuracyMeters negative value (-5.0m) | `{"taskId":5,"latitude":12.97,"longitude":77.59,"accuracyMeters":-5.0}` | **400 Bad Request** | Rejected: AccuracyMeters cannot be negative |
| **TC-CIN-036** | Validation | AccuracyMeters passed as string ('high') | `{"taskId":5,"latitude":12.97,"longitude":77.59,"accuracyMeters":"high"}` | **400 Bad Request** | Rejected: Invalid decimal format |
| **TC-CIN-037** | Validation | Exact Equator boundary: Latitude=0.0, Longitude=0.0 | `{"taskId":5,"latitude":0.0,"longitude":0.0}` | **400 Bad Request** | Rejected by geofence (location at Gulf of Guinea) |
| **TC-CIN-038** | Validation | Exact North Pole boundary: Latitude=+90.0, Longitude=0.0 | `{"taskId":5,"latitude":90.0,"longitude":0.0}` | **400 Bad Request** | Rejected by geofence (thousands of km away) |
| **TC-CIN-039** | Validation | Exact South Pole boundary: Latitude=-90.0, Longitude=0.0 | `{"taskId":5,"latitude":-90.0,"longitude":0.0}` | **400 Bad Request** | Rejected by geofence (thousands of km away) |
| **TC-CIN-040** | Validation | Exact Antimeridian East: Latitude=0.0, Longitude=+180.0 | `{"taskId":5,"latitude":0.0,"longitude":180.0}` | **400 Bad Request** | Rejected by geofence (Pacific Ocean) |
| **TC-CIN-041** | Validation | Exact Antimeridian West: Latitude=0.0, Longitude=-180.0 | `{"taskId":5,"latitude":0.0,"longitude":-180.0}` | **400 Bad Request** | Rejected by geofence (Pacific Ocean) |
| **TC-CIN-042** | Validation | TaskId integer overflow (> 2,147,483,647) | `{"taskId":9999999999,"latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Int32 overflow |
| **TC-CIN-043** | Validation | Non-existent TaskId (999999) | `{"taskId":999999,"latitude":12.97,"longitude":77.59}` | **404 Not Found** | Rejected: Task with ID 999999 was not found |
| **TC-CIN-044** | Validation | Payload with unsupported extraneous fields included | `{"taskId":5,"latitude":12.97161,"longitude":77.59457,"hacked":"yes"}` | **201 Created** | Safely ignores unknown fields, processes valid fields |
| **TC-CIN-045** | Validation | Null taskId value explicitly passed | `{"taskId":null,"latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Rejected: Valid TaskId is required |
| **TC-CIN-046** | Geofence | Technician location exactly 1m inside boundary (149m on 150m site) | `Coordinates computed for 149m distance` | **201 Created** | Within geofence: distance 149m <= 150m |
| **TC-CIN-047** | Geofence | Technician location exactly on boundary threshold (150.00m) | `Coordinates computed for 150.0m distance` | **201 Created** | Within geofence: boundary equality permitted |
| **TC-CIN-048** | Geofence | Technician location 1m outside boundary (151m on 150m site) | `Coordinates computed for 151m distance` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance 151m > 150m |
| **TC-CIN-049** | Geofence | Technician location 50m outside boundary (200m on 150m site) | `Coordinates computed for 200m distance` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance 200m > 150m |
| **TC-CIN-050** | Geofence | Technician location 500m away in neighboring locality | `{"taskId":5,"latitude":12.97500,"longitude":77.59800}` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance ~500m reported |
| **TC-CIN-051** | Geofence | Technician location 2.5km away (coffee shop sitting) | `{"taskId":5,"latitude":12.99500,"longitude":77.61500}` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance 2,540m reported |
| **TC-CIN-052** | Geofence | Technician location 15km away across the metro area | `{"taskId":5,"latitude":13.08000,"longitude":77.65000}` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance ~15km reported |
| **TC-CIN-053** | Geofence | Technician location in another city (350km away, Chennai) | `{"taskId":5,"latitude":13.0827,"longitude":80.2707}` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance ~300km rejected |
| **TC-CIN-054** | Geofence | Technician location in another country (Singapore) | `{"taskId":5,"latitude":1.3521,"longitude":103.8198}` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance ~3,000km rejected |
| **TC-CIN-055** | Geofence | Compact site: 50m radius, technician at 45m distance | `Compact site coordinates at 45m distance` | **201 Created** | Within geofence: distance 45m <= 50m accepted |
| **TC-CIN-056** | Geofence | Compact site: 50m radius, technician at 55m distance | `Compact site coordinates at 55m distance` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance 55m > 50m rejected |
| **TC-CIN-057** | Geofence | Airport / Campus site: 500m radius, technician at 480m | `Airport site coordinates at 480m distance` | **201 Created** | Within geofence: distance 480m <= 500m accepted |
| **TC-CIN-058** | Geofence | Airport / Campus site: 500m radius, technician at 520m | `Airport site coordinates at 520m distance` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance 520m > 500m rejected |
| **TC-CIN-059** | Geofence | Industrial refinery: 2000m radius, technician at 1950m | `Refinery site coordinates at 1950m distance` | **201 Created** | Within geofence: distance 1950m <= 2000m accepted |
| **TC-CIN-060** | Geofence | Industrial refinery: 2000m radius, technician at 2050m | `Refinery site coordinates at 2050m distance` | **400 Bad Request** | OUTSIDE_GEOFENCE_BOUNDARY: distance 2050m > 2000m rejected |
| **TC-CIN-061** | State Guard | Check-in on task in CREATED status (unassigned work order) | `Task with status=CREATED` | **400 Bad Request** | INVALID_TASK_STATE: Cannot check in to unassigned task |
| **TC-CIN-062** | State Guard | Check-in on task in ASSIGNED status | `Task with status=ASSIGNED` | **201 Created** | Valid transition: ASSIGNED -> CHECKED_IN |
| **TC-CIN-063** | State Guard | Check-in on task in EN_ROUTE status | `Task with status=EN_ROUTE` | **201 Created** | Valid transition: EN_ROUTE -> CHECKED_IN |
| **TC-CIN-064** | State Guard | Check-in on task already in COMPLETED status | `Task with status=COMPLETED` | **400 Bad Request** | INVALID_TASK_STATE: Cannot check in to COMPLETED task |
| **TC-CIN-065** | State Guard | Check-in on task already in CANCELLED status | `Task with status=CANCELLED` | **400 Bad Request** | INVALID_TASK_STATE: Cannot check in to CANCELLED task |
| **TC-CIN-066** | State Guard | Check-in on task whose client site is deactivated (IsActive=false) | `Task linked to inactive ClientSite` | **400 Bad Request** | CLIENT_SITE_INACTIVE: Client site is inactive |
| **TC-CIN-067** | State Guard | Check-in on task with missing or deleted client site | `Task with non-existent ClientSiteId` | **400 Bad Request** | CLIENT_SITE_INACTIVE: Client site missing |
| **TC-CIN-068** | State Guard | Check-in on task with null AssignedEmployeeId | `Task with AssignedEmployeeId=null` | **400 Bad Request** | INVALID_TASK_STATE: Task must be assigned |
| **TC-CIN-069** | State Guard | Rapid double-tap check-in from mobile app | `Rapid succession identical payload calls` | **201 then 200** | First call creates visit, second call returns existing visit |
| **TC-CIN-070** | State Guard | Idempotent retry with slightly different GPS within site | `Retry with shifted coordinates inside site` | **200 OK** | Returns existing visit without duplicate record |
| **TC-CIN-071** | State Guard | Reassigned task: Newly assigned Tech B checks in | `Task reassigned to Tech B` | **201 Created** | Tech B recognized as owner and checked in |
| **TC-CIN-072** | State Guard | Reassigned task: Previously assigned Tech A attempts check-in | `Tech A token on reassigned task` | **403 Forbidden** | TASK_ACCESS_DENIED: Tech A no longer owner |
| **TC-CIN-073** | State Guard | Check-in on task scheduled in future window (early arrival) | `Scheduled start in +3 hours` | **201 Created** | Early arrival permitted for proactive maintenance |
| **TC-CIN-074** | State Guard | Check-in on task scheduled in past window (overdue SLA) | `Scheduled end was 2 hours ago` | **201 Created** | Overdue check-in recorded for SLA audit |
| **TC-CIN-075** | State Guard | Check-in on task with URGENT priority | `Task with priority=URGENT` | **201 Created** | Urgent job check-in recorded immediately |
| **TC-CIN-076** | Security | BOLA: Field Technician A attempts check-in to Tech B's task | `Tech A token on Tech B task ID` | **403 Forbidden** | TASK_ACCESS_DENIED: Caller does not match assigned technician |
| **TC-CIN-077** | Security | BOLA: Field Technician attempts check-in with forged employeeId claim | `Forged sub/employeeId in token payload` | **401 Unauthorized** | JWT signature mismatch triggers immediate rejection |
| **TC-CIN-078** | Security | Horizontal privilege escalation: Bulk check-in probe | `Repeated probes across sequence of task IDs` | **403 Forbidden** | BOLA enforced per individual task entity |
| **TC-CIN-079** | Security | Missing Authorization header (Anonymous unauthenticated request) | `No Bearer token header` | **401 Unauthorized** | Rejected: Authentication is required |
| **TC-CIN-080** | Security | Malformed Authorization header (e.g. Basic auth) | `Authorization: Basic YWRtaW46cGFzcw==` | **401 Unauthorized** | Rejected: Bearer scheme required |
| **TC-CIN-081** | Security | Tampered JWT signature on valid payload | `Modified RS256 signature bytes` | **401 Unauthorized** | Rejected: Signature validation failed |
| **TC-CIN-082** | Security | Expired JWT token (exp in past) | `Validly formatted expired token` | **401 Unauthorized** | Rejected: Token has expired |
| **TC-CIN-083** | Security | Token signed by untrusted / unknown RSA private key | `Foreign RSA keypair token` | **401 Unauthorized** | Rejected: Untrusted issuer key |
| **TC-CIN-084** | Security | Token with algorithm header 'none' (alg: none attack) | `Header {"alg":"none","typ":"JWT"}` | **401 Unauthorized** | Rejected: Insecure algorithm prohibited |
| **TC-CIN-085** | Security | Suspended / inactive employee account token | `Inactive employee token` | **401/403 Forbidden** | Rejected: Inactive accounts barred |
| **TC-CIN-086** | Security | Token with unauthorized role (e.g. GUEST or EXTERNAL) | `Role GUEST token` | **403 Forbidden** | Forbidden: Role not authorized for visit check-in |
| **TC-CIN-087** | Security | Vertical escalation: Employee pretending to be SUPERVISOR via forged claim | `Tampered role claim` | **401 Unauthorized** | Signature validation fails on tampered payload |
| **TC-CIN-088** | Security | BOLA: Cross-technician schedule snooping after check-in | `Tech A requesting Tech B check-in details` | **403 Forbidden** | Schedule self-scoping prevents cross-read |
| **TC-CIN-089** | Security | Zero Path Parameters Defense: POST /api/v1/visits/102/check-in | `Passing ID in route path` | **404 Not Found** | Rejected: Route does not exist (Zero path standard) |
| **TC-CIN-090** | Security | Zero Path Parameters Defense: GET /api/v1/visits/check-in?taskId=102 | `GET request with query params` | **405 Method Not Allowed** | Rejected: Mutations require POST JSON body |
| **TC-CIN-091** | Resiliency | Anti-Mock Location: Flag isMockLocation=true from mock GPS app | `{"taskId":107,"latitude":12.97161,"longitude":77.59457,"isMockLocation":true}` | **400 Bad Request** | MOCK_LOCATION_DETECTED: Mock GPS detected |
| **TC-CIN-092** | Resiliency | Single Active Visit Guard: Tech checks in to Task 2 while Task 1 open | `Check-in on Task 6 while Task 5 visit open` | **409 Conflict** | ACTIVE_VISIT_EXISTS: Active visit already open |
| **TC-CIN-093** | Resiliency | Concurrent simultaneous check-ins for same technician on two tasks | `Parallel async check-in requests` | **One 201, other 409** | Database unique partial index prevents race condition |
| **TC-CIN-094** | Resiliency | SQL Injection tautology probe in taskId field | `{"taskId":"5 OR 1=1","latitude":12.97,"longitude":77.59}` | **400 Bad Request** | Strongly-typed model binder rejects injection string |
| **TC-CIN-095** | Resiliency | Stored XSS script injection probe in numeric fields | `{"taskId":5,"latitude":"<script>","longitude":77.59}` | **400 Bad Request** | Deserializer rejects non-numeric input |
| **TC-CIN-096** | Resiliency | Extremely high coordinate precision (15 decimal places) | `{"taskId":5,"latitude":12.971598765432109,"longitude":77.594562765432109}` | **201 Created** | Safely rounded to decimal(10,7) without overflow |
| **TC-CIN-097** | Resiliency | Server-authoritative timestamp: Device clock set to yesterday | `Mobile clock at 2026-10-29` | **201 Created** | CheckInTime set strictly to server DateTime.UtcNow |
| **TC-CIN-098** | Resiliency | Massive JSON payload DoS attack buffer (10MB body) | `Payload padded with 10MB whitespace` | **413 Payload Too Large** | Kestrel MaxRequestBodySize limits payload |
| **TC-CIN-099** | Resiliency | Transient database connection disconnect during commit | `Simulated transient network drop` | **500 ProblemDetails** | Handled gracefully via RFC 7807 middleware |
| **TC-CIN-100** | Resiliency | Database unique partial index constraint enforcement (IX_Visits_ActiveEmployeeVisit) | `Concurrent duplicate DB insert probe` | **409 Conflict** | DB partial unique index prevents duplicate active visits |
