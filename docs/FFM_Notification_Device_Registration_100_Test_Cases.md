# FFM Notification Service — Device Registration API 100 Test Cases

**API Endpoint:** `POST /api/v1/Notifications/devices/register` & `POST /api/v1/Notifications/devices/deregister`  
**Architectural Standard:** Zero Path Parameters, RFC 7807, RS256 JWT Security, Single-Active Handset Enforcement  
**Authorized Roles:** `EMPLOYEE`, `SUPERVISOR`, `MANAGER`, `ADMIN`, `SYSTEM_ADMIN`  

---

## 1. Test Cases Summary Matrix

| Category | Count | Primary Focus |
|---|:---:|---|
| **1. Positive / Functional** | 15 | Android & iOS registrations, token rotation, handset handover, logout deregister, active device lookup |
| **2. Push Token Validation** | 15 | Null, empty, whitespace, < 50 chars, > 500 chars, token string sanitization, special symbols |
| **3. Platform & Hardware ID** | 15 | Platform enum validation (`ANDROID`, `IOS`, `WEB`), invalid platforms, missing/empty/long `DeviceId` |
| **4. Authentication & Security** | 15 | Zero-Trust JWT claim extraction, missing token, expired JWT, signature tampering, BOLA spoofing defense |
| **5. HTTP & Routing Standards** | 15 | Zero path parameter enforcement, method tampering (PUT/DELETE/GET), Content-Type headers, JSON schema |
| **6. Concurrency, Edge & Stress**| 25 | Atomic upsert under high concurrency, rapid login/logout, SQL injection, XSS payloads, Unicode strings |
| **TOTAL** | **100** | **Comprehensive Full-Spectrum Test Suite** |

---

## 2. Complete 100 Test Cases Specification Table

| Test ID | Category | Scenario Description | Input Payload / Headers | Status | Expected Behavior |
|:---:|---|---|---|:---:|---|
| **TC-DEV-001** | Positive | Register new Android device with valid FCM token | `{"deviceId": "dev-001", "platform": "ANDROID", "deviceModel": "Samsung S23", "osVersion": "Android 14", "appVersion": "1.0.0", "fcmToken": "<150_char_token>"}` | 200 OK | Device registered, `IsActive=true`, `EmployeeId` bound from JWT |
| **TC-DEV-002** | Positive | Register new iOS device with valid APNs token | `{"deviceId": "idfv-002", "platform": "IOS", "deviceModel": "iPhone 15 Pro", "osVersion": "iOS 17.4", "appVersion": "1.0.0", "fcmToken": "<150_char_token>"}` | 200 OK | iOS device registered successfully with `Platform="IOS"` |
| **TC-DEV-003** | Positive | Register Web portal browser push device | `{"deviceId": "web-003", "platform": "WEB", "deviceModel": "Chrome 122", "osVersion": "Windows 11", "appVersion": "2.0.0", "fcmToken": "<150_char_token>"}` | 200 OK | Web client registered successfully for browser push alerts |
| **TC-DEV-004** | Positive | Existing device token refresh (silent token rotation) | Same `deviceId` sent with fresh `fcmToken` and updated `appVersion` | 200 OK | `FcmToken` and `LastActiveAt` updated atomically; no duplicate rows created |
| **TC-DEV-005** | Positive | Handset handover (new employee logs into shared device) | Employee 102 logs into `deviceId="dev-001"` previously used by Employee 101 | 200 OK | Hardware record reassigned to Employee 102; previous mapping revoked |
| **TC-DEV-006** | Positive | Single-Active Policy: Register replacement phone | Employee 101 registers `deviceId="dev-002"`; previously had `dev-001` active | 200 OK | `dev-002` set to `Active=true`; older `dev-001` automatically deactivated |
| **TC-DEV-007** | Positive | Employee logout deregistration | `POST /api/v1/Notifications/devices/deregister` with `{"deviceId": "dev-001"}` | 200 OK | Device `IsActive` flipped to `false`; push alerts suspended |
| **TC-DEV-008** | Positive | Idempotent logout deregistration | Call `/deregister` a second time on an already deactivated device | 200 OK | Returns 200 OK without errors; state remains inactive |
| **TC-DEV-009** | Positive | Query active registered device | `GET /api/v1/Notifications/devices/active` with valid Bearer JWT | 200 OK | Returns the active device metadata, model, OS, and registration date |
| **TC-DEV-010** | Positive | Lowercase platform normalization | `{"platform": "android", ...}` | 200 OK | Backend normalizes to uppercase `ANDROID` and accepts payload |
| **TC-DEV-011** | Positive | Mixed-case platform normalization | `{"platform": "iOs", ...}` | 200 OK | Backend normalizes to uppercase `IOS` and accepts payload |
| **TC-DEV-012** | Positive | Minimum allowed FCM token length (exactly 50 chars) | `fcmToken` of exactly 50 alphanumeric characters | 200 OK | Accepted at lower boundary |
| **TC-DEV-013** | Positive | Medium standard FCM token length (152 chars standard) | Standard base64 Firebase token format | 200 OK | Successfully persisted |
| **TC-DEV-014** | Positive | Maximum allowed FCM token length (exactly 500 chars) | `fcmToken` string of exactly 500 characters | 200 OK | Accepted at upper boundary |
| **TC-DEV-015** | Positive | Alphanumeric DeviceId with dashes and underscores | `{"deviceId": "SSAID_8849-AF32_01"}` | 200 OK | Accepted and persisted cleanly |
| **TC-DEV-016** | Push Token | Null FCM token | `{"fcmToken": null}` | 400 Bad Request | Validation error: `FcmToken is required` |
| **TC-DEV-017** | Push Token | Empty string FCM token | `{"fcmToken": ""}` | 400 Bad Request | Validation error: `FcmToken is required` |
| **TC-DEV-018** | Push Token | Whitespace-only FCM token | `{"fcmToken": "     "}` | 400 Bad Request | Validation error: `FcmToken is required` |
| **TC-DEV-019** | Push Token | Short FCM token (< 50 chars, e.g. 10 chars) | `{"fcmToken": "short_1234"}` | 400 Bad Request | Validation error: `FcmToken must be at least 50 characters` |
| **TC-DEV-020** | Push Token | Short FCM token (exactly 49 chars) | `{"fcmToken": "<49_chars>"}` | 400 Bad Request | Validation error: Boundary check failed (under 50 chars) |
| **TC-DEV-021** | Push Token | Excessively long FCM token (501 chars) | `{"fcmToken": "<501_chars>"}` | 400 Bad Request | Validation error: `FcmToken cannot exceed 500 characters` |
| **TC-DEV-022** | Push Token | Massive token string (2000 chars) | `{"fcmToken": "<2000_chars>"}` | 400 Bad Request | Validation error: Maximum length violation |
| **TC-DEV-023** | Push Token | Missing `fcmToken` property from JSON | Payload omitting `"fcmToken"` key | 400 Bad Request | Validation error: `FcmToken is required` |
| **TC-DEV-024** | Push Token | Numeric literal passed as token | `{"fcmToken": 1234567890}` | 400 Bad Request | JSON model binding type mismatch error |
| **TC-DEV-025** | Push Token | Boolean literal passed as token | `{"fcmToken": true}` | 400 Bad Request | JSON model binding type mismatch error |
| **TC-DEV-026** | Push Token | JSON object passed as token | `{"fcmToken": {"key": "val"}}` | 400 Bad Request | JSON deserialization conversion failure |
| **TC-DEV-027** | Push Token | JSON array passed as token | `{"fcmToken": ["tok1", "tok2"]}` | 400 Bad Request | JSON deserialization conversion failure |
| **TC-DEV-028** | Push Token | Token with newline characters | `{"fcmToken": "line1\nline2..."}` | 400 Bad Request | Invalid token formatting |
| **TC-DEV-029** | Push Token | Token with tab characters | `{"fcmToken": "tab1\ttab2..."}` | 400 Bad Request | Invalid token formatting |
| **TC-DEV-030** | Push Token | Standard Firebase colon-delimited token | `{"fcmToken": "f7dE:APA91bF8h_jK3..."}` | 200 OK | Colons and underscores accepted |
| **TC-DEV-031** | Platform & Hardware | Unsupported platform: WINDOWS_PHONE | `{"platform": "WINDOWS_PHONE"}` | 400 Bad Request | Validation error: `Unsupported device platform` |
| **TC-DEV-032** | Platform & Hardware | Unsupported platform: BLACKBERRY | `{"platform": "BLACKBERRY"}` | 400 Bad Request | Validation error: `Unsupported device platform` |
| **TC-DEV-033** | Platform & Hardware | Unsupported platform: SYMBIAN | `{"platform": "SYMBIAN"}` | 400 Bad Request | Validation error: `Unsupported device platform` |
| **TC-DEV-034** | Platform & Hardware | Null platform value | `{"platform": null}` | 400 Bad Request | Validation error: `Platform is required` |
| **TC-DEV-035** | Platform & Hardware | Empty string platform | `{"platform": ""}` | 400 Bad Request | Validation error: `Platform is required` |
| **TC-DEV-036** | Platform & Hardware | Whitespace-only platform | `{"platform": "   "}` | 400 Bad Request | Validation error: `Platform is required` |
| **TC-DEV-037** | Platform & Hardware | Numeric platform value | `{"platform": 1}` | 400 Bad Request | Type mismatch on platform string |
| **TC-DEV-038** | Platform & Hardware | Null DeviceId value | `{"deviceId": null}` | 400 Bad Request | Validation error: `DeviceId is required` |
| **TC-DEV-039** | Platform & Hardware | Empty string DeviceId | `{"deviceId": ""}` | 400 Bad Request | Validation error: `DeviceId is required` |
| **TC-DEV-040** | Platform & Hardware | Whitespace-only DeviceId | `{"deviceId": "   "}` | 400 Bad Request | Validation error: `DeviceId is required` |
| **TC-DEV-041** | Platform & Hardware | Long DeviceId exceeding 100 chars (101 chars) | `{"deviceId": "<101_chars>"}` | 400 Bad Request | Validation error: `DeviceId cannot exceed 100 characters` |
| **TC-DEV-042** | Platform & Hardware | Maximum allowed DeviceId (exactly 100 chars) | `{"deviceId": "<100_chars>"}` | 200 OK | Accepted at upper boundary |
| **TC-DEV-043** | Platform & Hardware | Missing DeviceModel string | `{"deviceModel": ""}` | 400 Bad Request | Validation error: `DeviceModel is required` |
| **TC-DEV-044** | Platform & Hardware | Missing OsVersion string | `{"osVersion": ""}` | 400 Bad Request | Validation error: `OsVersion is required` |
| **TC-DEV-045** | Platform & Hardware | Missing AppVersion string | `{"appVersion": ""}` | 400 Bad Request | Validation error: `AppVersion is required` |
| **TC-DEV-046** | Auth & Security | Missing Authorization header | Request sent without Bearer token | 401 Unauthorized | Access denied: Missing token |
| **TC-DEV-047** | Auth & Security | Empty Authorization header value | `Authorization: ""` | 401 Unauthorized | Access denied: Missing token |
| **TC-DEV-048** | Auth & Security | Bearer prefix omitted | `Authorization: <raw_jwt_string>` | 401 Unauthorized | Malformed authorization header |
| **TC-DEV-049** | Auth & Security | Expired JWT token | Bearer token with expired `exp` claim | 401 Unauthorized | Token lifetime validation failed |
| **TC-DEV-050** | Auth & Security | Forged JWT signature | Token signed with unknown private key | 401 Unauthorized | Signature validation failed |
| **TC-DEV-051** | Auth & Security | Wrong JWT Issuer | Token with unrecognized `iss` claim | 401 Unauthorized | Issuer validation failed |
| **TC-DEV-052** | Auth & Security | Wrong JWT Audience | Token with unrecognized `aud` claim | 401 Unauthorized | Audience validation failed |
| **TC-DEV-053** | Auth & Security | Zero-Trust: Payload contains malicious employeeId | Payload: `{"employeeId": 999, ...}` | 200 OK | Ignored; backend uses authenticated JWT claim (`sub`), preventing spoofing |
| **TC-DEV-054** | Auth & Security | Non-numeric `sub` claim in JWT | Token with `sub="anonymous"` | 401 Unauthorized | Cannot resolve positive employeeId |
| **TC-DEV-055** | Auth & Security | Negative `sub` claim in JWT | Token with `sub="-5"` | 401 Unauthorized | Invalid employee identity |
| **TC-DEV-056** | Auth & Security | Zero `sub` claim in JWT | Token with `sub="0"` | 401 Unauthorized | Invalid employee identity |
| **TC-DEV-057** | Auth & Security | Deregister device belonging to another employee | Employee 102 attempts to deregister Employee 101's device | 200 OK | BOLA protection: Ignores request without affecting other user |
| **TC-DEV-058** | Auth & Security | Role: EMPLOYEE authorized | Valid EMPLOYEE JWT calls register | 200 OK | Permitted |
| **TC-DEV-059** | Auth & Security | Role: SUPERVISOR authorized | Valid SUPERVISOR JWT calls register | 200 OK | Permitted |
| **TC-DEV-060** | Auth & Security | Role: ADMIN authorized | Valid ADMIN JWT calls register | 200 OK | Permitted |
| **TC-DEV-061** | HTTP & Routing | Path parameter tampering on register | `POST /api/v1/Notifications/devices/register/101` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-DEV-062** | HTTP & Routing | Path parameter tampering on deregister | `POST /api/v1/Notifications/devices/deregister/dev-1` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-DEV-063** | HTTP & Routing | Query string parameter tampering | `POST /api/v1/Notifications/devices/register?deviceId=1` | 400 Bad Request | Rejects empty body; requires JSON body |
| **TC-DEV-064** | HTTP & Routing | GET request to register endpoint | `GET /api/v1/Notifications/devices/register` | 405 Method Not Allowed | Method tampering blocked |
| **TC-DEV-065** | HTTP & Routing | PUT request to register endpoint | `PUT /api/v1/Notifications/devices/register` | 405 Method Not Allowed | Method tampering blocked |
| **TC-DEV-066** | HTTP & Routing | DELETE request to register endpoint | `DELETE /api/v1/Notifications/devices/register` | 405 Method Not Allowed | Method tampering blocked |
| **TC-DEV-067** | HTTP & Routing | GET request to deregister endpoint | `GET /api/v1/Notifications/devices/deregister` | 405 Method Not Allowed | Method tampering blocked |
| **TC-DEV-068** | HTTP & Routing | Content-Type text/plain | `Content-Type: text/plain` with JSON body | 415 Unsupported Media Type | Requires application/json |
| **TC-DEV-069** | HTTP & Routing | Content-Type application/xml | `Content-Type: application/xml` | 415 Unsupported Media Type | Requires application/json |
| **TC-DEV-070** | HTTP & Routing | Empty request body on register | `POST /register` with body `{}` | 400 Bad Request | Required fields missing |
| **TC-DEV-071** | HTTP & Routing | Malformed JSON (syntax error, missing bracket) | `{"deviceId": "dev-1", ...` | 400 Bad Request | JSON parse error |
| **TC-DEV-072** | HTTP & Routing | Trailing comma in JSON body | `{"deviceId": "dev-1", }` | 400 Bad Request | JSON syntax error |
| **TC-DEV-073** | HTTP & Routing | Duplicate JSON keys in payload | `{"deviceId": "dev-1", "deviceId": "dev-2"}` | 200/400 | Deserializes cleanly to last value or rejects duplicate |
| **TC-DEV-074** | HTTP & Routing | Extra unknown JSON properties | `{"extraField": "ignored", ...}` | 200 OK | Safely ignores unmapped properties |
| **TC-DEV-075** | HTTP & Routing | Null body payload | `POST /register` with `null` | 400 Bad Request | `Request body cannot be empty` |
| **TC-DEV-076** | Edge & Stress | High-concurrency upsert (same device, 100 simultaneous requests) | 100 parallel register calls with identical `deviceId` | 200 OK | Database handles concurrency; exactly 1 record persisted |
| **TC-DEV-077** | Edge & Stress | Concurrent register and deregister | 1 thread registering while 1 thread deregistering | 200 OK | Atomic operations prevent database corruption |
| **TC-DEV-078** | Edge & Stress | SQL Injection attempt in DeviceId | `{"deviceId": "dev-01'; DROP TABLE \"EmployeeDevices\";--"}` | 400/200 | Parameterized EF Core query neutralizes SQL injection safely |
| **TC-DEV-079** | Edge & Stress | SQL Injection attempt in FcmToken | `{"fcmToken": "'; SELECT * FROM users; --" + padding}` | 200 OK | Parameterized query stores string literally without execution |
| **TC-DEV-080** | Edge & Stress | XSS Script injection in DeviceModel | `{"deviceModel": "<script>alert('XSS')</script>"}` | 200 OK | Encoded and stored as safe plain string |
| **TC-DEV-081** | Edge & Stress | XSS Script injection in OsVersion | `{"osVersion": "<img src=x onerror=alert(1)>"}` | 200 OK | Encoded and stored safely |
| **TC-DEV-082** | Edge & Stress | Unicode emoji in DeviceModel | `{"deviceModel": "📱 iPhone 15 Pro 🚀"}` | 200 OK | Full UTF-8 support preserved in database |
| **TC-DEV-083** | Edge & Stress | Multilingual DeviceModel (Tamil / Japanese) | `{"deviceModel": "தொலைபேசி モデル"}` | 200 OK | Unicode text stored and returned accurately |
| **TC-DEV-084** | Edge & Stress | Deregister non-existent device | `{"deviceId": "non-existent-device-uuid"}` | 200 OK | Idempotent success (returns true) |
| **TC-DEV-085** | Edge & Stress | Query active device when user has no active devices | `GET /active` for newly created employee | 404 Not Found | Returns `DEVICE_NOT_FOUND` |
| **TC-DEV-086** | Edge & Stress | Rapid login/logout cycle (10 times) | Register -> Deregister -> Register -> Deregister (10x) | 200 OK | `IsActive` state toggles accurately without leaking memory |
| **TC-DEV-087** | Edge & Stress | AppVersion with semantic release tags | `{"appVersion": "2.4.1-beta.3+build.20261110"}` | 200 OK | Semantic versioning format supported |
| **TC-DEV-088** | Edge & Stress | OsVersion with custom ROM name | `{"osVersion": "LineageOS 21.0-20261108"}` | 200 OK | Alphanumeric strings accepted up to 50 chars |
| **TC-DEV-089** | Edge & Stress | Very large valid FCM token (499 chars) | Valid token string of exactly 499 chars | 200 OK | Persisted accurately |
| **TC-DEV-090** | Edge & Stress | Very small valid DeviceId (1 char: 'X') | `{"deviceId": "X"}` | 200 OK | Single character identifier accepted |
| **TC-DEV-091** | Edge & Stress | Null Island defense verification | Module operates independently of GPS coordinates | 200 OK | Pure notification telemetry |
| **TC-DEV-092** | Edge & Stress | Verify RFC 7807 problem details format on validation error | Missing token error response check | 400 Bad Request | Contains `type`, `title`, `status`, `detail`, `errorCode` |
| **TC-DEV-093** | Edge & Stress | Verify UTC timestamp in API response envelope | Response inspection | 200 OK | Timestamp is in ISO 8601 UTC format (`...Z`) |
| **TC-DEV-094** | Edge & Stress | Verify `success: true` boolean in success response | Response inspection | 200 OK | Standard envelope compliant |
| **TC-DEV-095** | Edge & Stress | Re-registering already active device with identical token | Re-register same device without changing token | 200 OK | Updates `LastActiveAt` heartbeat timestamp |
| **TC-DEV-096** | Edge & Stress | Switch platform for same deviceId (e.g. tablet dual-boot) | Same deviceId registered first as ANDROID then WEB | 200 OK | Platform updated and old platform mapping reconciled |
| **TC-DEV-097** | Edge & Stress | Verify `LastActiveAt` updates on refresh | Register device, wait 1 sec, re-register | 200 OK | `LastActiveAt` increases to current time |
| **TC-DEV-098** | Edge & Stress | Multiple devices on different platforms for same employee | Employee has 1 ANDROID and 1 WEB device simultaneously | 200 OK | Single-Active applies per platform; both can remain active |
| **TC-DEV-099** | Edge & Stress | Deregistering with empty deviceId field | `{"deviceId": ""}` | 400 Bad Request | `DeviceId is required` |
| **TC-DEV-100** | Edge & Stress | Zero Path Parameter contract integrity across all endpoints | Audit all 3 endpoints for path variables | 200 OK | Zero path parameters; full compliance verified |

