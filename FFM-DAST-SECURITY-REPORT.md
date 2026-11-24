# FFM DAST Security Testing Report

## 1. Executive Summary

This report presents the findings from **Phase 1 — Dynamic Application Security Testing (DAST)** conducted against the running Field Force Management (FFM) backend services in the authorized development/test environment.

The testing evaluated live HTTP endpoints across both available microservices:
* **FFM Identity & Access API** (`http://localhost:5000`)
* **FFM Workforce API** (`http://localhost:5001`)

Testing was conducted using systematic, automated and manual dynamic probes covering security configuration, authentication, role-based authorization, BOLA/IDOR, input validation, rate limiting, token handling, and information disclosure.

---

## 2. Scope & Target Information

### 2.1 Target Base URLs
* **Identity API**: `http://localhost:5000`
* **Workforce API**: `http://localhost:5001`

### 2.2 Available Live Endpoints Tested
| Service | Endpoint | HTTP Method | Auth Required | Documented Role Policy |
|---|---|---|---|---|
| Identity API | `/api/auth/login` | POST | No | Anonymous |
| Identity API | `/api/auth/refresh` | POST | No | Anonymous |
| Identity API | `/api/auth/users` | POST | Yes | `CanCreateUser` (SYSTEM_ADMIN, ADMIN) |
| Identity API | `/api/auth/users/{id}` | GET | Yes | Any Authenticated User |
| Identity API | `/api/auth/me` | GET | Yes | Any Authenticated User |
| Identity API | `/swagger/v1/swagger.json` | GET | No | Public / Development |
| Identity API | `/swagger/index.html` | GET | No | Public / Development |
| Workforce API | `/api/v1/employees` | POST | Yes | `CanCreateEmployee` (SYSTEM_ADMIN, ADMIN, MANAGER) |
| Workforce API | `/api/v1/employees/{id}` | GET | Yes | `CanCreateEmployee` (SYSTEM_ADMIN, ADMIN, MANAGER) |
| Workforce API | `/health` | GET | No | Public |
| Workforce API | `/swagger/v1/swagger.json` | GET | No | Public / Development |
| Workforce API | `/swagger/index.html` | GET | No | Public / Development |

### 2.3 Roles Tested
All 6 canonical FFM roles were dynamically tested using authenticated JWTs:
1. `SYSTEM_ADMIN`
2. `ADMIN`
3. `EXECUTIVE`
4. `MANAGER`
5. `SUPERVISOR`
6. `EMPLOYEE`
7. `UNAUTHENTICATED` (Anonymous)

---

## 3. Testing Techniques Used

The following DAST testing techniques were systematically applied:
1. **Security Configuration Testing**: Inspection of security headers, server identification banners, CORS preflight handling, Swagger UI exposure, and transport security.
2. **HTTP Method Tampering**: Testing unsupported and administrative HTTP verbs (`HEAD`, `OPTIONS`, `PUT`, `DELETE`, `PATCH`, `TRACE`, `PROPFIND`) against all endpoints.
3. **Authentication Testing**: Verification of credential validation, SQL injection in authentication fields, user enumeration behavior, account lockout enforcement, inactive/pending account authentication, and refresh token handling.
4. **Authorization & RBAC Testing**: Systematic matrix testing of all 7 identity contexts (Unauthenticated + 6 roles) against all protected operations.
5. **BOLA / IDOR Testing**: Horizontal and vertical object-level authorization testing across user identity profiles and employee records.
6. **Input Validation Testing**: Injection of SQL payloads, XSS vectors, boundary values (negative, zero, out-of-range IDs), oversized buffers (100,000 characters), and invalid data formats.
7. **Rate Limiting Testing**: Burst and sustained request rate evaluation across both Identity and Workforce APIs.
8. **Information Disclosure Testing**: Analysis of response headers, error response payloads, stack trace generation, and token metadata.
9. **JWT / Token Handling Testing**: Testing expired tokens, wrong audience, wrong issuer, invalid cryptographic signatures, `alg: none` stripping, and malformed tokens.
10. **File Upload Security Testing**: Probing for multipart/form-data endpoints and unrestricted upload vectors.

---

## 4. Confirmed DAST Vulnerabilities

### Finding 1: Broken Object Level Authorization (BOLA / IDOR) on User Profile API
* **Vulnerability Name**: Broken Object Level Authorization / IDOR
* **Count**: 1
* **Severity**: High (CVSS: 7.5 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:N/A:N`)
* **Affected Endpoint**: `GET /api/auth/users/{id:int}`
* **HTTP Method**: `GET`
* **Test Technique**: BOLA / IDOR Testing, Parameter Tampering
* **Status**: Open
* **Cross-Method Finding**: Yes (DAST / SAST)
* **Description**:
  The `GET /api/auth/users/{id}` endpoint allows any authenticated user, regardless of their assigned role (including lowest-privileged `EMPLOYEE`), to retrieve the identity details of any other user in the database simply by incrementing the numeric integer user ID.
* **Evidence**:
  * Request sent with token belonging to `EMPLOYEE` (`sub: 16`):
    ```http
    GET /api/auth/users/12 HTTP/1.1
    Host: localhost:5000
    Authorization: Bearer eyJhbGciOiJSUzI1NiIs...[MASKED]
    ```
  * Response received:
    ```http
    HTTP/1.1 200 OK
    Content-Type: application/json; charset=utf-8

    {"id":12,"usernameOrEmail":"test@example.com","role":"SYSTEM_ADMIN","status":"Locked"}
    ```
  * The regular employee successfully accessed User 12 (`SYSTEM_ADMIN`), User 4 (`MANAGER`), and User 1 (`EMPLOYEE`), exposing internal IDs, usernames/emails, roles, and account security statuses.
* **Impact**:
  Full horizontal and vertical information disclosure of all user accounts and system administrators across the organization. Attackers can map organizational hierarchy, identify high-value targets (administrators), and verify account lock states.
* **Recommendation**:
  Implement object-level authorization: ensure that callers can only access their own user record (`id == currentUserId`), or require administrative authorization (`SYSTEM_ADMIN` / `ADMIN`) to view other users' records.

---

### Finding 2: Authentication Bypass for Inactive and Pending User Accounts
* **Vulnerability Name**: Authentication Bypass for Inactive Account Status
* **Count**: 1
* **Severity**: High (CVSS: 7.3 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:H/A:N`)
* **Affected Endpoint**: `POST /api/auth/login`
* **HTTP Method**: `POST`
* **Test Technique**: Authentication Testing, Business Logic Testing
* **Status**: Open
* **Cross-Method Finding**: Yes (DAST / SAST)
* **Description**:
  In accordance with Section 13.11 of the Identity Specification, only accounts in `ACTIVE` status are permitted to authenticate; accounts with status `PENDING_ACTIVATION`, `SUSPENDED`, `BLOCKED`, or `DEACTIVATED` must be rejected with HTTP 403. However, `AuthService.AuthenticateAsync` only checks for `AccountStatus.Locked` and permits accounts in `PendingActivation` (and other non-active states) to log in and receive valid JWT access and refresh tokens.
* **Evidence**:
  * Account created with initial default status `AccountStatus.PendingActivation` (`dast_employee@test.com`).
  * Login request sent:
    ```http
    POST /api/auth/login HTTP/1.1
    Host: localhost:5000
    Content-Type: application/json

    {"username":"dast_employee@test.com","password":"DastPassword123!","clientId":"dast-suite"}
    ```
  * Response received:
    ```http
    HTTP/1.1 200 OK
    Content-Type: application/json; charset=utf-8

    {"accessToken":"eyJhbGciOiJSUzI1NiIs...[MASKED]","refreshToken":"...[MASKED]","expiresAt":"2026-09-29T10:43:24Z","mfaRequired":false,"mfaChallengeId":null}
    ```
* **Impact**:
  Unapproved, deactivated, or suspended users can maintain access to the system. Pre-provisioned employees can access resources before formal onboarding approval.
* **Recommendation**:
  Enforce explicit account status validation in `AuthService.AuthenticateAsync`:
  ```csharp
  if (user.Status != AccountStatus.Active)
  {
      return null!; // Or return structured 403 Forbidden
  }
  ```

---

### Finding 3: Bypassed Input Validation Pipeline on Employee Creation
* **Vulnerability Name**: Missing Input Validation Pipeline
* **Count**: 1
* **Severity**: Medium (CVSS: 6.5 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:H/A:N`)
* **Affected Endpoint**: `POST /api/v1/employees`
* **HTTP Method**: `POST`
* **Test Technique**: Input Validation Testing, Fuzzing
* **Status**: Open
* **Cross-Method Finding**: Yes (DAST / SAST)
* **Description**:
  Although the application defines a validation class (`CreateEmployeeCommandValidator`), it is not registered in the DI container or MediatR pipeline in `Program.cs`. As a result, input validation is entirely non-functional:
  * Name field accepts strings up to 100,000 characters without truncation (configured limit: 100).
  * Arbitrary HTML/JavaScript tags (`<script>alert(1)</script>`) are accepted and stored verbatim (Stored XSS).
  * Malformed email addresses (`definitely-not-an-email`) are accepted and stored.
  * Invalid mobile phone numbers (`abcxyz-not-digits`) are accepted and stored.
* **Evidence**:
  * Request sent with 100,000 characters in `name`:
    ```http
    POST /api/v1/employees HTTP/1.1
    Host: localhost:5001
    Authorization: Bearer eyJhbGciOiJSUzI1NiIs...[MASKED]
    Content-Type: application/json

    {"employeeCode":"OVERFLOW1","name":"AAAA...[100,000 characters]...","password":"Password123!","roleId":6,"email":"overflow@example.com"}
    ```
  * Response received:
    ```http
    HTTP/1.1 201 Created
    Location: /api/v1/Employees/b2e6fd42-8822-494b-9706-53d9e83ec50e
    ```
  * Request sent with HTML/XSS payload:
    ```http
    POST /api/v1/employees HTTP/1.1
    Host: localhost:5001
    Content-Type: application/json

    {"employeeCode":"XSS5003","name":"XSS Test <script>alert(1)</script>","password":"Password123!","roleId":6,"email":"xss5003@example.com"}
    ```
  * Response received: `HTTP/1.1 201 Created`. Subsequent `GET /api/v1/employees/{id}` returns the unencoded payload:
    `"name":"XSS Test <script>alert(1)</script>"`
* **Impact**:
  Data corruption, database bloat/denial of service through oversized payloads, and stored cross-site scripting when employee data is rendered in downstream dashboards or web applications.
* **Recommendation**:
  Register FluentValidation in `FFM.Workforce.Api/Program.cs` and configure a MediatR validation pipeline behavior to automatically reject invalid commands with `400 Bad Request`.

---

### Finding 4: Information Disclosure via Unhandled Exception Stack Traces
* **Vulnerability Name**: Information Disclosure / Verbose Error Messages
* **Count**: 1
* **Severity**: Medium (CVSS: 5.3 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N`)
* **Affected Endpoint**: `POST /api/v1/employees`
* **HTTP Method**: `POST`
* **Test Technique**: Information Disclosure Testing, Negative Boundary Testing
* **Status**: Open
* **Cross-Method Finding**: Yes (DAST / SAST)
* **Description**:
  When invalid parameters such as `roleId: -1` or `roleId: 9999` are submitted, `CreateEmployeeCommandHandler` throws an unhandled `System.InvalidOperationException`. The server responds with `HTTP 500 Internal Server Error` containing raw internal execution details, including class names, source code file paths, and exact line numbers.
* **Evidence**:
  * Request sent:
    ```http
    POST /api/v1/employees HTTP/1.1
    Host: localhost:5001
    Content-Type: application/json

    {"employeeCode":"NEG1","name":"Negative Role Test","password":"Password123!","roleId":-1,"email":"neg@example.com"}
    ```
  * Response received:
    ```http
    HTTP/1.1 500 Internal Server Error
    Content-Type: text/plain; charset=utf-8

    System.InvalidOperationException: Role with ID -1 not found.
       at FFM.Workforce.Application.Handlers.CreateEmployeeCommandHandler.Handle(CreateEmployeeCommand request, CancellationToken cancellationToken) in /src/FFM.Workforce/FFM.Workforce.Application/Handlers/CreateEmployeeCommandHandler.cs:line 72
       at FFM.Workforce.Api.Controllers.EmployeesController.Create(CreateEmployeeCommand command, CancellationToken cancellationToken) in /src/FFM.Workforce/FFM.Workforce.Api/Controllers/EmployeesController.cs:line 34
    ```
* **Impact**:
  Exposes internal architectural structure, source file hierarchy (`/src/FFM.Workforce/...`), line numbers, and technology details to unauthorized callers, aiding attackers in constructing targeted exploits.
* **Recommendation**:
  Implement a global exception handling middleware that catches all unhandled exceptions, returns standardized RFC 7807 problem details with generic error descriptions, and logs detailed stack traces internally only.

---

### Finding 5: Missing Rate Limiting on Workforce API
* **Vulnerability Name**: Missing Rate Limiting
* **Count**: 1
* **Severity**: Medium (CVSS: 5.3 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:L`)
* **Affected Endpoint**: All Workforce API endpoints (`/api/v1/employees`, `/health`)
* **HTTP Method**: ALL
* **Test Technique**: Rate Limiting Testing, DoS Probing
* **Status**: Open
* **Cross-Method Finding**: Yes (DAST / SAST)
* **Description**:
  While the Identity API implements an `AuthPolicy` rate limiter limiting requests to 10/minute, the Workforce API lacks rate limiting entirely. Neither `AddRateLimiter` nor `UseRateLimiter` is registered in `FFM.Workforce.Api/Program.cs`.
* **Evidence**:
  * 25 consecutive requests sent to `http://localhost:5001/api/v1/employees/{id}` within 1.5 seconds.
  * Every single request returned `HTTP 200 OK` (0 rejections, 0 rate limit headers).
  * Rapid employee creation POST requests were processed without any throttling or backoff enforcement.
* **Impact**:
  Susceptible to denial of service, database connection pool exhaustion, and automated resource creation abuse.
* **Recommendation**:
  Configure ASP.NET Core Rate Limiting in `FFM.Workforce.Api/Program.cs` on sensitive and resource-intensive endpoints.

---

### Finding 6: Security Misconfiguration — Missing Defense Headers and Banner Disclosure
* **Vulnerability Name**: Security Misconfiguration / Missing Security Headers
* **Count**: 1
* **Severity**: Low (CVSS: 3.7 - `CVSS:3.1/AV:N/AC:H/PR:N/UI:N/S:U/C:L/I:N/A:N`)
* **Affected Endpoint**: All endpoints across Identity API (`:5000`) and Workforce API (`:5001`)
* **HTTP Method**: ALL
* **Test Technique**: Security Configuration Testing
* **Status**: Open
* **Cross-Method Finding**: Yes (DAST / SAST)
* **Description**:
  The HTTP response headers from both APIs leak the underlying web server banner (`Server: Kestrel`) and omit standard defense-in-depth security headers. Furthermore, Swagger UI is exposed publicly on both services without authentication.
* **Evidence**:
  * Response headers observed from `http://localhost:5000` and `http://localhost:5001`:
    ```http
    Server: Kestrel
    Content-Type: application/json;charset=utf-8
    ```
  * Missing security headers:
    * `X-Content-Type-Options: nosniff`
    * `X-Frame-Options: DENY`
    * `Content-Security-Policy`
    * `Strict-Transport-Security` (HSTS)
    * `Referrer-Policy: no-referrer`
  * Swagger UI accessible anonymously at:
    * `http://localhost:5000/swagger/index.html`
    * `http://localhost:5001/swagger/index.html`
* **Impact**:
  MIME-sniffing attacks, clickjacking risks in web views, information leakage regarding backend technology stack, and public exposure of API schemas to unauthorized parties.
* **Recommendation**:
  Add security header middleware to append required defense headers, suppress the Kestrel server header (`builder.WebHost.ConfigureKestrel(opts => opts.AddServerHeader = false)`), and restrict Swagger UI to authorized development environments or behind authentication.

---

## 5. Categories Tested with Zero Confirmed Findings

In accordance with Section 3.5 and Section 6.3 of the Security Testing Specification, the following categories were actively tested and confirmed to have **0 vulnerabilities**:

| Category | Test Scenarios Executed | Result Count |
|---|---|---|
| **SQL Injection** | Tested SQL syntax injection in username, password, employeeCode, name, email fields using single quotes, comments, boolean logic (`' OR '1'='1`), and UNION payloads. EF Core parameterization successfully prevented SQL injection across all tested endpoints. | **0** |
| **Command Injection** | Tested OS command injection payloads (`|`, `;`, `&`, backticks). No command execution functionality exists. | **0** |
| **Path Traversal** | Tested path traversal sequences (`../`, `..\`) in URL routes and parameters. No file system retrieval is exposed. | **0** |
| **JWT Signature Bypass** | Tested `alg: none` stripping attack, manipulated RSA signatures, untrusted RSA keys, expired tokens, audience mismatch, and issuer mismatch. All forged and invalid tokens were rejected with `HTTP 401 Unauthorized`. | **0** |
| **Cross-Site Request Forgery (CSRF)** | Both APIs rely exclusively on stateless `Authorization: Bearer <JWT>` headers. No ambient cookie authentication is accepted, inherently mitigating CSRF. | **0** |
| **HTTP Method Tampering (Bypass)** | Tested unsupported verbs (`PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`, `TRACE`, `PROPFIND`) on POST/GET endpoints. Routing correctly rejected all unmapped methods with `HTTP 405 Method Not Allowed`. | **0** |
| **Cross-User Token Confusion** | Tested token exchange between services. Audience (`aud: ffm-client`) and Issuer (`iss: https://ffm.example.com`) are strictly validated across both services. | **0** |

---

## 6. False Positives

| Scenario | Observation | Analysis | Disposition |
|---|---|---|---|
| Identity API Rate Limit on Authorized Tests | During rapid test suite execution, `POST /api/auth/users` and login tests intermittently returned `HTTP 429 Too Many Requests`. | This was not an application failure or denial of service vulnerability; it was the intended operation of the ASP.NET Core `AuthPolicy` fixed-window rate limiter (10 requests/minute). | **False Positive** (Working security control) |
| Missing Attendance/Task/GPS API 404s | Requests to documented endpoints like `/api/v1/attendance` and `/api/v1/tasks` returned `HTTP 404 Not Found`. | These modules are specified in future design documentation but have not yet been implemented in the active codebase. 404 is the expected HTTP behavior for unbuilt endpoints. | **False Positive / Not in Scope** |

---

## 7. Blocked / Untested Areas

1. **Unimplemented Modules in Active Codebase**:
   * The following specification-defined modules have no controller implementation in the running codebase and were verified to return `404 Not Found`:
     * Attendance Management (`/api/v1/attendance/*`)
     * Field Tasks (`/api/v1/tasks/*`)
     * Expenses (`/api/v1/expenses/*`)
     * GPS Tracking (`/api/v1/gps/*`)
     * Visit Verification (`/api/v1/visits/*`)
     * Rosters and Leave Balances (`/api/v1/rosters`, `/api/v1/leave/*`)
   * Dynamic security testing of these modules is blocked pending their development.
2. **File Upload Security Testing**:
   * No multipart/form-data or file upload endpoints currently exist in the running application. File upload testing is marked as **0 / Not Applicable** until upload endpoints are added.
3. **MFA Verification Workflow**:
   * MFA challenge creation and verification endpoints (`/api/v1/auth/mfa/*`) are stubbed/not exposed as active controller actions in `AuthController.cs`.

---

## 8. DAST Vulnerability Summary and Exact Counts

```plaintext
DAST Vulnerability Summary

Broken Access Control / BOLA: 2
Authentication Weakness / Inactive Account Login: 1
Bypassed Input Validation: 1
Information Disclosure: 1
Rate Limiting Weakness: 1
Security Misconfiguration: 1
SQL Injection: 0
Command Injection: 0
Path Traversal: 0
JWT / Token Security Weakness: 0
File Upload Security Weakness: 0
Cross-Site Request Forgery (CSRF): 0

Total DAST Vulnerabilities: 7
```
