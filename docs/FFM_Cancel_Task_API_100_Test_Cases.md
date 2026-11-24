# FFM Field Operations Service — Cancel Task API 100 Test Cases

**API Endpoint:** POST /api/v1/tasks/cancel  
**Architectural Standard:** Zero Path Parameters, RBAC Security, Strict State Machine Guard  
**Authorized Roles:** SYSTEM_ADMIN, ADMIN, MANAGER, SUPERVISOR (Blocked for EMPLOYEE)  

---

## 1. Test Cases Summary Matrix

| Category | Count | Primary Focus |
|---|:---:|---|
| **Positive / Functional** | 15 | Valid task cancellation across CREATED, ASSIGNED, EN_ROUTE, role permissions, and reason lengths |
| **State Machine Guards** | 15 | Blocking cancellation of CHECKED_IN, COMPLETED, CANCELLED tasks, and invalid task IDs |
| **Reason Validation** | 15 | Null, empty, whitespace, < 5 chars, > 500 chars, special characters, and trimming |
| **Authentication & RBAC** | 15 | Blocking EMPLOYEE role, missing JWT, expired token, malformed signature, wrong issuer/aud |
| **HTTP & Routing Standards** | 15 | Zero path parameter enforcement, method tampering (PUT, DELETE), content-type headers |
| **Security & Boundary Edge** | 25 | SQL injection, XSS payloads, concurrent cancellations, integer boundaries, unicode strings |
| **TOTAL** | **100** | **Comprehensive Full-Spectrum Test Suite** |

---

## 2. Complete 100 Test Cases Specification Table

| Test ID | Category | Scenario | Input Payload / Headers | Status | Expected Behavior |
|:---:|---|---|---|:---:|---|
| **TC-CAN-001** | Positive | Cancel task in CREATED status (unassigned work order) | {"taskId": 101, "reason": "Client cancelled quarterly inspection request."} | 200 OK | Task status transitions to CANCELLED, CancelledAt set, CancelledByUserId recorded |
| **TC-CAN-002** | Positive | Cancel task in ASSIGNED status (technician assigned) | {"taskId": 102, "reason": "Severe rainfall prevented on-site access."} | 200 OK | Task status set to CANCELLED, assigned technician schedule unblocked |
| **TC-CAN-003** | Positive | Cancel task in EN_ROUTE status (technician traveling) | {"taskId": 103, "reason": "Store manager called to reschedule to next week."} | 200 OK | Task status set to CANCELLED, technician recalled via mobile push event |
| **TC-CAN-004** | Positive | Cancel task by SYSTEM_ADMIN role | {"taskId": 104, "reason": "Emergency system maintenance cancellation."} | 200 OK | Authorized and cancelled successfully |
| **TC-CAN-005** | Positive | Cancel task by ADMIN role | {"taskId": 105, "reason": "Duplicate work order created by mistake."} | 200 OK | Authorized and cancelled successfully |
| **TC-CAN-006** | Positive | Cancel task by MANAGER role | {"taskId": 106, "reason": "Zone priority reshuffle by area manager."} | 200 OK | Authorized and cancelled successfully |
| **TC-CAN-007** | Positive | Cancel task by SUPERVISOR role | {"taskId": 107, "reason": "Technician team reallocated to critical breakdown."} | 200 OK | Authorized and cancelled successfully |
| **TC-CAN-008** | Positive | Cancellation with minimum allowed reason length (exactly 5 chars) | {"taskId": 108, "reason": "Rainy"} | 200 OK | Accepted and saved; reason exactly 5 characters |
| **TC-CAN-009** | Positive | Cancellation with medium detailed reason (150 chars) | {"taskId": 109, "reason": "Client contacted central helpline stating ATM facility power grid was down. Work deferred until electricity board restores transformer connection."} | 200 OK | Full detailed explanation persisted accurately in database |
| **TC-CAN-010** | Positive | Cancellation with maximum allowed reason length (exactly 500 chars) | {"taskId": 110, "reason": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"} | 200 OK | Successfully accepted at upper boundary (500 chars) |
| **TC-CAN-011** | Positive | Reason with special characters and punctuation | {"taskId": 111, "reason": "Client's AC #3 unit replaced; visit isn't required (Ref: TKT-991 & WO#44)."} | 200 OK | Punctuation and symbols safely stored |
| **TC-CAN-012** | Positive | Reason with multilingual Unicode text (Tamil / Hindi) | {"taskId": 112, "reason": "மழை காரணமாக பணி ரத்து செய்யப்பட்டது (Work cancelled due to heavy rain)."} | 200 OK | UTF-8 preserved in database |
| **TC-CAN-013** | Positive | Reason with numbers and hyphens | {"taskId": 113, "reason": "Rescheduled to 2026-11-30 per customer phone call #8849."} | 200 OK | Alphanumeric reason accepted |
| **TC-CAN-014** | Positive | Verify response payload envelope for cancel operation | {"taskId": 114, "reason": "Valid reason exceeding five chars"} | 200 OK | Envelope returns success: true, message: 'Task cancelled successfully', data containing taskNumber and status CANCELLED |
| **TC-CAN-015** | Positive | Sequential cancellation of multiple distinct tasks | Cancel Task 115 then Task 116 sequentially | 200 OK | Both tasks independently cancelled without state pollution |
| **TC-CAN-016** | Negative | Cancel task currently CHECKED_IN (technician on site) | {"taskId": 201, "reason": "Client closed gate."} | 400 Bad Request | State Guard blocked: Active on-site visit cannot be cancelled; must complete or abort via check-out |
| **TC-CAN-017** | Negative | Cancel task currently COMPLETED (finished task) | {"taskId": 202, "reason": "Client dissatisfied with completed work."} | 400 Bad Request | State Guard blocked: Completed task cannot be cancelled retrospectively |
| **TC-CAN-018** | Negative | Cancel task currently already CANCELLED | {"taskId": 203, "reason": "Second cancel attempt."} | 400 Bad Request | Idempotency Guard: Task is already cancelled |
| **TC-CAN-019** | Negative | Cancel non-existent taskId (id = 999999) | {"taskId": 999999, "reason": "Non-existent task ID cancel."} | 404 Not Found | KeyNotFoundException: Task with ID 999999 not found |
| **TC-CAN-020** | Negative | Cancel with taskId = 0 | {"taskId": 0, "reason": "Zero taskId validation test."} | 400 Bad Request / 404 | Validation error: TaskId must be a positive integer > 0 |
| **TC-CAN-021** | Negative | Cancel with negative taskId (id = -1) | {"taskId": -1, "reason": "Negative taskId validation test."} | 400 Bad Request / 404 | Validation error: TaskId must be greater than 0 |
| **TC-CAN-022** | Negative | Cancel with large negative taskId (id = -9999) | {"taskId": -9999, "reason": "Large negative taskId test."} | 400 Bad Request | Validation error: TaskId cannot be negative |
| **TC-CAN-023** | Negative | Cancel with 64-bit integer taskId exceeding Int32 max | {"taskId": 3000000000, "reason": "Int64 overflow cancel test."} | 400 Bad Request | JSON deserialization integer overflow error |
| **TC-CAN-024** | Negative | Cancel with maximum signed 32-bit int taskId (2147483647) | {"taskId": 2147483647, "reason": "Valid reason for max int."} | 404 Not Found | Parses cleanly without crash, returns 404 Not Found |
| **TC-CAN-025** | Negative | Cancel with minimum signed 32-bit int taskId (-2147483648) | {"taskId": -2147483648, "reason": "Valid reason for min int."} | 400 Bad Request | Negative integer validation error |
| **TC-CAN-026** | Negative | Cancel with floating point taskId (id = 101.5) | {"taskId": 101.5, "reason": "Float taskId validation test."} | 400 Bad Request | JSON parsing type mismatch (decimal to integer conversion failed) |
| **TC-CAN-027** | Negative | Cancel with string taskId (id = '101') | {"taskId": "101", "reason": "String quoted taskId test."} | 200 OK / 400 Bad Request | Binds safely to integer or fails model binding gracefully |
| **TC-CAN-028** | Negative | Cancel with alphabetical string taskId (id = 'abc') | {"taskId": "abc", "reason": "Alpha taskId validation test."} | 400 Bad Request | JSON parsing failure: 'abc' cannot be converted to integer |
| **TC-CAN-029** | Negative | Cancel with missing taskId field in payload | {"reason": "Missing taskId entirely."} | 400 Bad Request | Validation error: TaskId is required |
| **TC-CAN-030** | Negative | Cancel with null taskId field | {"taskId": null, "reason": "Null taskId test."} | 400 Bad Request | Validation error: TaskId is required |
| **TC-CAN-031** | Negative | Reason field is null | {"taskId": 101, "reason": null} | 400 Bad Request | Validation error: Cancellation reason is required |
| **TC-CAN-032** | Negative | Reason field is empty string | {"taskId": 101, "reason": ""} | 400 Bad Request | Validation error: Cancellation reason is required |
| **TC-CAN-033** | Negative | Reason field is whitespace only | {"taskId": 101, "reason": "   "} | 400 Bad Request | Validation error: Cancellation reason must be between 5 and 500 characters |
| **TC-CAN-034** | Negative | Reason is 1 character long ('A') | {"taskId": 101, "reason": "A"} | 400 Bad Request | Validation error: Cancellation reason minimum length is 5 characters |
| **TC-CAN-035** | Negative | Reason is 2 characters long ('No') | {"taskId": 101, "reason": "No"} | 400 Bad Request | Validation error: Cancellation reason minimum length is 5 characters |
| **TC-CAN-036** | Negative | Reason is 3 characters long ('Bad') | {"taskId": 101, "reason": "Bad"} | 400 Bad Request | Validation error: Cancellation reason minimum length is 5 characters |
| **TC-CAN-037** | Negative | Reason is exactly 4 characters long ('Done') | {"taskId": 101, "reason": "Done"} | 400 Bad Request | Validation error: Cancellation reason minimum length is 5 characters |
| **TC-CAN-038** | Negative | Reason exceeds 500 characters (501 chars) | {"taskId": 101, "reason": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"} | 400 Bad Request | Validation error: Cancellation reason cannot exceed 500 characters |
| **TC-CAN-039** | Negative | Reason exceeds 500 characters (1000 chars) | {"taskId": 101, "reason": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"} | 400 Bad Request | Validation error: Cancellation reason cannot exceed 500 characters |
| **TC-CAN-040** | Negative | Reason with leading & trailing spaces trimmed < 5 chars | {"taskId": 101, "reason": "  ok  "} | 400 Bad Request | Validation error: Trimmed reason is shorter than 5 characters |
| **TC-CAN-041** | Negative | Reason as numeric integer (12345) | {"taskId": 101, "reason": 12345} | 200 OK / 400 Bad Request | Converts to string or fails type validation |
| **TC-CAN-042** | Negative | Reason as empty JSON object ({}) | {"taskId": 101, "reason": {}} | 400 Bad Request | JSON type conversion error |
| **TC-CAN-043** | Negative | Reason as JSON array ([]) | {"taskId": 101, "reason": []} | 400 Bad Request | JSON type conversion error |
| **TC-CAN-044** | Negative | Reason as boolean literal (true) | {"taskId": 101, "reason": true} | 400 Bad Request | Type mismatch on string property |
| **TC-CAN-045** | Negative | Missing reason field entirely from JSON | {"taskId": 101} | 400 Bad Request | Validation error: Cancellation reason is required |
| **TC-CAN-046** | Negative | EMPLOYEE role attempts cancellation | Valid payload with EMPLOYEE token | 403 Forbidden | Access denied: Field technicians are not authorized to cancel work orders |
| **TC-CAN-047** | Negative | Assigned technician attempts self-cancellation | Valid payload with assigned technician token | 403 Forbidden | Field technician prohibited from administrative cancellation |
| **TC-CAN-048** | Negative | Missing Authorization header entirely | POST /api/v1/tasks/cancel without Authorization header | 401 Unauthorized | Access denied: Missing JWT bearer token |
| **TC-CAN-049** | Negative | Empty Authorization header value | Authorization: '' | 401 Unauthorized | Access denied: Empty authorization credentials |
| **TC-CAN-050** | Negative | Authorization header missing 'Bearer ' prefix | Authorization: eyJhbGciOi... | 401 Unauthorized | Malformed header: Scheme missing |
| **TC-CAN-051** | Negative | Expired JWT token | Authorization: Bearer <expired_token> | 401 Unauthorized | Token expired: Lifetime validation failed |
| **TC-CAN-052** | Negative | JWT signed with invalid private key | Authorization: Bearer <forged_token> | 401 Unauthorized | Signature validation failed |
| **TC-CAN-053** | Negative | JWT with wrong audience | Authorization: Bearer <wrong_aud_token> | 401 Unauthorized | Audience validation failed |
| **TC-CAN-054** | Negative | JWT with wrong issuer | Authorization: Bearer <wrong_iss_token> | 401 Unauthorized | Issuer validation failed |
| **TC-CAN-055** | Negative | Malformed JWT string | Authorization: Bearer invalid.jwt.string | 401 Unauthorized | Malformed JWT format |
| **TC-CAN-056** | Negative | Algorithm 'none' attack in JWT header | Authorization: Bearer <alg_none_token> | 401 Unauthorized | Algorithm 'none' rejected by JWT policy |
| **TC-CAN-057** | Negative | Revoked token check | Authorization: Bearer <revoked_token> | 401 Unauthorized | Token has been revoked |
| **TC-CAN-058** | Negative | Suspended user token | Authorization: Bearer <suspended_user_token> | 401 Unauthorized | User account is suspended |
| **TC-CAN-059** | Negative | Guest or invalid role in token | Authorization: Bearer <guest_token> | 403 Forbidden / 401 | Unauthorized role for task cancellation |
| **TC-CAN-060** | Negative | Token missing 'sub' (User ID) claim | Authorization: Bearer <token_no_sub> | 401 Unauthorized | Subject claim missing from token |
| **TC-CAN-061** | Negative | SQL Injection in reason: ' OR 1=1;-- | {"taskId": 101, "reason": "Client cancel' OR 1=1;--"} | 200 OK | Safely parameterized by EF Core; treated as literal string |
| **TC-CAN-062** | Negative | SQL Injection with DROP TABLE: '; DROP TABLE Tasks;-- | {"taskId": 101, "reason": "Cancel; DROP TABLE Tasks;--"} | 200 OK | Safely parameterized; no execution of embedded SQL commands |
| **TC-CAN-063** | Negative | SQL Injection with time delay: '; WAITFOR DELAY '0:0:5';-- | {"taskId": 101, "reason": "Cancel; WAITFOR DELAY '0:0:5';--"} | 200 OK | Treated as raw text; zero execution latency delay |
| **TC-CAN-064** | Negative | SQL Injection in taskId field: 101 OR 1=1 | {"taskId": "101 OR 1=1", "reason": "Valid reason exceeding 5 chars"} | 400 Bad Request | JSON parsing failure: Expected integer value for taskId |
| **TC-CAN-065** | Negative | XSS attack in reason: <script>alert('XSS')</script> | {"taskId": 101, "reason": "<script>alert('XSS')</script>"} | 200 OK | Sanitized and stored as plain text without HTML execution |
| **TC-CAN-066** | Negative | XSS attack with image onerror payload | {"taskId": 101, "reason": "<img src=x onerror=alert(1)>"} | 200 OK | Sanitized safely; stored as inert string |
| **TC-CAN-067** | Negative | Path traversal in reason: ../../etc/passwd | {"taskId": 101, "reason": "../../etc/passwd cancelled."} | 200 OK | Stored as plain text; zero file system access |
| **TC-CAN-068** | Negative | Server-Side Template Injection (SSTI): ${{7*7}} | {"taskId": 101, "reason": "${{7*7}} customer cancelled."} | 200 OK | Stored as literal text without expression evaluation |
| **TC-CAN-069** | Negative | CRLF injection in reason payload | {"taskId": 101, "reason": "Reason  Injected-Header: bad"} | 200 OK | Newline sanitized; zero HTTP header injection |
| **TC-CAN-070** | Negative | Null byte injection in reason: Client%00cancelled | {"taskId": 101, "reason": "Client\u0000cancelled"} | 400 Bad Request / 200 OK | Null byte rejected or sanitized by JSON parser |
| **TC-CAN-071** | Negative | Command injection attempt: reason \| cat /etc/passwd | {"taskId": 101, "reason": "Cancel \| cat /etc/passwd"} | 200 OK | Treated as literal string |
| **TC-CAN-072** | Negative | URL path parameter violation: POST /api/v1/tasks/101/cancel | POST /api/v1/tasks/101/cancel with empty body | 404 Not Found | Zero Path Parameter standard enforced; route does not exist |
| **TC-CAN-073** | Negative | Unsupported HTTP method: GET /api/v1/tasks/cancel | GET /api/v1/tasks/cancel | 405 Method Not Allowed | Allow header indicates POST only |
| **TC-CAN-074** | Negative | Unsupported HTTP method: PUT /api/v1/tasks/cancel | PUT /api/v1/tasks/cancel | 405 Method Not Allowed | Method not allowed |
| **TC-CAN-075** | Negative | Unsupported HTTP method: DELETE /api/v1/tasks/cancel | DELETE /api/v1/tasks/cancel | 405 Method Not Allowed | Method not allowed |
| **TC-CAN-076** | Negative | Unsupported HTTP method: PATCH /api/v1/tasks/cancel | PATCH /api/v1/tasks/cancel | 405 Method Not Allowed | Method not allowed |
| **TC-CAN-077** | Negative | Empty request body `{}` sent | POST /api/v1/tasks/cancel with body {} | 400 Bad Request | Validation error: taskId and reason are required |
| **TC-CAN-078** | Negative | Content-Type header: text/plain instead of application/json | POST with Content-Type: text/plain | 415 Unsupported Media Type | Server requires application/json |
| **TC-CAN-079** | Negative | Malformed JSON payload (missing closing brace) | {"taskId": 101, "reason": "Unclosed string | 400 Bad Request | Malformed JSON syntax rejected by Kestrel |
| **TC-CAN-080** | Negative | Massive request body exceeding size limit (20MB) | POST payload padded to 20MB | 413 Payload Too Large | Kestrel request body size limit enforced |
| **TC-CAN-081** | Edge | Concurrent requests: Two simultaneous cancels for the same task | Two parallel POST /tasks/cancel calls | 200 OK / 400 Bad Request | First request succeeds (200), second rejected as already CANCELLED (400) |
| **TC-CAN-082** | Edge | Race condition: Cancel arrives simultaneously with Visit Check-In | Cancel & Check-In executed concurrently | 200 OK / 400 Bad Request | Database transaction isolation guarantees clean serial outcome |
| **TC-CAN-083** | Edge | Race condition: Cancel arrives simultaneously with Task Reassignment | Cancel & Reassign executed concurrently | 200 OK / 400 Bad Request | Transaction isolation maintains consistent entity state |
| **TC-CAN-084** | Edge | Task cancellation on task scheduled in the distant future (Year 2030) | {"taskId": 301, "reason": "Future work order deferred."} | 200 OK | Future scheduled tasks cancelled safely |
| **TC-CAN-085** | Edge | Task cancellation on overdue task (scheduledEnd in past, still ASSIGNED) | {"taskId": 302, "reason": "Overdue task closed administrative clean-up."} | 200 OK | Overdue pending task cleanly marked CANCELLED |
| **TC-CAN-086** | Edge | Reason containing URL link: 'Customer requested at https://portal.com' | {"taskId": 101, "reason": "Ticket logged at https://clientportal.com/req/123."} | 200 OK | URL syntax preserved in cancellation reason |
| **TC-CAN-087** | Edge | Reason containing multi-line text with newline characters | {"taskId": 101, "reason": "Line 1: Customer called. Line 2: Gate locked."} | 200 OK | Newlines preserved in database without corrupting JSON |
| **TC-CAN-088** | Edge | Reason containing double quotes escaped properly | {"taskId": 101, "reason": "Customer stated \"not required today\""} | 200 OK | Escaped quotes parsed and saved correctly |
| **TC-CAN-089** | Edge | Task with NULL assignedEmployeeId (unassigned) cancelled | {"taskId": 303, "reason": "Unassigned work order obsolete."} | 200 OK | Cancelled cleanly with assignedEmployeeId remaining null |
| **TC-CAN-090** | Edge | Task with assignedEmployeeId populated cancelled | {"taskId": 304, "reason": "Assigned technician shift reassigned."} | 200 OK | Cancelled cleanly; technician schedule released |
| **TC-CAN-091** | Edge | Task with URGENT priority cancelled | {"taskId": 305, "reason": "Emergency escalated to external vendor."} | 200 OK | High/Urgent priority tasks cancelled with high-visibility audit event |
| **TC-CAN-092** | Edge | Task with LOW priority cancelled | {"taskId": 306, "reason": "Low priority routine check skipped this month."} | 200 OK | Low priority task cancelled |
| **TC-CAN-093** | Edge | Rapid sequential cancel requests in tight loop (10 calls) | 10 rapid requests for same task | 200 OK then 400s | First call succeeds, remaining 9 reject cleanly with 400 |
| **TC-CAN-094** | Edge | Verify UpdatedAt timestamp is refreshed on cancellation | {"taskId": 101, "reason": "Valid reason exceeding five chars"} | 200 OK | UpdatedAt > CreatedAt reflects current UTC timestamp |
| **TC-CAN-095** | Edge | Verify CancelledByUserId matches authenticated token sub | {"taskId": 101, "reason": "Valid reason exceeding five chars"} | 200 OK | CancelledByUserId strictly matches actor's JWT subject |
| **TC-CAN-096** | Edge | Database glitch / temporary connection interruption during cancel | POST during DB connection reset | 503 / 500 | Handled gracefully by error handling middleware |
| **TC-CAN-097** | Edge | Cancellation Token triggered mid-request | Client disconnects before DB commit | 499 / Aborted | CancellationToken cancels async task cleanly |
| **TC-CAN-098** | Edge | Security response headers verification | Inspect response headers | 200 OK | X-Content-Type-Options: nosniff present, Kestrel banner suppressed |
| **TC-CAN-099** | Edge | Verify zero hard-deletion: Task record remains in database | Query database after cancel | 200 OK | Row still exists with status CANCELLED (soft lifecycle transition) |
| **TC-CAN-100** | Edge | X-Correlation-ID tracing header propagation | X-Correlation-ID: 9f8e7d6c-5b4a-3210-9876-fedcba098765 | 200 OK | Correlation ID echoed back in response header for distributed tracing |
