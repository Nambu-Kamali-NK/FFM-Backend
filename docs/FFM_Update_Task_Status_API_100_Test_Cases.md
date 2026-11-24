# FFM Field Operations Service — Update Task Status API 100 Test Cases

**API Endpoint:** `POST /api/v1/tasks/status`  
**Target Status:** `EN_ROUTE` (Start Travel)  
**Architectural Standard:** Zero Path Parameters, BOLA Ownership Defense, Role Authorization, Strict Lifecycle State Machine  
**Authorized Roles:** `EMPLOYEE` (Self-Only with BOLA Guard), `SUPERVISOR`, `MANAGER`, `ADMIN`, `SYSTEM_ADMIN`  

---

## 1. Test Cases Summary Matrix

| Category | Count | Primary Focus |
|---|:---:|---|
| **Positive & Functional** | 15 | Valid departure travel transitions from ASSIGNED to EN_ROUTE, role execution, notes length, and idempotent mobile retries |
| **State Machine Guards** | 15 | Blocking departure transitions for CREATED (unassigned), CHECKED_IN (on-site), COMPLETED (finished), CANCELLED tasks, and non-existent IDs |
| **Status & Notes Validation** | 15 | Null, empty, whitespace, unrecognized status values (DRIVING, COMPLETED), case normalization, and notes length limits (<= 500 chars) |
| **BOLA & RBAC Security** | 15 | Blocking technicians from updating peer workloads (403 Forbidden), supervisory access, missing/expired JWT, signature spoofing |
| **HTTP & Routing Standards** | 15 | Zero path parameter enforcement, method tampering (GET, PUT, DELETE), Content-Type handling, and rate limiting (429) |
| **Security, Concurrency & Resilience** | 25 | SQL injection, XSS payloads in notes, concurrent mobile retries, unicode/emoji notes, integer boundaries, server-authoritative timestamps |
| **TOTAL** | **100** | **Comprehensive Full-Spectrum Test Suite** |

---

## 2. Complete 100 Test Cases Specification Table

| Test ID | Category | Scenario | Input Payload / Headers | Status | Expected Behavior |
|:---:|---|---|---|:---:|---|
| **TC-STS-001** | Positive | Technician transitions assigned task from ASSIGNED to EN_ROUTE | `{"taskId": 101, "status": "EN_ROUTE"}` (Caller: Assigned Technician ID 12) | 200 OK | Task status transitions to `EN_ROUTE`, `UpdatedAt` timestamp refreshed to UTC now |
| **TC-STS-002** | Positive | Technician transitions task to EN_ROUTE with short travel notes | `{"taskId": 102, "status": "EN_ROUTE", "notes": "Departing hub."}` | 200 OK | Status updated to `EN_ROUTE`, travel notes persisted |
| **TC-STS-003** | Positive | Technician transitions task to EN_ROUTE with detailed travel notes (250 chars) | `{"taskId": 103, "status": "EN_ROUTE", "notes": "Departed south distribution center with replacement biometric scanner and wiring harnesses. Estimated travel duration 35 minutes via Outer Ring Road."}` | 200 OK | Detailed notes persisted cleanly in audit trail |
| **TC-STS-004** | Positive | Technician transitions task with maximum allowed notes length (500 chars) | `{"taskId": 104, "status": "EN_ROUTE", "notes": "<500 characters string>"}` | 200 OK | Accepted at upper boundary (exactly 500 chars) |
| **TC-STS-005** | Positive | Technician transitions task with null notes | `{"taskId": 105, "status": "EN_ROUTE", "notes": null}` | 200 OK | Status updated to `EN_ROUTE`, notes stored as null |
| **TC-STS-006** | Positive | Technician transitions task without notes field in body | `{"taskId": 106, "status": "EN_ROUTE"}` | 200 OK | Status updated to `EN_ROUTE` cleanly |
| **TC-STS-007** | Positive | Status update initiated by SUPERVISOR role for team technician | `{"taskId": 107, "status": "EN_ROUTE", "notes": "Supervisor manually marked en route per dispatch call"}` | 200 OK | Supervisor authorized to update team member's task status |
| **TC-STS-008** | Positive | Status update initiated by MANAGER role | `{"taskId": 108, "status": "EN_ROUTE"}` (Caller: MANAGER) | 200 OK | Manager authorized to update status across zone |
| **TC-STS-009** | Positive | Status update initiated by ADMIN role | `{"taskId": 109, "status": "EN_ROUTE"}` (Caller: ADMIN) | 200 OK | Admin authorized globally to transition task status |
| **TC-STS-010** | Positive | Status update initiated by SYSTEM_ADMIN role | `{"taskId": 110, "status": "EN_ROUTE"}` (Caller: SYSTEM_ADMIN) | 200 OK | Platform-wide administrative authority honored |
| **TC-STS-011** | Positive | Idempotent re-request when task is already in EN_ROUTE status | `{"taskId": 101, "status": "EN_ROUTE"}` (Already in EN_ROUTE) | 200 OK | Idempotent success: Returns 200 OK with current state without error |
| **TC-STS-012** | Positive | Case-insensitive status value normalization ("en_route") | `{"taskId": 112, "status": "en_route"}` | 200 OK | Normalized to `EN_ROUTE` and saved |
| **TC-STS-013** | Positive | Mixed-case status value normalization ("En_Route") | `{"taskId": 113, "status": "En_Route"}` | 200 OK | Normalized to `EN_ROUTE` and saved |
| **TC-STS-014** | Positive | Status string with leading and trailing whitespace | `{"taskId": 114, "status": "  EN_ROUTE  "}` | 200 OK | Trimmed, normalized, and saved successfully |
| **TC-STS-015** | Positive | Verify response payload envelope contains complete task metadata | `{"taskId": 115, "status": "EN_ROUTE"}` | 200 OK | Envelope returns `success: true`, `message`, `data` containing `taskNumber`, `clientSiteCode`, `status: "EN_ROUTE"` |
| **TC-STS-016** | State Guard | Attempt to start travel on unassigned task in CREATED status | `{"taskId": 201, "status": "EN_ROUTE"}` (Task has no assigned technician) | 400 Bad Request | `INVALID_TASK_STATE`: Cannot start travel on an unassigned task |
| **TC-STS-017** | State Guard | Attempt to transition task currently in CHECKED_IN status (already on site) | `{"taskId": 202, "status": "EN_ROUTE"}` | 400 Bad Request | `INVALID_TASK_STATE`: Cannot transition to EN_ROUTE from CHECKED_IN |
| **TC-STS-018** | State Guard | Attempt to transition task currently in COMPLETED status | `{"taskId": 203, "status": "EN_ROUTE"}` | 400 Bad Request | `INVALID_TASK_STATE`: Cannot transition to EN_ROUTE from terminal COMPLETED |
| **TC-STS-019** | State Guard | Attempt to transition task currently in CANCELLED status | `{"taskId": 204, "status": "EN_ROUTE"}` | 400 Bad Request | `INVALID_TASK_STATE`: Cannot transition to EN_ROUTE from CANCELLED |
| **TC-STS-020** | State Guard | Non-existent taskId (id = 999999) | `{"taskId": 999999, "status": "EN_ROUTE"}` | 404 Not Found | `TASK_NOT_FOUND`: Task with ID 999999 was not found |
| **TC-STS-021** | State Guard | Task ID is zero | `{"taskId": 0, "status": "EN_ROUTE"}` | 400 Bad Request | Validation error: Valid TaskId is required |
| **TC-STS-022** | State Guard | Task ID is negative integer (id = -1) | `{"taskId": -1, "status": "EN_ROUTE"}` | 400 Bad Request | Validation error: Valid TaskId is required |
| **TC-STS-023** | State Guard | Large negative Task ID (id = -99999) | `{"taskId": -99999, "status": "EN_ROUTE"}` | 400 Bad Request | Validation error: Valid TaskId is required |
| **TC-STS-024** | State Guard | 64-bit integer Task ID exceeding Int32 maximum | `{"taskId": 3000000000, "status": "EN_ROUTE"}` | 400 Bad Request | JSON deserialization integer overflow error |
| **TC-STS-025** | State Guard | Maximum signed 32-bit integer Task ID (2147483647) | `{"taskId": 2147483647, "status": "EN_ROUTE"}` | 404 Not Found | Parses cleanly without crash, returns 404 Not Found |
| **TC-STS-026** | State Guard | Minimum signed 32-bit integer Task ID (-2147483648) | `{"taskId": -2147483648, "status": "EN_ROUTE"}` | 400 Bad Request | Negative integer validation error |
| **TC-STS-027** | State Guard | Floating point Task ID (id = 101.5) | `{"taskId": 101.5, "status": "EN_ROUTE"}` | 400 Bad Request | JSON parsing type mismatch (decimal to integer conversion failed) |
| **TC-STS-028** | State Guard | Quoted numeric string Task ID (id = "101") | `{"taskId": "101", "status": "EN_ROUTE"}` | 200 OK / 400 Bad Request | Binds safely to integer or model binding validation |
| **TC-STS-029** | State Guard | Alphabetical string Task ID (id = "abc") | `{"taskId": "abc", "status": "EN_ROUTE"}` | 400 Bad Request | JSON parsing failure: 'abc' cannot be converted to integer |
| **TC-STS-030** | State Guard | Missing taskId property in request body | `{"status": "EN_ROUTE"}` | 400 Bad Request | Validation error: TaskId is required |
| **TC-STS-031** | Validation | Status field is null | `{"taskId": 101, "status": null}` | 400 Bad Request | Validation error: Status is required |
| **TC-STS-032** | Validation | Status field is empty string | `{"taskId": 101, "status": ""}` | 400 Bad Request | Validation error: Status is required |
| **TC-STS-033** | Validation | Status field is whitespace string | `{"taskId": 101, "status": "   "}` | 400 Bad Request | Validation error: Status is required |
| **TC-STS-034** | Validation | Invalid target status: "COMPLETED" | `{"taskId": 101, "status": "COMPLETED"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-035** | Validation | Invalid target status: "CHECKED_IN" | `{"taskId": 101, "status": "CHECKED_IN"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-036** | Validation | Invalid target status: "CANCELLED" | `{"taskId": 101, "status": "CANCELLED"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-037** | Validation | Invalid target status: "DRIVING" | `{"taskId": 101, "status": "DRIVING"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-038** | Validation | Invalid target status: "ON_THE_WAY" | `{"taskId": 101, "status": "ON_THE_WAY"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-039** | Validation | Invalid target status: "STARTED" | `{"taskId": 101, "status": "STARTED"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-040** | Validation | Invalid target status: random string "FOOBAR" | `{"taskId": 101, "status": "FOOBAR"}` | 400 Bad Request | `VALIDATION_ERROR`: Permitted status for this endpoint is 'EN_ROUTE' |
| **TC-STS-041** | Validation | Notes exceeding 500 characters (501 chars) | `{"taskId": 101, "status": "EN_ROUTE", "notes": "<501 chars string>"}` | 400 Bad Request | `VALIDATION_ERROR`: Notes cannot exceed 500 characters |
| **TC-STS-042** | Validation | Notes exceeding 1000 characters | `{"taskId": 101, "status": "EN_ROUTE", "notes": "<1000 chars string>"}` | 400 Bad Request | `VALIDATION_ERROR`: Notes cannot exceed 500 characters |
| **TC-STS-043** | Validation | Notes with leading/trailing whitespace | `{"taskId": 101, "status": "EN_ROUTE", "notes": "  Traffic clear on highway.  "}` | 200 OK | Saved properly with whitespace handled |
| **TC-STS-044** | Validation | Completely empty JSON object in body | `{}` | 400 Bad Request | Validation error: TaskId and Status are required |
| **TC-STS-045** | Validation | Null request body | null payload | 400 Bad Request | `BAD_REQUEST`: Request body cannot be empty |
| **TC-STS-046** | BOLA & RBAC | Technician A (ID 8) attempts to update Technician B's task (ID 12) | `{"taskId": 101, "status": "EN_ROUTE"}` (Token: Employee ID 8, Task Assigned To: 12) | 403 Forbidden | `TASK_ACCESS_DENIED`: Caller employee ID does not match assigned technician |
| **TC-STS-047** | BOLA & RBAC | Technician attempts to update unassigned task | `{"taskId": 102, "status": "EN_ROUTE"}` (Token: Employee ID 8, Task unassigned) | 403 Forbidden | `TASK_ACCESS_DENIED`: Caller not authorized on unassigned task |
| **TC-STS-048** | BOLA & RBAC | Supervisor updates task assigned to technician in their department | `{"taskId": 103, "status": "EN_ROUTE"}` (Token: Supervisor) | 200 OK | Authorized by supervisor governance role |
| **TC-STS-049** | BOLA & RBAC | Manager updates task assigned to any technician in branch | `{"taskId": 104, "status": "EN_ROUTE"}` (Token: Manager) | 200 OK | Authorized by manager supervisory role |
| **TC-STS-050** | BOLA & RBAC | Admin updates task assigned to any technician | `{"taskId": 105, "status": "EN_ROUTE"}` (Token: Admin) | 200 OK | Authorized by global admin authority |
| **TC-STS-051** | BOLA & RBAC | Request without Authorization header | `Authorization: (none)` | 401 Unauthorized | JWT Bearer middleware rejects unauthenticated request |
| **TC-STS-052** | BOLA & RBAC | Request with malformed Bearer token | `Authorization: Bearer invalid-garbage-token-string` | 401 Unauthorized | JWT parsing failure: signature invalid |
| **TC-STS-053** | BOLA & RBAC | Request with expired JWT token | `Authorization: Bearer <expired_token>` | 401 Unauthorized | Token lifetime validation failed (`LifetimeValidationFailed`) |
| **TC-STS-054** | BOLA & RBAC | Token signed by forged RSA private key | `Authorization: Bearer <untrusted_signature_token>` | 401 Unauthorized | RSA signature verification failed (`SignatureValidationFailed`) |
| **TC-STS-055** | BOLA & RBAC | Token with invalid issuer claim | `Authorization: Bearer <wrong_issuer_token>` | 401 Unauthorized | Issuer validation failed (`IssuerValidationFailed`) |
| **TC-STS-056** | BOLA & RBAC | Token with invalid audience claim | `Authorization: Bearer <wrong_audience_token>` | 401 Unauthorized | Audience validation failed (`AudienceValidationFailed`) |
| **TC-STS-057** | BOLA & RBAC | Token missing role claim entirely | `Authorization: Bearer <token_without_roles>` | 403 Forbidden | Authorization handler denies access |
| **TC-STS-058** | BOLA & RBAC | Token with unauthorized guest role (e.g., "VENDOR_GUEST") | `Authorization: Bearer <vendor_guest_token>` | 403 Forbidden | RBAC denies access: Role not in permitted whitelist |
| **TC-STS-059** | BOLA & RBAC | Token containing lowercase role string "employee" | `Authorization: Bearer <lowercase_employee_token>` | 200 OK | Case-insensitive role matching binds successfully |
| **TC-STS-060** | BOLA & RBAC | Token containing multiple roles (`["EMPLOYEE", "SUPERVISOR"]`) | `Authorization: Bearer <multi_role_token>` | 200 OK | Highest privilege applied (Supervisor permits update) |
| **TC-STS-061** | HTTP Standards | Invoking endpoint via path parameter: `POST /api/v1/tasks/101/status` | POST to route with path param | 404 Not Found | Routing Mismatch: Endpoint strictly requires zero path parameters |
| **TC-STS-062** | HTTP Standards | Invoking endpoint via GET: `GET /api/v1/tasks/status` | GET method invocation | 405 Method Not Allowed | Method Not Allowed: Status update must be a POST request |
| **TC-STS-063** | HTTP Standards | Invoking endpoint via PUT: `PUT /api/v1/tasks/status` | PUT method invocation | 405 Method Not Allowed | Method Not Allowed |
| **TC-STS-064** | HTTP Standards | Invoking endpoint via PATCH: `PATCH /api/v1/tasks/status` | PATCH method invocation | 405 Method Not Allowed | Method Not Allowed |
| **TC-STS-065** | HTTP Standards | Invoking endpoint via DELETE: `DELETE /api/v1/tasks/status` | DELETE method invocation | 405 Method Not Allowed | Method Not Allowed |
| **TC-STS-066** | HTTP Standards | Invoking endpoint with `Content-Type: text/plain` | Body sent as raw text | 415 Unsupported Media Type | Media type rejected; `application/json` required |
| **TC-STS-067** | HTTP Standards | Invoking endpoint with `Content-Type: application/xml` | Body sent as XML | 415 Unsupported Media Type | XML format rejected |
| **TC-STS-068** | HTTP Standards | Missing `Content-Type` header | Header omitted | 415 Unsupported Media Type / 400 | Rejection of untyped request body |
| **TC-STS-069** | HTTP Standards | Malformed JSON: unclosed trailing brace `{"taskId": 101, "status": "EN_ROUTE"` | Malformed JSON | 400 Bad Request | JSON parser syntax error |
| **TC-STS-070** | HTTP Standards | Malformed JSON: trailing comma `{"taskId": 101, "status": "EN_ROUTE",}` | Trailing comma in JSON | 400 Bad Request | JSON parser syntax error |
| **TC-STS-071** | HTTP Standards | Additional unknown properties in request payload (e.g., `{"latitude": 12.9}`) | Extra unmapped JSON attributes | 200 OK | Extraneous properties ignored cleanly without model failure |
| **TC-STS-072** | HTTP Standards | Extra large payload (> 1 MB) in request body | 1 MB payload with repeated notes text | 400 Bad Request / 413 | Request size boundary or notes validation guard triggered |
| **TC-STS-073** | HTTP Standards | Rapid consecutive requests from same client IP (rate limit threshold) | 65 rapid status update requests within 60 seconds | 429 Too Many Requests | Rate limiting middleware activates (sliding window 60 req/min) |
| **TC-STS-074** | HTTP Standards | Verify response HTTP headers include security controls | Inspect response headers | 200 OK | Headers contain `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` |
| **TC-STS-075** | HTTP Standards | Verify Kestrel server banner is suppressed | Inspect `Server` response header | 200 OK | `Server` header omitted or generic (prevents framework disclosure) |
| **TC-STS-076** | Security & Edge | SQL Injection attack in notes: single quote breakout | `{"taskId": 101, "status": "EN_ROUTE", "notes": "' OR '1'='1"}` | 200 OK | EF Core parameterized query treats string as literal value safely |
| **TC-STS-077** | Security & Edge | SQL Injection attack in notes: table drop command | `{"taskId": 101, "status": "EN_ROUTE", "notes": "Departed; DROP TABLE \"Tasks\";--"}` | 200 OK | Stored safely as harmless literal string; no SQL executed |
| **TC-STS-078** | Security & Edge | SQL Injection attack in notes: boolean union injection | `{"taskId": 101, "status": "EN_ROUTE", "notes": "' UNION SELECT * FROM \"Users\"--"}` | 200 OK | Parameterized; no data leakage |
| **TC-STS-079** | Security & Edge | Cross-Site Scripting (XSS) attack in notes: basic script tag | `{"taskId": 101, "status": "EN_ROUTE", "notes": "<script>alert('XSS')</script>"}` | 200 OK | Stored safely; sanitized upon UI rendering |
| **TC-STS-080** | Security & Edge | XSS attack in notes: image onerror payload | `{"taskId": 101, "status": "EN_ROUTE", "notes": "<img src=x onerror=alert(1)>"}` | 200 OK | Stored safely without script execution |
| **TC-STS-081** | Security & Edge | XSS attack in notes: iframe injection payload | `{"taskId": 101, "status": "EN_ROUTE", "notes": "<iframe src='javascript:alert(1)'></iframe>"}` | 200 OK | Stored safely without HTML interpretation |
| **TC-STS-082** | Security & Edge | Notes containing Tamil Unicode characters | `{"taskId": 101, "status": "EN_ROUTE", "notes": "பயணம் தொடங்கியது (Travel started to customer site)"}` | 200 OK | UTF-8 encoded text persisted accurately in PostgreSQL |
| **TC-STS-083** | Security & Edge | Notes containing Hindi Unicode characters | `{"taskId": 101, "status": "EN_ROUTE", "notes": "ग्राहक स्थल के लिए यात्रा शुरू की (Travel started to customer site)"}` | 200 OK | UTF-8 Hindi characters persisted without corruption |
| **TC-STS-084** | Security & Edge | Notes containing vehicle and transit emojis | `{"taskId": 101, "status": "EN_ROUTE", "notes": "Departing on motorbike 🏍️ to ATM location 🏦 via highway 🛣️"}` | 200 OK | UTF-8 4-byte emoji sequences stored accurately |
| **TC-STS-085** | Security & Edge | Notes containing HTML encoded entities | `{"taskId": 101, "status": "EN_ROUTE", "notes": "Parts &amp; tools loaded &lt;ready&gt;"}` | 200 OK | Handled safely |
| **TC-STS-086** | Security & Edge | Notes containing newline and carriage return characters | `{"taskId": 101, "status": "EN_ROUTE", "notes": "Line 1: Departed.\nLine 2: Picking up filter."}` | 200 OK | Multi-line text preserved |
| **TC-STS-087** | Security & Edge | Notes containing tab characters and JSON escaping | `{"taskId": 101, "status": "EN_ROUTE", "notes": "Gate A\tRef: \"TKT-991\""}` | 200 OK | Escaped JSON string binds properly |
| **TC-STS-088** | Security & Edge | Server-authoritative timestamp verification | Compare `data.updatedAt` against server UTC clock | 200 OK | `UpdatedAt` generated exclusively by server (`DateTime.UtcNow`); ignores client clock |
| **TC-STS-089** | Security & Edge | Concurrent duplicate status updates on same task (race condition) | 2 concurrent POST requests with `taskId: 101` from same technician | 200 OK | Both requests resolve safely to `EN_ROUTE` via idempotency |
| **TC-STS-090** | Security & Edge | Rapid triple-tap mobile retry simulation | 3 rapid sequential calls within 200ms | 200 OK | First transitions state; second and third return idempotent 200 OK |
| **TC-STS-091** | Security & Edge | Status update on task assigned to deactivated employee | `{"taskId": 101, "status": "EN_ROUTE"}` (Assigned employee is deactivated) | 400 Bad Request | Inactive employee guard prevents workflow progression |
| **TC-STS-092** | Security & Edge | Status update on task whose client site has been deactivated | `{"taskId": 101, "status": "EN_ROUTE"}` (Linked client site `isActive = false`) | 400 Bad Request | Inactive site guard prevents dispatch progression |
| **TC-STS-093** | Security & Edge | Verify TaskDto data masking: no sensitive password or secret hashes exposed | Inspect returned `data` object | 200 OK | Response contains only public task, site, and status properties |
| **TC-STS-094** | Security & Edge | Zero path parameter standard verification: Query parameters in URL | `POST /api/v1/tasks/status?taskId=101&status=EN_ROUTE` with empty body | 400 Bad Request | Rejection: Parameters in query string are ignored; body binding fails |
| **TC-STS-095** | Security & Edge | Status update during active shift verification | Technician has checked in for attendance shift | 200 OK | Status updated successfully within active shift |
| **TC-STS-096** | Security & Edge | Attempt status update without active attendance shift | Technician has NOT checked in for attendance shift | 400 Bad Request / 200 OK | System flags shift dependency per enterprise attendance policy |
| **TC-STS-097** | Security & Edge | Response latency benchmark under standard network load | Measure roundtrip response time | 200 OK | Response time `< 150ms` (meets high-performance mobile SLA) |
| **TC-STS-098** | Security & Edge | Database transaction rollback on simulated repository fault | Force repository save exception | 500 Internal Server Error | ExceptionHandlingMiddleware returns standard ProblemDetails; no corrupt partial state |
| **TC-STS-099** | Security & Edge | Verify SignalR / Event Bus dispatch event emitted | Check background integration event | 200 OK | `TaskStatusUpdatedIntegrationEvent` dispatched with `taskId`, `status: "EN_ROUTE"`, timestamp |
| **TC-STS-100** | Security & Edge | Full end-to-end task progression cycle verification | `CREATED` ➔ `ASSIGNED` ➔ `EN_ROUTE` | 200 OK | Task successfully progresses through complete dispatch lifecycle up to departure |
