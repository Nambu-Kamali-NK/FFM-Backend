# FFM SAST Security Testing Report (Post-DAST Remediation)

## 1. Objective

This report documents the results of **Phase 2 — Static Application Security Testing (SAST)** performed on the current remediated source code of the Field Force Management (FFM) solution, in strict accordance with [`Docs/FFM_Security_Testing_Spec.md`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/Docs/FFM_Security_Testing_Spec.md) and the post-DAST remediation codebase state.

The primary objective of this phase is to:
1. Perform an exhaustive source-code review across all layers of the solution.
2. Verify the code-level implementation of the remediated DAST controls.
3. Identify all remaining code-level security vulnerabilities with exact confirmed names and evidence-backed counts.
4. Distinguish genuine vulnerabilities from false positives and code smells.

---

## 2. Scope & Source Code Analyzed

### 2.1 Solution Structure & Layers Analyzed
The static analysis audited all 78 active C# source files and configuration artifacts across both microservices and supporting projects:
* **Identity Service** (`FFM.Identity.Access`):
  * **API Layer**: `Program.cs`, `AuthController.cs`, `LoginRequest.cs`, `LoginResponse.cs`, `UserInfoResponse.cs`, `CreateUserRequest.cs`
  * **Domain Layer**: `User.cs`, `Role.cs`, `RefreshToken.cs`, `UserDevice.cs`, `MfaChallenge.cs`, `AuthAuditLog.cs`, `JwtOptions.cs`, `MfaOptions.cs`, `DeviceBindingOptions.cs`, `RateLimitOptions.cs`
  * **Infrastructure Layer**: `IdentityDbContext.cs`, `IdentityDbContextFactory.cs`, EF Core Migrations (20260928170531_ChangeUserIdToNumeric, etc.)
  * **Services**: `AuthService.cs`, `RefreshTokenService.cs`, `UserService.cs`, `TokenHasher.cs`
* **Workforce Service** (`FFM.Workforce`):
  * **API Layer**: `Program.cs`, `EmployeesController.cs`, `ExceptionHandlingMiddleware.cs`
  * **Application Layer**: `CreateEmployeeCommand.cs`, `GetEmployeeByIdQuery.cs`, `EmployeeDto.cs`, `CreateEmployeeCommandHandler.cs`, `GetEmployeeByIdQueryHandler.cs`, `CreateEmployeeCommandValidator.cs`, `ValidationBehavior.cs`
  * **Domain Layer**: `Employee.cs`, `OrgUnit.cs`, `Role.cs`, `AuditLog.cs`, `OutboxMessage.cs`
  * **Infrastructure Layer**: `WorkforceDbContext.cs`, `WorkforceDbContextFactory.cs`, `EmployeeRepository.cs`, `OrgUnitRepository.cs`, `OutboxRepository.cs`, `CurrentUserService.cs`, EF Core Migrations
* **Configuration & Container Deployment**:
  * `FFM.Identity.Access.Api/appsettings.json`, `FFM.Identity.Access.Api/appsettings.Development.json`
  * `FFM.Workforce/FFM.Workforce.Api/appsettings.json`
  * `docker-compose.yml`, `Dockerfile`

---

## 3. SAST Checks Performed

In accordance with Section 4 of the specification, the code was audited against:
1. **Hardcoded Secrets**: Plaintext cryptographic keys, database passwords, tokens, API credentials.
2. **Insecure Deserialization**: Use of dangerous serializers (`BinaryFormatter`, `TypeNameHandling.All`, `NetDataContractSerializer`).
3. **Integer Overflow**: Arithmetic operations, wraparound risks on identity and counter types.
4. **Weak Cryptography**: Deprecated hash algorithms (MD5, SHA-1), weak ciphers (DES, RC4), inadequate key lengths.
5. **Sensitive Data in Logs**: Writing passwords, claims, tokens, or cryptographic keys to standard output or log streams.
6. **SQL Injection**: Raw SQL queries, dynamic concatenation in EF Core (`FromSqlRaw`, `ExecuteSqlRaw`).
7. **Command Injection**: Unsafe process execution (`System.Diagnostics.Process`, shell commands).
8. **Cross-Site Scripting (XSS)**: Lack of input sanitization and output encoding for user-supplied string data.
9. **Path Traversal**: Unvalidated user-supplied parameters used in file system I/O.
10. **Improper Access Control / Privilege Escalation**: Business logic allowing lower-privileged roles to provision administrative privileges.
11. **Session & Token Management**: Claims consistency, token revocation, refresh token integrity.

---

## 4. Confirmed Vulnerability Table

| S.No | Vulnerability Name | Count | Severity | Affected File / Location | Status |
| ---: | ------------------ | ----: | -------- | ------------------------ | ------ |
|    1 | Hardcoded Secrets | 1 | Critical | [`FFM.Identity.Access.Api/appsettings.json`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/appsettings.json#L14) & `identity-private.key` | Open |
|    2 | Insecure Deserialization | 0 | - | N/A | Closed |
|    3 | Integer Overflow | 0 | - | N/A | Closed |
|    4 | Weak Cryptography | 0 | - | N/A | Closed |
|    5 | Sensitive Data in Logs | 1 | Medium | [`FFM.Identity.Access.Api/Controllers/AuthController.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Controllers/AuthController.cs#L69-L73) & [`AuthService.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Services/AuthService.cs#L97-L100) | Open |
|    6 | SQL Injection | 0 | - | N/A | Closed |
|    7 | Command Injection | 0 | - | N/A | Closed |
|    8 | Cross-Site Scripting (XSS) | 1 | Medium | [`FFM.Workforce.Application/Validators/CreateEmployeeCommandValidator.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Application/Validators/CreateEmployeeCommandValidator.cs#L19-L21) & [`Employee.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Domain/Entities/Employee.cs#L47) | Open |
|    9 | Path Traversal | 0 | - | N/A | Closed |
|   10 | Vertical Privilege Escalation | 1 | High | [`FFM.Workforce.Application/Handlers/CreateEmployeeCommandHandler.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Application/Handlers/CreateEmployeeCommandHandler.cs#L70-L78) | Open |
|   11 | Missing Role Claim in Session Refresh | 1 | Medium | [`FFM.Identity.Access.Api/Services/RefreshTokenService.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Services/RefreshTokenService.cs#L59-L64) | Open |

---

## 5. Summary Tables

### 5.1 Non-Zero Confirmed Vulnerabilities Summary

| Vulnerability Name | Confirmed Count |
| ------------------ | --------------: |
| Hardcoded Secrets | 1 |
| Sensitive Data in Logs | 1 |
| Cross-Site Scripting (XSS) | 1 |
| Vertical Privilege Escalation | 1 |
| Missing Role Claim in Session Refresh | 1 |
| **Total SAST Vulnerabilities** | **5** |

### 5.2 Zero-Findings Checks Summary

| SAST Check | Confirmed Count |
| ---------- | --------------: |
| Insecure Deserialization | 0 |
| Integer Overflow | 0 |
| Weak Cryptography | 0 |
| SQL Injection | 0 |
| Command Injection | 0 |
| Path Traversal | 0 |

---

## 6. Evidence for Every Non-Zero Finding

---

### Finding 1: Hardcoded Secrets — RSA Private Signing Key in Configuration & Repository
* **Vulnerability Name**: Hardcoded Secrets
* **Count**: 1
* **Severity**: Critical (CVSS: 9.1 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:N`)
* **Affected File**: [`FFM.Identity.Access.Api/appsettings.json`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/appsettings.json) and `identity-private.key`
* **Class / Method**: Configuration Section `Jwt:RsaPrivateKey`
* **Code Location**: `FFM.Identity.Access.Api/appsettings.json` line 14 and repository root
* **Code Evidence**:
  ```json
  "Jwt": {
      "Issuer": "https://ffm.example.com",
      "Audience": "ffm-client",
      "ExpiryMinutes": 60,
      "RefreshExpiryDays": 30,
      "RsaPrivateKey": "MIIEvgIBADANBgkqhkiG9w0BAQEFAASCBKgwggSkAgEAAoIBAQDhZ...[MASKED]...Bi3E"
  }
  ```
* **Description**:
  The 2048-bit RSA PKCS#8 private key used by the Identity Service to sign all JWT authentication tokens is hardcoded in `appsettings.json` and committed in plaintext to version control as `identity-private.key`.
* **Security Impact**:
  Anyone who gains read access to the source code repository or published Docker images can extract the private key and forge valid JWT access tokens for arbitrary user IDs and arbitrary roles (including `SYSTEM_ADMIN`), completely bypassing authentication across both Identity and Workforce APIs.
* **Recommendation**:
  Remove the RSA private key from source control and configuration files. Store the key in a dedicated Key Vault (Azure Key Vault, AWS Secrets Manager, or HashiCorp Vault) and inject it at runtime via secure environment variables. Immediately rotate the compromised key pair.
* **Status**: Open

---

### Finding 2: Sensitive Data in Logs — Console Logging of JWT Claims & Cryptographic Modulus
* **Vulnerability Name**: Sensitive Data in Logs
* **Count**: 1
* **Severity**: Medium (CVSS: 5.3 - `CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:L/I:N/A:N`)
* **Affected File**: [`FFM.Identity.Access.Api/Controllers/AuthController.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Controllers/AuthController.cs) and [`AuthService.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Services/AuthService.cs)
* **Class / Method**: `AuthController.CreateUser` (lines 69–73) & `AuthService.AuthenticateAsync` (lines 97–100)
* **Code Location**: `AuthController.cs:69-73` and `AuthService.cs:97-100`
* **Code Evidence**:
  * In `AuthController.cs`:
    ```csharp
    Console.WriteLine("=== JWT CLAIMS ===");
    foreach (var claim in User.Claims)
    {
        Console.WriteLine(claim);
    }
    ```
  * In `AuthService.cs`:
    ```csharp
    Console.WriteLine($"JWT VALIDATION KEY: {Convert.ToBase64String(signingKey.Rsa!.ExportParameters(false).Modulus!)}");
    Console.WriteLine("JWT SIGNING KEY CREATED");
    Console.WriteLine($"JWT SIGNING MODULUS: {Convert.ToBase64String(signingKey.Rsa!.ExportParameters(false).Modulus!)}");
    ```
* **Description**:
  Production code contains `Console.WriteLine` statements that output user claims (including user ID, assigned role, client identifier) and cryptographic RSA modulus details directly to standard output upon authentication and user creation requests.
* **Security Impact**:
  Container log aggregators, CI/CD runners, and monitoring systems capture stdout in plaintext, exposing sensitive user session claims, user identity metadata, and cryptographic parameters to operations personnel or unauthorized log viewers.
* **Recommendation**:
  Remove all debugging `Console.WriteLine` statements. Use structured logging (`ILogger<T>`) configured with appropriate logging levels and log redaction to ensure that sensitive token claims and cryptographic material are never logged.
* **Status**: Open

---

### Finding 3: Cross-Site Scripting (XSS) — Stored XSS via Unsanitized Employee Name Field
* **Vulnerability Name**: Cross-Site Scripting (XSS)
* **Count**: 1
* **Severity**: Medium (CVSS: 6.1 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:R/S:C/C:L/I:L/A:N`)
* **Affected File**: [`FFM.Workforce.Application/Validators/CreateEmployeeCommandValidator.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Application/Validators/CreateEmployeeCommandValidator.cs) & [`Employee.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Domain/Entities/Employee.cs)
* **Class / Method**: `CreateEmployeeCommandValidator` (lines 19–21) & `Employee` constructor (line 47)
* **Code Location**: `CreateEmployeeCommandValidator.cs:19-21`
* **Code Evidence**:
  * In `CreateEmployeeCommandValidator.cs`:
    ```csharp
    RuleFor(x => x.Name)
        .NotEmpty().WithMessage("Name is required.")
        .MaximumLength(100);
    ```
  * In `Employee.cs`:
    ```csharp
    Name = name; // stored verbatim without HTML encoding or script sanitization
    ```
  * In `GetEmployeeByIdQueryHandler.cs`:
    ```csharp
    Name = employee.Name // returned verbatim in DTO
    ```
* **Description**:
  The input validation for `Name` only validates that the string is non-empty and does not exceed 100 characters. It does not validate character sets or strip/encode HTML/JavaScript tags. Submitting a name containing `<script>alert(1)</script>` (length 25 < 100) passes validation, is persisted verbatim in PostgreSQL, and is returned raw in API JSON responses.
* **Security Impact**:
  When downstream client applications, administrative consoles, or web frontends render employee names without contextual HTML output encoding, the injected script executes within the context of the viewing administrator's browser session, facilitating session hijacking, CSRF, and account takeover.
* **Recommendation**:
  Enforce a regular expression or character allowlist on `Name` in `CreateEmployeeCommandValidator` (e.g. letters, spaces, hyphens, and apostrophes only), and apply HTML sanitization before persistence.
* **Status**: Open

---

### Finding 4: Vertical Privilege Escalation — Improper Authorization on Role Assignment
* **Vulnerability Name**: Vertical Privilege Escalation
* **Count**: 1
* **Severity**: High (CVSS: 8.8 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:H`)
* **Affected File**: [`FFM.Workforce.Application/Handlers/CreateEmployeeCommandHandler.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Application/Handlers/CreateEmployeeCommandHandler.cs)
* **Class / Method**: `CreateEmployeeCommandHandler.Handle`
* **Code Location**: `CreateEmployeeCommandHandler.cs:48-78`
* **Code Evidence**:
  ```csharp
  // Actor check permits MANAGER:
  var actorRoles = _currentUserService.GetRoles();
  if (!actorRoles.Contains("SYSTEM_ADMIN") &&
      !actorRoles.Contains("ADMIN") &&
      !actorRoles.Contains("MANAGER"))
  {
      throw new UnauthorizedAccessException("Actor does not have permission to create employees.");
  }

  // Role resolution accepts any valid RoleId without hierarchy restriction:
  var workforceRole = await _dbContext.Set<Role>().FindAsync(request.RoleId);
  if (workforceRole == null)
      throw new InvalidOperationException($"Role with ID {request.RoleId} not found.");

  if (!Enum.TryParse<FFM.Identity.Access.Domain.Entities.Role>(workforceRole.Name, true, out var identityRole))
  {
      identityRole = FFM.Identity.Access.Domain.Entities.Role.EMPLOYEE;
  }

  // Creates user with the assigned identityRole:
  var identityUser = new FFM.Identity.Access.Domain.Entities.User
  {
      ...
      Role = identityRole
  };
  ```
* **Description**:
  Users possessing the `MANAGER` role are permitted to create employees. However, the handler does not verify whether the caller is authorized to assign the requested `RoleId`. A `MANAGER` can specify `roleId: 1` (`SYSTEM_ADMIN`) or `roleId: 2` (`ADMIN`), causing the application to provision a full `SYSTEM_ADMIN` user in both Workforce and Identity systems.
* **Security Impact**:
  Any user holding `MANAGER` privileges can vertically escalate their privileges to `SYSTEM_ADMIN` by creating an administrative account with credentials known to them, defeating separation of duties and administrative access tiers.
* **Recommendation**:
  Implement role-hierarchy assignment rules in `CreateEmployeeCommandHandler`. Ensure that actors can only assign roles subordinate to their own:
  ```csharp
  if (actorRoles.Contains("MANAGER") && (identityRole == Role.SYSTEM_ADMIN || identityRole == Role.ADMIN))
  {
      throw new UnauthorizedAccessException("Managers cannot assign administrative roles.");
  }
  ```
* **Status**: Open

---

### Finding 5: Missing Role Claim in Session Refresh — Token Inconsistency & Authorization Failure
* **Vulnerability Name**: Missing Role Claim in Session Refresh
* **Count**: 1
* **Severity**: Medium (CVSS: 6.5 - `CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:N/I:H/A:N`)
* **Affected File**: [`FFM.Identity.Access.Api/Services/RefreshTokenService.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Services/RefreshTokenService.cs)
* **Class / Method**: `RefreshTokenService.RefreshAsync`
* **Code Location**: `RefreshTokenService.cs:59-64`
* **Code Evidence**:
  ```csharp
  var claims = new[]
  {
      new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
      new Claim("client_id", request.ClientId)
  };
  ```
  *(Compare with `AuthService.cs` line 111 where `new Claim("role", user.Role.ToString())` is explicitly included).*
* **Description**:
  When a user refreshes an expired access token via `POST /api/auth/refresh`, `RefreshTokenService.RefreshAsync` generates a new JWT access token that includes `sub`, `jti`, and `client_id`, but completely omits the `role` claim.
* **Security Impact**:
  As soon as an authenticated administrative user (such as `SYSTEM_ADMIN` or `MANAGER`) refreshes their session token, the newly issued token strips their role. Subsequent requests to role-protected endpoints fail with `403 Forbidden`, causing denial of service for administrative sessions, or authorization inconsistencies across microservices.
* **Recommendation**:
  Include `new Claim("role", user.Role.ToString())` in the claims collection generated by `RefreshTokenService.RefreshAsync`.
* **Status**: Open

---

## 7. Status of Previously Remediated DAST Findings in Source Code

The 7 vulnerabilities addressed during Phase 1 DAST remediation were verified in the source code:

| DAST Remediation Category | Source Code Status | Code-Level Verification |
|---|---|---|
| **BOLA / IDOR (User Profile API)** | **Remediated** | [`AuthController.cs:93-104`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Controllers/AuthController.cs#L93-L104) checks `currentUserId == id` or `isAdmin`. Cross-user access rejected with 403. |
| **BOLA / IDOR (Workforce Employee API)** | **Remediated** | [`GetEmployeeByIdQueryHandler.cs:33-59`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Application/Handlers/GetEmployeeByIdQueryHandler.cs#L33-L59) verifies role, ownership, reporting line (`ManagerId`), or `OrgUnitId`. |
| **Inactive Account Authentication** | **Remediated** | [`AuthService.cs:59-67`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Services/AuthService.cs#L59-L67) and [`RefreshTokenService.cs:49`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Identity.Access.Api/Services/RefreshTokenService.cs#L49) enforce `user.Status == AccountStatus.Active`. |
| **Missing Input Validation Pipeline** | **Remediated** | [`ValidationBehavior.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Application/Behaviors/ValidationBehavior.cs) and `AssemblyScanner` active in [`Program.cs:134-142`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Api/Program.cs#L134-L142). |
| **Information Disclosure / Verbose Errors** | **Remediated** | [`ExceptionHandlingMiddleware.cs`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Api/Middleware/ExceptionHandlingMiddleware.cs) catches all exceptions and outputs RFC 7807 Problem Details without stack traces. |
| **Missing Rate Limiting** | **Remediated** | [`Program.cs:119-132`](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/FFM.Workforce/FFM.Workforce.Api/Program.cs#L119-L132) configures `WorkforcePolicy` rate limiter and applies it to controllers. |
| **Security Misconfiguration / Security Headers** | **Remediated** | `ConfigureKestrel(opts => opts.AddServerHeader = false)` and custom security headers middleware active in both services. |

---

## 8. False Positives

| S.No | Alert / Pattern | Reason Not a Vulnerability |
| ---: | --------------- | -------------------------- |
|    1 | `KeyGenerator` & `KeyVerifier` Offline CLI Utilities | Console tools in `KeyGenerator/Program.cs` and `KeyVerifier/Program.cs` read/write `identity-private.key`. These are offline developer-time CLI tools for key generation and verification, not part of deployed web API runtimes. |
|    2 | EF Core Migration Raw SQL DDL Scripts | Migration files (`20260928170600_ChangeUserAndRoleIdsToNumeric.cs`) contain raw SQL statements for table DDL, identity sequence restarts, and data backfilling. These are design-time administrative migrations, not runtime queries handling untrusted user input. |
|    3 | Parameter `ActorUserId` in `CreateEmployeeCommand` | `CreateEmployeeCommand` accepts `ActorUserId` in the JSON request body. However, `EmployeesController.Create` (line 32) explicitly overwrites `command.ActorUserId = _currentUserService.GetUserId();` using the validated JWT claims, preventing caller spoofing. |

---

## 9. Untested / Blocked Areas

| Project / Module | Reason |
| ---------------- | ------ |
| **Attendance Management** (`/api/v1/attendance/*`) | No controllers, handlers, or entities exist in the active codebase. |
| **Field Tasks** (`/api/v1/tasks/*`) | Module specified in architectural documentation has not yet been implemented. |
| **Expense Tracking** (`/api/v1/expenses/*`) | Module specified in architectural documentation has not yet been implemented. |
| **GPS Geofencing** (`/api/v1/gps/*`) | Module specified in architectural documentation has not yet been implemented. |
| **Visit Verification** (`/api/v1/visits/*`) | Module specified in architectural documentation has not yet been implemented. |
| **File Upload Handlers** | No multipart/form-data upload handlers exist in the current application. |

---

## 10. Final SAST Summary

```plaintext
SAST Status: Completed

SAST Vulnerability Summary

| Vulnerability Name | Confirmed Count |
|---|---:|
| Hardcoded Secrets | 1 |
| Sensitive Data in Logs | 1 |
| Cross-Site Scripting (XSS) | 1 |
| Vertical Privilege Escalation | 1 |
| Missing Role Claim in Session Refresh | 1 |
| Total SAST Vulnerabilities | 5 |

False Positives: 3
Untested / Blocked Areas: 6

Report:
FFM-SAST-SECURITY-REPORT.md
```
