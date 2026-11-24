# FFM Workforce Service — Get Shifts API 100 Test Cases

## Module: Master Shifts Blueprints Engine
### Service: `FFM.Workforce` (Port 5010)
### Document Reference: `FFM-TC-WRK-GET-SHIFTS-100-V1.0`

**API Endpoint:** `GET /api/v1/Shifts`  
**Architectural Standards:** Zero Path Parameters, RFC 7807 (ProblemDetails), RS256 Bearer JWT, Response Caching  
**Authorized Roles:** `SYSTEM_ADMIN`, `ADMIN`, `MANAGER`, `SUPERVISOR`, `EMPLOYEE`  

---

## 1. Test Cases Summary Matrix

| Category | Count | Primary Focus |
|---|:---:|---|
| **1. Positive / Master Data Retrieval** | 15 | Default query, all 5 municipal shift blueprints (`MCT-MORN`, `MCT-EVEN`, `MCT-NGHT`, `MCT-GEN`, `MCT-OFF`), field contracts, durations, and color codes |
| **2. Query Parameter Filtering** | 15 | `isActive` (true, false, null), `includeRestDays` (true, false), combinations, case-insensitivity, and unknown parameter handling |
| **3. Overnight Crossover & Duration Math**| 15 | Midnight time window calculation (23:00 to 07:00), 24h boundaries, rest day zero duration, sub-hour increments, and arithmetic integrity |
| **4. Authentication & RBAC Policy** | 15 | Role matrix validation (`SYSTEM_ADMIN`, `ADMIN`, `MANAGER`, `SUPERVISOR`, `EMPLOYEE`), missing token, expired token, tampered signature |
| **5. HTTP, Headers & Zero Path Standards** | 15 | Prohibiting path parameters (`/api/v1/Shifts/1` $\rightarrow$ 404), HTTP method tampering (POST, PUT, DELETE), Accept negotiation, ETag / 304 |
| **6. Concurrency, Edge & Security Stress**| 25 | High-concurrency query load, SQL injection via query parameters, XSS strings, memory leakage, response latency benchmarks |
| **TOTAL** | **100** | **Comprehensive Full-Spectrum Test Suite** |

---

## 2. Complete 100 Test Cases Specification Table

| Test ID | Category | Scenario Description | Request Parameters / Headers | Status | Expected Behavior / Assertions |
|:---:|---|---|---|:---:|---|
| **TC-SHF-001** | Positive | Fetch shifts with default parameters | `GET /api/v1/Shifts`<br>Auth: Bearer JWT | 200 OK | Returns array of 5 master municipal shifts |
| **TC-SHF-002** | Positive | Verify Morning Field Shift contract | `GET /api/v1/Shifts` | 200 OK | `shiftCode: "MCT-MORN"`, `startTime: "07:00:00"`, `endTime: "15:00:00"`, `gracePeriodMinutes: 15`, `colorCode: "#1C585C"` |
| **TC-SHF-003** | Positive | Verify Evening Field Shift contract | `GET /api/v1/Shifts` | 200 OK | `shiftCode: "MCT-EVEN"`, `startTime: "15:00:00"`, `endTime: "23:00:00"`, `gracePeriodMinutes: 15`, `colorCode: "#D97706"` |
| **TC-SHF-004** | Positive | Verify Night Emergency Shift contract | `GET /api/v1/Shifts` | 200 OK | `shiftCode: "MCT-NGHT"`, `startTime: "23:00:00"`, `endTime: "07:00:00"`, `isNightShift: true`, `colorCode: "#4338CA"` |
| **TC-SHF-005** | Positive | Verify General Directorate Shift contract | `GET /api/v1/Shifts` | 200 OK | `shiftCode: "MCT-GEN"`, `startTime: "08:00:00"`, `endTime: "17:00:00"`, `gracePeriodMinutes: 15`, `colorCode: "#059669"` |
| **TC-SHF-006** | Positive | Verify Scheduled Rest Day contract | `GET /api/v1/Shifts` | 200 OK | `shiftCode: "MCT-OFF"`, `startTime: "00:00:00"`, `endTime: "00:00:00"`, `isRestDay: true`, `gracePeriodMinutes: 0`, `colorCode: "#64748B"` |
| **TC-SHF-007** | Positive | Response envelope format verification | `GET /api/v1/Shifts` | 200 OK | Response contains `success: true`, `message: "Shifts retrieved successfully."`, `data: [...]`, `timestamp` |
| **TC-SHF-008** | Positive | Duration field for daytime shift | `GET /api/v1/Shifts` | 200 OK | `MCT-MORN` returns `durationHours: 8.0` |
| **TC-SHF-009** | Positive | Duration field for general shift | `GET /api/v1/Shifts` | 200 OK | `MCT-GEN` returns `durationHours: 9.0` |
| **TC-SHF-010** | Positive | Duration field for rest day | `GET /api/v1/Shifts` | 200 OK | `MCT-OFF` returns `durationHours: 0.0` |
| **TC-SHF-011** | Positive | Sequential identical requests return identical blueprints | Send 3 sequential `GET /api/v1/Shifts` calls | 200 OK | Idempotent output across all calls |
| **TC-SHF-012** | Positive | Verify shift description presence | `GET /api/v1/Shifts` | 200 OK | Description text describes municipal work type |
| **TC-SHF-013** | Positive | Verify active flag on master blueprints | `GET /api/v1/Shifts` | 200 OK | All 5 seeded shifts have `isActive: true` |
| **TC-SHF-014** | Positive | Verify unique Shift IDs in response | `GET /api/v1/Shifts` | 200 OK | IDs are sequential integers `[1, 2, 3, 4, 5]` |
| **TC-SHF-015** | Positive | Verify hex color code format | `GET /api/v1/Shifts` | 200 OK | Every shift matches regex `^#[0-9A-Fa-f]{6}$` |
| **TC-SHF-016** | Filtering | Query active shifts explicitly (`isActive=true`) | `GET /api/v1/Shifts?isActive=true` | 200 OK | Returns all 5 active master shifts |
| **TC-SHF-017** | Filtering | Query inactive shifts (`isActive=false`) | `GET /api/v1/Shifts?isActive=false` | 200 OK | Returns empty array `[]` |
| **TC-SHF-018** | Filtering | Query with `isActive=1` (truthy integer) | `GET /api/v1/Shifts?isActive=1` | 200 OK | Binds to true; returns active shifts |
| **TC-SHF-019** | Filtering | Query with `isActive=0` (falsy integer) | `GET /api/v1/Shifts?isActive=0` | 200 OK | Binds to false; returns inactive shifts |
| **TC-SHF-020** | Filtering | Exclude rest days (`includeRestDays=false`) | `GET /api/v1/Shifts?includeRestDays=false` | 200 OK | Returns 4 items (`MCT-OFF` excluded) |
| **TC-SHF-021** | Filtering | Explicitly include rest days (`includeRestDays=true`)| `GET /api/v1/Shifts?includeRestDays=true` | 200 OK | Returns all 5 items |
| **TC-SHF-022** | Filtering | Combined: active working shifts only | `GET /api/v1/Shifts?isActive=true&includeRestDays=false` | 200 OK | Returns 4 working shifts |
| **TC-SHF-023** | Filtering | Case-insensitive parameter value (`isActive=TRUE`) | `GET /api/v1/Shifts?isActive=TRUE` | 200 OK | ASP.NET model binder parses `TRUE` cleanly |
| **TC-SHF-024** | Filtering | Case-insensitive parameter value (`includeRestDays=False`) | `GET /api/v1/Shifts?includeRestDays=False` | 200 OK | Rest days excluded |
| **TC-SHF-025** | Filtering | Unknown extra query parameters ignored | `GET /api/v1/Shifts?foo=bar&version=2` | 200 OK | Extra parameters disregarded without error |
| **TC-SHF-026** | Filtering | Empty string query parameter (`isActive=`) | `GET /api/v1/Shifts?isActive=` | 200 OK | Defaults to null; returns active shifts |
| **TC-SHF-027** | Filtering | Malformed boolean in `isActive` | `GET /api/v1/Shifts?isActive=maybe` | 400 Bad Request | Model binding validation error |
| **TC-SHF-028** | Filtering | Malformed boolean in `includeRestDays` | `GET /api/v1/Shifts?includeRestDays=yes` | 400 Bad Request | Model binding validation error |
| **TC-SHF-029** | Filtering | Repeated query parameter (`isActive=true&isActive=false`) | `GET /api/v1/Shifts?isActive=true&isActive=false` | 200 OK / 400 | ASP.NET uses first parameter or fails cleanly |
| **TC-SHF-030** | Filtering | Trailing ampersand in query string | `GET /api/v1/Shifts?isActive=true&` | 200 OK | Handled cleanly |
| **TC-SHF-031** | Midnight Math | Standard overnight calculation (23:00 to 07:00) | `ShiftCode: MCT-NGHT` | 200 OK | `(24 - 23 + 7) = 8.0` hours |
| **TC-SHF-032** | Midnight Math | Midnight crossover from 22:00 to 06:00 | Unit Entity Test | 200 OK | `(24 - 22 + 6) = 8.0` hours |
| **TC-SHF-033** | Midnight Math | Midnight crossover from 20:00 to 04:00 | Unit Entity Test | 200 OK | `(24 - 20 + 4) = 8.0` hours |
| **TC-SHF-034** | Midnight Math | Short midnight crossover (23:30 to 03:30) | Unit Entity Test | 200 OK | `4.0` hours |
| **TC-SHF-035** | Midnight Math | Midnight crossover ending exactly at midnight (16:00 to 00:00) | Unit Entity Test | 200 OK | `8.0` hours |
| **TC-SHF-036** | Midnight Math | Shift starting exactly at midnight (00:00 to 08:00) | Unit Entity Test | 200 OK | `8.0` hours |
| **TC-SHF-037** | Midnight Math | Zero duration rest day calculation | `ShiftCode: MCT-OFF` | 200 OK | `0.0` hours |
| **TC-SHF-038** | Midnight Math | Daytime fraction hour shift (08:30 to 17:00) | Unit Entity Test | 200 OK | `8.5` hours |
| **TC-SHF-039** | Midnight Math | Overnight fraction hour shift (22:30 to 06:45) | Unit Entity Test | 200 OK | `8.25` hours |
| **TC-SHF-040** | Midnight Math | Same start and end time non-rest day | Unit Entity Test: 08:00 to 08:00 | 200 OK | `0.0` hours |
| **TC-SHF-041** | Midnight Math | Shift spanning 23 hours (01:00 to 00:00) | Unit Entity Test | 200 OK | `23.0` hours |
| **TC-SHF-042** | Midnight Math | Float precision check for 1/3 hour (08:00 to 08:20) | Unit Entity Test | 200 OK | `0.3333` hours |
| **TC-SHF-043** | Midnight Math | Midnight crossover grace period calculation | `MCT-NGHT` check-in at 23:14:00 | 200 OK | Evaluated as ON_TIME within 15m grace |
| **TC-SHF-044** | Midnight Math | Midnight crossover late arrival threshold | `MCT-NGHT` check-in at 23:16:00 | 200 OK | Evaluated as LATE (exceeds 15m grace) |
| **TC-SHF-045** | Midnight Math | Overnight early checkout threshold | `MCT-NGHT` check-out at 06:45:00 | 200 OK | Evaluated as EARLY_DEPARTURE |
| **TC-SHF-046** | Auth & RBAC | Request with missing Authorization header | `GET /api/v1/Shifts` (No header) | 401 Unauthorized | Missing Bearer token rejected |
| **TC-SHF-047** | Auth & RBAC | Request with empty Bearer token | `Authorization: Bearer ` | 401 Unauthorized | Malformed authorization header |
| **TC-SHF-048** | Auth & RBAC | Request with Basic auth instead of Bearer | `Authorization: Basic dXNlcjpwYXNz` | 401 Unauthorized | Scheme rejected |
| **TC-SHF-049** | Auth & RBAC | Expired JWT token | Bearer with past `exp` claim | 401 Unauthorized | JWT expired rejected |
| **TC-SHF-050** | Auth & RBAC | Tampered JWT signature | Valid JWT with modified payload | 401 Unauthorized | RS256 signature verification fails |
| **TC-SHF-051** | Auth & RBAC | Wrong issuer (`iss` != `https://ffm.example.com`) | Bearer with rogue issuer | 401 Unauthorized | Issuer validation rejected |
| **TC-SHF-052** | Auth & RBAC | Wrong audience (`aud` != `ffm-client`) | Bearer with rogue audience | 401 Unauthorized | Audience validation rejected |
| **TC-SHF-053** | Auth & RBAC | `SYSTEM_ADMIN` role authorization | Bearer with `SYSTEM_ADMIN` | 200 OK | Full access permitted |
| **TC-SHF-054** | Auth & RBAC | `ADMIN` role authorization | Bearer with `ADMIN` | 200 OK | Full access permitted |
| **TC-SHF-055** | Auth & RBAC | `MANAGER` role authorization | Bearer with `MANAGER` | 200 OK | Full access permitted |
| **TC-SHF-056** | Auth & RBAC | `SUPERVISOR` role authorization | Bearer with `SUPERVISOR` | 200 OK | Full access permitted |
| **TC-SHF-057** | Auth & RBAC | `EMPLOYEE` role authorization | Bearer with `EMPLOYEE` | 200 OK | Field technicians permitted to view shifts |
| **TC-SHF-058** | Auth & RBAC | Token with multiple roles | Bearer with `["EMPLOYEE", "SUPERVISOR"]` | 200 OK | Permitted |
| **TC-SHF-059** | Auth & RBAC | Token without role claim | Bearer with valid user but no role | 200 OK | Authenticated access permitted |
| **TC-SHF-060** | Auth & RBAC | Untrusted public key signed token | Bearer signed by unknown RSA key | 401 Unauthorized | Key not in keystore rejected |
| **TC-SHF-061** | Standards | Path parameter prohibited (`/api/v1/Shifts/1`) | `GET /api/v1/Shifts/1` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-SHF-062** | Standards | Method tampering: POST to query route | `POST /api/v1/Shifts` | 405 Method Not Allowed | Read-only endpoint rejects mutation verbs |
| **TC-SHF-063** | Standards | Method tampering: PUT to query route | `PUT /api/v1/Shifts` | 405 Method Not Allowed | Method not allowed |
| **TC-SHF-064** | Standards | Method tampering: DELETE to query route | `DELETE /api/v1/Shifts` | 405 Method Not Allowed | Method not allowed |
| **TC-SHF-065** | Standards | Method tampering: PATCH to query route | `PATCH /api/v1/Shifts` | 405 Method Not Allowed | Method not allowed |
| **TC-SHF-066** | Standards | HEAD request to query route | `HEAD /api/v1/Shifts` | 200 OK | Returns headers without body |
| **TC-SHF-067** | Standards | OPTIONS request for CORS preflight | `OPTIONS /api/v1/Shifts` | 204 No Content | CORS preflight headers returned |
| **TC-SHF-068** | Standards | Accept header `application/json` | `Accept: application/json` | 200 OK | JSON response returned |
| **TC-SHF-069** | Standards | Accept header `*/*` wildcard | `Accept: */*` | 200 OK | JSON response returned |
| **TC-SHF-070** | Standards | Unsupported Accept header (`application/xml`) | `Accept: application/xml` | 406 Not Acceptable / 200 | Returns JSON or 406 |
| **TC-SHF-071** | Standards | Content-Type header on GET request ignored | `Content-Type: application/json` | 200 OK | GET accepts headers cleanly |
| **TC-SHF-072** | Standards | Request body on GET request ignored | `GET /api/v1/Shifts` with body `{"dummy":1}` | 200 OK | Body safely ignored per HTTP spec |
| **TC-SHF-073** | Standards | Trailing slash in URL (`/api/v1/Shifts/`) | `GET /api/v1/Shifts/` | 200 OK / 301 | Handled or redirected cleanly |
| **TC-SHF-074** | Standards | Case sensitivity in route (`/api/v1/shifts`) | `GET /api/v1/shifts` | 200 OK | Case-insensitive URL matching |
| **TC-SHF-075** | Standards | Response caching header verification | Inspect response headers | 200 OK | Cache-Control / ETag present |
| **TC-SHF-076** | Edge & Security| SQL injection in `isActive` parameter | `GET /api/v1/Shifts?isActive=true' OR 1=1--` | 400 Bad Request | Blocked by model binder without SQL execution |
| **TC-SHF-077** | Edge & Security| SQL injection in `includeRestDays` parameter | `GET /api/v1/Shifts?includeRestDays=true; DROP TABLE "Shifts";` | 400 Bad Request | Model binder blocks invalid boolean string |
| **TC-SHF-078** | Edge & Security| XSS payload in parameter value | `GET /api/v1/Shifts?isActive=<script>alert(1)</script>` | 400 Bad Request | Model binder blocks payload |
| **TC-SHF-079** | Edge & Security| Extremely long query string (10,000 chars) | `GET /api/v1/Shifts?param=<10KB>` | 414 URI Too Long | Web server restricts excessive URI length |
| **TC-SHF-080** | Edge & Security| Null byte injection in query parameter | `GET /api/v1/Shifts?isActive=true%00` | 400 Bad Request | Blocked cleanly |
| **TC-SHF-081** | Edge & Security| High-concurrency burst (100 simultaneous queries) | 100 parallel `GET /api/v1/Shifts` | 200 OK | 100% success rate without memory leaks |
| **TC-SHF-082** | Edge & Security| Response time SLA benchmark | Measure latency across 50 requests | 200 OK | P95 latency < 50ms |
| **TC-SHF-083** | Edge & Security| Database connection pool exhaustion resilience | Rapid queries during DB load | 200 OK | Connection pool recycles cleanly |
| **TC-SHF-084** | Edge & Security| Response size SLA check | Measure payload byte size | 200 OK | Payload size < 5KB |
| **TC-SHF-085** | Edge & Security| Unicode characters in unused query parameters | `GET /api/v1/Shifts?search=مسقط` | 200 OK | UTF-8 query parsed without crash |
| **TC-SHF-086** | Edge & Security| Rate limiting policy enforcement | Exceed configured rate limit (60 req/min) | 429 Too Many Requests | Rate limiter triggers gracefully |
| **TC-SHF-087** | Edge & Security| Rate limit header verification | Inspect headers on request | 200 OK | `X-RateLimit-*` headers present |
| **TC-SHF-088** | Edge & Security| Memory stability during continuous polling | Poll endpoint every 100ms for 30s | 200 OK | Process memory footprint remains stable |
| **TC-SHF-089** | Edge & Security| Server header suppression check | Inspect response headers | 200 OK | `Server: Kestrel` header suppressed |
| **TC-SHF-090** | Edge & Security| Security headers verification | Inspect headers | 200 OK | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` |
| **TC-SHF-091** | Edge & Security| Content-Security-Policy header check | Inspect headers | 200 OK | `CSP` header populated per spec |
| **TC-SHF-092** | Edge & Security| Database restart recovery check | Query immediately after PostgreSQL container reload | 200 OK | EF Core reconnects automatically |
| **TC-SHF-093** | Edge & Security| Empty database table fallback | Table empty test condition | 200 OK | Returns empty array `[]` without null reference |
| **TC-SHF-094** | Edge & Security| Multiple active shifts with identical timings | Add second 07:00-15:00 shift | 200 OK | Both shifts returned with distinct codes and IDs |
| **TC-SHF-095** | Edge & Security| Shift with null optional description | Create shift with null description | 200 OK | `description: null` serialized cleanly |
| **TC-SHF-096** | Edge & Security| Shift with maximum description length (255 chars) | Create shift with 255-char description | 200 OK | Full description returned without truncation |
| **TC-SHF-097** | Edge & Security| Color code with lowercase letters (`#1c585c`) | Verify lowercase hex color | 200 OK | Serialized accurately |
| **TC-SHF-098** | Edge & Security| Zero grace period non-rest shift | Shift with `gracePeriodMinutes: 0` | 200 OK | Integer 0 serialized cleanly |
| **TC-SHF-099** | Edge & Security| Large grace period shift (60 minutes) | Shift with `gracePeriodMinutes: 60` | 200 OK | Accepted and serialized cleanly |
| **TC-SHF-100** | Edge & Security| End-to-end integration with test JWT | Use generated token for `test@example.com` | 200 OK | Live end-to-end verification successful |
