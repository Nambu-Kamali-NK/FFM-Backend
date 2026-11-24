# FFM Penetration Testing Security Report

## 1. Executive Summary

This report documents the results of **Phase 3 — Penetration Testing** conducted on the Field Force Management (FFM) application and running test environment in accordance with [`Docs/FFM_Security_Testing_Spec.md`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/Docs/FFM_Security_Testing_Spec.md).

The primary objective of this phase was to determine whether security weaknesses in the FFM APIs and business logic could be actively exploited in a live environment, and to validate their real-world impact across authentication, authorization, session management, data isolation, and user privilege boundaries.

Testing was performed safely against the authorized development/test Docker container stack:
* **Identity API**: `http://localhost:5000`
* **Workforce API**: `http://localhost:5001`
* **PostgreSQL Database**: Port `5434` (Container: `ffm-identity-access-db`)
* **Redis Cache**: Port `6379` (Container: `ffm-identity-access-redis`)

A total of **12 penetration testing scenarios** were systematically executed. A total of **8 confirmed exploitable security vulnerabilities** were identified and validated with reproducible evidence.

---

## 2. Scope & Environment

### 2.1 Scope
* **Target Services**:
  * `FFM.Identity.Access.Api` (.NET 8.0 / ASP.NET Core Web API)
  * `FFM.Workforce.Api` (.NET 8.0 / ASP.NET Core Web API with MediatR & EF Core)
* **Tested Core Functions**:
  * Authentication workflows (`POST /api/auth/login`, `POST /api/auth/refresh`)
  * User profile access (`GET /api/auth/users/{id}`, `GET /api/auth/me`)
  * User provisioning (`POST /api/auth/users`)
  * Employee management workflows (`POST /api/v1/Employees`, `GET /api/v1/Employees/{id}`)
  * Privilege boundaries and Role-Based Access Control (RBAC)
  * Cryptographic token handling and validation

### 2.2 Test Accounts and Roles Used
All 6 canonical FFM roles were dynamically evaluated using authenticated sessions:
1. `SYSTEM_ADMIN` (Actor ID: 12)
2. `ADMIN` (Actor ID: 20)
3. `EXECUTIVE` (Actor ID: 21)
4. `MANAGER` (Actor ID: 4)
5. `SUPERVISOR` (Actor ID: 22)
6. `EMPLOYEE` (Actor ID: 16)
7. `ANONYMOUS` (Unauthenticated)

---

## 3. Penetration Testing Scenarios & Results

| # | Scenario | Status / Result | Findings Identified |
|---|---|---|---|
| 1 | **Brute Force & Rate Limiting** | Executed | Account lockout and Identity API rate-limiting defended single-user attacks; Workforce API lacks rate-limiting (Finding 8). |
| 2 | **Race Condition / Concurrency** | Executed | Concurrent requests with duplicate employee codes failed gracefully via database unique constraint / EF exception. |
| 3 | **Privilege Escalation (Vertical)** | **Exploitable** | **Finding 1**: Manager can assign `SYSTEM_ADMIN` role when provisioning employees. |
| 4 | **Session / JWT Attacks** | **Exploitable** | **Finding 2**: Token forgery using hardcoded RSA private key permits total bypass. |
| 5 | **Workflow Bypass** | **Exploitable** | **Finding 3**: Accounts with status `PendingActivation` can log in and bypass activation. |
| 6 | **File Upload Attacks** | Not Applicable | Blocked: No file upload endpoints exist in the codebase. |
| 7 | **BOLA / IDOR** | **Exploitable** | **Finding 4 & 5**: Identity user profiles and Workforce employee records accessible across authorization boundaries. |
| 8 | **Authentication Bypass** | Defended | Unauthenticated requests without tokens or with bad signatures are rejected (401). |
| 9 | **Authorization Bypass (RBAC)** | Defended | Unprivileged roles (`EMPLOYEE`, `SUPERVISOR`) are rejected with 403 on restricted actions. |
| 10 | **Business Logic Abuse** | **Exploitable** | **Finding 6**: Invalid inputs trigger unhandled exceptions leaking stack traces. |
| 11 | **Parameter Manipulation** | Defended | `ActorUserId` manipulation defended; controller overwrites actor ID from token claims. |
| 12 | **Other Application-Level Attacks** | **Exploitable** | **Finding 7**: Stored XSS payload stored and rendered unencoded. |

---

## 4. Confirmed Vulnerabilities

---

### Finding 1: Vertical Privilege Escalation via Employee Role Assignment Flaw
* **Confirmed Vulnerability Name**: Vertical Privilege Escalation
* **Count**: 1
* **Severity**: Critical (CVSS: 9.9 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:C/C:H/I:H/A:H`)
* **Affected Endpoint**: `POST /api/v1/Employees`
* **Test Scenario**: Privilege Escalation
* **Test Steps**:
  1. Authenticate as a user holding the `MANAGER` role (`sub: 4`).
  2. Send a `POST /api/v1/Employees` request with `roleId: 1` (`SYSTEM_ADMIN`).
  3. Verify whether the request is accepted and whether the created user is granted administrative privileges.
* **Evidence**:
  * Request:
    ```http
    POST /api/v1/Employees HTTP/1.1
    Host: localhost:5001
    Authorization: Bearer <MANAGER_JWT_TOKEN>
    Content-Type: application/json

    {
      "employeeCode": "ESCAL_4921",
      "name": "Escalated Admin",
      "password": "AdminPassword@123",
      "roleId": 1,
      "email": "escal_4921@example.com",
      "mobile": "9876543210"
    }
    ```
  * Response:
    ```http
    HTTP/1.1 201 Created
    Location: http://localhost:5001/api/v1/Employees/5a251011-aaa3-49ba-adc2-5ab1aebcd608
    Content-Type: application/json; charset=utf-8

    {"id":"5a251011-aaa3-49ba-adc2-5ab1aebcd608"}
    ```
  * Database Verification:
    The newly created record in `Users` table has `Role = 'SYSTEM_ADMIN'`.
* **Exploitation Result**: **Successful**. A Manager successfully created a user with higher privileges than their own (`SYSTEM_ADMIN`).
* **Impact**:
  A compromised or rogue Manager account can instantly create full administrative accounts, bypassing administrative hierarchy and taking total control of both Identity and Workforce services.
* **Recommendation**:
  Implement strict role-hierarchy checks in `CreateEmployeeCommandHandler`. Ensure that actors can only assign roles lower than their own, and forbid `MANAGER` and `ADMIN` roles from provisioning `SYSTEM_ADMIN` accounts.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST)

---

### Finding 2: Complete Authentication Bypass via JWT Forgery Using Hardcoded RSA Private Key
* **Confirmed Vulnerability Name**: Cryptographic Token Forgery / Complete Authentication Bypass
* **Count**: 1
* **Severity**: Critical (CVSS: 9.8 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H`)
* **Affected Endpoint**: All protected endpoints across Identity and Workforce APIs
* **Test Scenario**: Session / JWT Attacks
* **Test Steps**:
  1. Extract the hardcoded RSA PKCS#8 private key from `appsettings.json` or `identity-private.key`.
  2. Use standard cryptographic libraries to mint a JWT token containing:
     * `iss`: `https://ffm.example.com`
     * `aud`: `ffm-client`
     * `sub`: `"12"`
     * `role`: `"SYSTEM_ADMIN"`
     * `exp`: `DateTime.UtcNow.AddHours(2)`
  3. Sign the token with RS256 using the extracted private key.
  4. Submit the forged token in the `Authorization: Bearer <FORGED_TOKEN>` header to protected endpoints.
* **Evidence**:
  * Request sent with forged token to `GET http://localhost:5000/api/auth/me`:
    ```http
    GET /api/auth/me HTTP/1.1
    Host: localhost:5000
    Authorization: Bearer eyJhbGciOiJSUzI1NiIsImtpZCI6ImZmbS1yc2Eta2V5LTE...
    ```
  * Response:
    ```http
    HTTP/1.1 200 OK
    Content-Type: application/json; charset=utf-8

    {"id":12,"usernameOrEmail":"test@example.com","role":"SYSTEM_ADMIN","status":"Active"}
    ```
  * Request sent with forged token to `POST http://localhost:5001/api/v1/Employees`:
    Result: HTTP 201 Created.
* **Exploitation Result**: **Successful**. Forged tokens were fully accepted and validated by both APIs.
* **Impact**:
  Attackers can forge arbitrary identity tokens for any user and role without possessing valid credentials, leading to total compromise of confidentiality, integrity, and availability.
* **Recommendation**:
  Remove the RSA private key from source control and configuration files immediately. Store private keys in a secure key management system (Azure Key Vault, AWS KMS, HashiCorp Vault). Rotate the compromised signing key pair.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

### Finding 3: Workflow Bypass — Authentication of Inactive Accounts
* **Confirmed Vulnerability Name**: Inactive Account Authentication / Workflow Bypass
* **Count**: 1
* **Severity**: High (CVSS: 7.5 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:N/A:N`)
* **Affected Endpoint**: `POST /api/auth/login`
* **Test Scenario**: Workflow Bypass
* **Test Steps**:
  1. Identify a user account with status `PendingActivation` (e.g., User 1 `arun.kumar@example.com` with `Status = 0`).
  2. Send a valid authentication request with matching credentials to `POST /api/auth/login`.
  3. Observe whether the API rejects the request or issues an active JWT access token.
* **Evidence**:
  * In `AuthService.cs` lines 46–50:
    ```csharp
    // Check account lockout
    if (user.Status == AccountStatus.Locked && user.LockedUntil.HasValue && user.LockedUntil > DateTimeOffset.UtcNow)
    {
        return null!; // Locked
    }
    ```
  * Notice that `AuthService.AuthenticateAsync` fails to verify whether `user.Status == AccountStatus.Active`. Accounts in `PendingActivation`, `Suspended`, `Blocked`, or `Deactivated` states proceed directly to token issuance upon password verification.
* **Exploitation Result**: **Successful**.
* **Impact**:
  Users whose accounts have been deactivated, suspended, or are pending formal onboarding can bypass activation policies and retain full operational access to APIs.
* **Recommendation**:
  Enforce explicit account status validation:
  ```csharp
  if (user.Status != AccountStatus.Active)
  {
      return null!; // Inactive, pending, or suspended
  }
  ```
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

### Finding 4: BOLA / IDOR — Identity API User Profile Exposure
* **Confirmed Vulnerability Name**: Broken Object Level Authorization (Identity API)
* **Count**: 1
* **Severity**: High (CVSS: 7.5 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:N/A:N`)
* **Affected Endpoint**: `GET /api/auth/users/{id:int}`
* **Test Scenario**: BOLA / IDOR
* **Test Steps**:
  1. Authenticate as a standard employee (`EMPLOYEE`, User ID 16).
  2. Send a request to `GET /api/auth/users/12` (System Administrator profile).
  3. Inspect response data.
* **Evidence**:
  * Request:
    ```http
    GET /api/auth/users/12 HTTP/1.1
    Host: localhost:5000
    Authorization: Bearer <EMPLOYEE_JWT_TOKEN>
    ```
  * Response:
    ```http
    HTTP/1.1 200 OK
    Content-Type: application/json; charset=utf-8

    {"id":12,"usernameOrEmail":"test@example.com","role":"SYSTEM_ADMIN","status":"Active"}
    ```
* **Exploitation Result**: **Successful**.
* **Impact**:
  Any authenticated low-privileged user can enumerate and view the full profile and role assignments of all users in the system, enabling targeted reconnaissance for privilege escalation.
* **Recommendation**:
  Restrict `GET /api/auth/users/{id}` so that users can only access their own profile (`id == currentUserId`) unless they hold the `SYSTEM_ADMIN` or `ADMIN` role.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

### Finding 5: BOLA / IDOR — Workforce Employee Record Access Across Hierarchies
* **Confirmed Vulnerability Name**: Broken Object Level Authorization (Workforce API)
* **Count**: 1
* **Severity**: Medium (CVSS: 6.5 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:N/A:N`)
* **Affected Endpoint**: `GET /api/v1/Employees/{id:guid}`
* **Test Scenario**: BOLA / IDOR
* **Test Steps**:
  1. Authenticate as a `MANAGER` (`sub: 4`).
  2. Send a request to `GET /api/v1/Employees/{id}` for an employee not reporting to this manager.
  3. Verify whether the API enforces reporting line or organizational unit boundaries.
* **Evidence**:
  * Request:
    ```http
    GET /api/v1/Employees/a247f25d-a045-42b1-9d6c-6fa1480b6aad HTTP/1.1
    Host: localhost:5001
    Authorization: Bearer <MANAGER_JWT_TOKEN>
    ```
  * Response:
    ```http
    HTTP/1.1 200 OK
    Content-Type: application/json; charset=utf-8

    {
      "id": "a247f25d-a045-42b1-9d6c-6fa1480b6aad",
      "employeeCode": "VICTIM_3141",
      "userId": 35,
      "name": "Victim Employee",
      "roleId": 6,
      "orgUnitId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "managerId": null,
      "email": "VICTIM_3141@example.com",
      "mobile": "9876543210",
      "status": "PENDING_ACTIVATION",
      "createdAt": "2026-09-29T10:46:23.439967Z",
      "createdBy": 12
    }
    ```
* **Exploitation Result**: **Successful**.
* **Impact**:
  Managers can access sensitive PII (emails, phone numbers, employee codes) of employees belonging to other managers, branches, or organizational units.
* **Recommendation**:
  Enforce tenant, organizational unit, and manager hierarchy verification in `GetEmployeeByIdQueryHandler`.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

### Finding 6: Information Disclosure via Unhandled Exception Stack Traces
* **Confirmed Vulnerability Name**: Information Disclosure / Verbose Stack Trace Exposure
* **Count**: 1
* **Severity**: Medium (CVSS: 5.3 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:L/I:N/A:N`)
* **Affected Endpoint**: `POST /api/v1/Employees`
* **Test Scenario**: Business Logic Abuse / Parameter Manipulation
* **Test Steps**:
  1. Authenticate with an authorized role (`SYSTEM_ADMIN` or `MANAGER`).
  2. Send a `POST /api/v1/Employees` request with an invalid/non-existent `roleId` (e.g. `99999`).
  3. Observe server response format and diagnostic exposure.
* **Evidence**:
  * Response:
    ```http
    HTTP/1.1 500 Internal Server Error
    Content-Type: text/plain; charset=utf-8

    System.InvalidOperationException: Role with ID 99999 not found.
       at FFM.Workforce.Application.Handlers.CreateEmployeeCommandHandler.Handle(CreateEmployeeCommand request, CancellationToken cancellationToken) in /src/FFM.Workforce/FFM.Workforce.Application/Handlers/CreateEmployeeCommandHandler.cs:line 72
       at FFM.Workforce.Api.Controllers.EmployeesController.Create(CreateEmployeeCommand command, CancellationToken cancellationToken) in /src/FFM.Workforce/FFM.Workforce.Api/Controllers/EmployeesController.cs:line 34
       ...
    HEADERS
    =======
    Host: localhost:5001
    Authorization: Bearer <TOKEN>
    ...
    ```
* **Exploitation Result**: **Successful**. The API leaked internal server file paths (`/src/FFM.Workforce/...`), code line numbers, and request headers.
* **Impact**:
  Provides attackers with detailed information regarding internal class names, execution flows, and underlying software versions.
* **Recommendation**:
  Implement a centralized Global Exception Handling Middleware that intercepts unhandled exceptions and returns standardized RFC 7807 Problem Details payloads without internal stack traces.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

### Finding 7: Stored Cross-Site Scripting (XSS) via Unsanitized Employee Fields
* **Confirmed Vulnerability Name**: Stored Cross-Site Scripting (XSS)
* **Count**: 1
* **Severity**: Medium (CVSS: 6.1 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:R/S:C/C:L/I:L/A:N`)
* **Affected Endpoint**: `POST /api/v1/Employees` & `GET /api/v1/Employees/{id}`
* **Test Scenario**: Other Application-Level Attacks (XSS)
* **Test Steps**:
  1. Submit a `POST /api/v1/Employees` request with an active XSS payload in the `name` field: `<script>alert('pwned')</script>`.
  2. Fetch the created record via `GET /api/v1/Employees/{id}`.
  3. Verify whether the payload is stored verbatim and returned unencoded.
* **Evidence**:
  * Request:
    ```json
    {
      "employeeCode": "XSS_7189",
      "name": "<script>alert('pwned')</script>",
      "password": "Password@123",
      "roleId": 6,
      "email": "xss_7189@example.com",
      "mobile": "9876543210"
    }
    ```
  * Response from `GET /api/v1/Employees/ac703ebb-201b-4b17-881b-e74aac4bfdbe`:
    ```json
    {
      "id": "ac703ebb-201b-4b17-881b-e74aac4bfdbe",
      "employeeCode": "XSS_7189",
      "name": "<script>alert('pwned')</script>",
      "roleId": 6,
      ...
    }
    ```
* **Exploitation Result**: **Successful**. Payload stored and rendered unencoded in the JSON response.
* **Impact**:
  When a web frontend or administrative dashboard renders employee records without HTML contextual encoding, the malicious script executes in the victim's session, enabling session hijacking, credential harvesting, or administrative actions.
* **Recommendation**:
  Sanitize and validate inputs upon submission and ensure client-side rendering frameworks enforce strict output encoding.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

### Finding 8: Missing Rate Limiting on Workforce Endpoints
* **Confirmed Vulnerability Name**: Missing Rate Limiting / Unthrottled API Endpoints
* **Count**: 1
* **Severity**: Medium (CVSS: 5.3 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:L`)
* **Affected Endpoint**: `POST /api/v1/Employees`, `GET /api/v1/Employees/{id}`
* **Test Scenario**: Brute Force / Resource Exhaustion
* **Test Steps**:
  1. Send rapid bursts of requests against Workforce endpoints.
  2. Observe whether any throttling (HTTP 429) occurs.
* **Evidence**:
  * While `FFM.Identity.Access.Api/Program.cs` configures `RateLimiter` with `AuthPolicy` (10 req/min), `FFM.Workforce.Api/Program.cs` contains zero rate-limiting middleware.
  * All burst requests were processed synchronously without rate restriction.
* **Exploitation Result**: **Successful**.
* **Impact**:
  Workforce endpoints are exposed to automated brute force, rapid resource exhaustion, and scraping.
* **Recommendation**:
  Configure ASP.NET Core RateLimiter middleware in `FFM.Workforce.Api/Program.cs` similar to the Identity service.
* **Status**: Open
* **Cross-Method Finding**: Yes (Pentest / SAST / DAST)

---

## 5. Tested Scenarios with Zero Findings

As required by Section 5.5 and Section 6.3 of `Docs/FFM_Security_Testing_Spec.md`, the following attack scenarios were thoroughly tested and verified to have **zero confirmed vulnerabilities**:

```plaintext
Authentication Bypass (Direct Endpoint Access): 0
Race Condition: 0
File Upload Security Weakness: 0
Credential Brute Force (Single User Lockout Bypass): 0
Authorization Bypass (Unprivileged Role Boundary): 0
```

* **Authentication Bypass (Direct)**: Requests without Authorization headers or with corrupted/tampered token signatures were consistently rejected with HTTP 401 Unauthorized.
* **Race Condition**: Rapid concurrent execution of employee creation with identical employee codes resulted in one successful creation (201) and one rejected creation (500 conflict), preventing double-booking or inconsistent database states.
* **File Upload Security Weakness**: No file upload endpoints exist in the active codebase.
* **Credential Brute Force (Single User)**: Target accounts correctly triggered account lockout after 5 consecutive failed login attempts, blocking subsequent authentication for 15 minutes.
* **Authorization Bypass (Unprivileged Role Boundary)**: Direct attempts by `EMPLOYEE` or `SUPERVISOR` roles to call restricted provisioning endpoints (`POST /api/v1/Employees`, `POST /api/auth/users`) were strictly rejected with HTTP 403 Forbidden.

---

## 6. False Positives

| Scenario | Observation | Analysis | Disposition |
|---|---|---|---|
| Parameter Manipulation (`ActorUserId`) | `CreateEmployeeCommand` includes an `actorUserId` property in the JSON schema, suggesting potential spoofing. | In `EmployeesController.Create` (line 32), the controller explicitly executes `command.ActorUserId = _currentUserService.GetUserId();`, overwriting any client-supplied value with the verified JWT `sub` claim. Spoofing is not exploitable. | **False Positive** (Defended by controller logic) |
| Token Tampering without Re-signing | Altering claims in a valid token payload (e.g. changing `role` from `EMPLOYEE` to `SYSTEM_ADMIN`). | The ASP.NET Core JWT Bearer authentication handler validates the cryptographic signature against the public RSA key, correctly failing verification and returning HTTP 401. | **False Positive** (Defended by cryptographic signature check) |

---

## 7. Blocked / Untested Areas

1. **Unimplemented Business Modules**:
   * Features defined in architectural specifications—including Attendance Management, Task Tracking, Expense Processing, GPS Geofencing, and Visit Verification—have no implemented controllers or endpoints in the active codebase. Penetration testing of these business logic workflows is blocked pending implementation.
2. **File Upload Handlers**:
   * No multipart/form-data upload handlers exist in the current application.

---

## 8. Penetration Testing Vulnerability Summary and Exact Counts

```plaintext
Penetration Testing Vulnerability Summary

Vertical Privilege Escalation: 1
Session / Cryptographic Token Forgery: 1
Workflow Bypass / Inactive Account Authentication: 1
BOLA / IDOR (Identity API): 1
BOLA / IDOR (Workforce API): 1
Information Disclosure / Verbose Stack Traces: 1
Stored Cross-Site Scripting (XSS): 1
Missing Rate Limiting (Workforce API): 1
Authentication Bypass (Direct Endpoint Access): 0
Race Condition: 0
File Upload Security Weakness: 0
Credential Brute Force (Single User Lockout Bypass): 0
Authorization Bypass (Unprivileged Role Boundary): 0

Total Penetration Testing Findings: 8
```
