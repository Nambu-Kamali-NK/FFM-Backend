# Edit Employee Specification

## 1. Purpose

Allow an authorized Admin, Manager, or Supervisor to update an existing employee's editable profile and workforce information.

The employee's existing `Id` must remain unchanged.

## 2. API

**Method:** `PUT`

**Endpoint:**

```text
/api/Employees/{id}
```

Where `{id}` is the employee's numeric Employee ID if Employee ID has been converted to numeric; otherwise use the existing Employee ID type from the current implementation.

## 3. Request

Use a request DTO/command similar to:

```json
{
  "name": "Updated Employee Name",
  "email": "updated@example.com",
  "mobile": "9876543210",
  "roleId": 6,
  "managerId": null,
  "actorUserId": 12
}
```

## 4. Editable Fields

Allow updating:

- Name
- Email
- Mobile
- Role
- Manager

Do **not** allow the request to change:

- Employee ID
- User ID
- Employee Code
- CreatedBy
- CreatedAt

If any of these fields are not editable according to the existing Workforce Service specification, preserve the current behavior and do not introduce new business rules.

## 5. Processing Flow

```text
Request
  ↓
Validate Employee ID
  ↓
Find Employee
  ↓
Validate Actor User
  ↓
Validate Actor Authorization & Role Permissions (DAST/BOLA Verification)
  ↓
Validate Request Data & Input Sanitization (SAST/DAST Input Validation)
  ↓
Verify Immutable Fields (Parameter Tampering Protection)
  ↓
Validate Role
  ↓
Validate Manager & Prevent Circular Hierarchy
  ↓
Check Duplicate Email/Mobile
  ↓
Update Employee
  ↓
Save Changes
  ↓
Create Audit Log (Secure Audit Trail without PII/Secret Leakage)
  ↓
Return Success
```

## 6. Validations

Implement the following validations:

1. Employee must exist.
2. Actor user must exist.
3. Actor must have permission to edit the employee.
4. Name must not be empty.
5. Email must be valid if provided.
6. Email must not belong to another employee.
7. Mobile must be valid if provided.
8. Mobile must not belong to another employee.
9. Role must exist.
10. Manager must exist if `managerId` is provided.
11. Employee must not be assigned as their own manager.
12. Do not modify the employee's ID.
13. Do not modify the employee's `CreatedAt` or `CreatedBy`.
14. Preserve existing employee status unless status modification is explicitly part of this operation.

## 7. ActorUserId

`actorUserId` represents the authenticated user who performs the edit.

Prefer obtaining the actor user ID from the authenticated JWT/current-user context rather than trusting a client-supplied `actorUserId`.

Do not allow a user to impersonate another user by supplying another user's ID.

## 8. Audit

Record an audit entry containing:

- ActorUserId
- Action = `EmployeeUpdated`
- EntityType = `Employee`
- EntityId = Employee ID
- Timestamp
- Relevant update information if supported by the existing audit design

## 9. Response

For successful update:

**HTTP 200 OK**

Return a simple response containing the employee ID and success message.

Example:

```json
{
  "id": 16,
  "message": "Employee updated successfully."
}
```

For employee not found:

**HTTP 404 Not Found**

For validation failure:

**HTTP 400 Bad Request**

For unauthorized/forbidden access:

**HTTP 403 Forbidden**

## 10. Architecture Requirements

Follow the existing Workforce Service architecture:

```text
API
 ↓
Application Command
 ↓
Command Handler
 ↓
Domain Entity
 ↓
Repository
 ↓
PostgreSQL
```

Follow the existing project conventions for:

- Commands
- Handlers
- DTOs
- Validators
- Repository interfaces
- Repository implementation
- Audit logging
- Exception handling
- API response format

Do not introduce a new architectural pattern.

## 11. Database

Do not recreate or reset the database.

Do not change existing primary keys or unrelated tables.

Use the existing `Employees` table and update only the required fields.

Preserve all existing relationships.

## 12. Tests

Add/update tests for:

- Successful employee update
- Employee not found
- Invalid email
- Duplicate email
- Invalid role
- Invalid manager
- Self-manager validation
- Unauthorized actor
- Employee ID remains unchanged
- CreatedBy remains unchanged
- CreatedAt remains unchanged
- Audit log is created
- Parallel automated security tests (BOLA, Parameter Tampering, Unauthorized Role, Input Validation)

## 13. Parallel Security Testing Requirements

Security testing must not be deferred until after implementation is complete. While developing the Edit Employee module, security testing must be conducted **in parallel** across three distinct testing methodologies:

### 13.1 DAST (Dynamic Application Security Testing) in Parallel

Execute runtime security testing against the active API endpoint (`PUT /api/Employees/{id}` or `PUT /api/v1/Employees/{id}`):

1. **Authentication & Token Handling:**
   - Verify unauthenticated requests return `401 Unauthorized`.
   - Verify requests with expired, malformed, or tampered JWT signatures return `401 Unauthorized`.
   - Verify `alg: none` algorithm confusion attack is rejected.

2. **Authorization & BOLA / IDOR Testing:**
   - Test access with each defined role: `SYSTEM_ADMIN`, `ADMIN`, `MANAGER`, `SUPERVISOR`, `EMPLOYEE`, `EXECUTIVE`.
   - Verify callers cannot edit employees outside their authorized organization unit or reporting hierarchy.
   - Verify users with insufficient roles (e.g., standard `EMPLOYEE`) receive `403 Forbidden`.
   - Verify the actor identity cannot be spoofed by injecting arbitrary `actorUserId` in the request body; the actor identity must be resolved strictly from the authenticated JWT claims.

3. **Mass Assignment & Parameter Tampering:**
   - Verify that attempts to overwrite immutable fields (`id`, `userId`, `employeeCode`, `createdAt`, `createdBy`, `organizationUnitId`) fail or are strictly ignored by domain mapping.
   - Test payload tampering with unexpected or extra JSON properties.

4. **Input Validation & Injection Probing:**
   - Test SQL Injection payloads (`' OR 1=1 --`, `'; DROP TABLE...`) in string fields (`name`, `email`, `mobile`) to ensure complete EF Core parameterization.
   - Test Cross-Site Scripting (XSS) / script tags in `name` and contact fields to ensure proper sanitization.
   - Test malformed emails, boundary lengths (extreme strings, null bytes, unicode characters).

5. **Rate Limiting & Error Handling:**
   - Confirm requests are governed by the Workforce Service rate-limiting policy (e.g., 60 requests/minute).
   - Verify invalid requests and exceptions return RFC 7807 Problem Details without exposing stack traces, DB schema names, or server infrastructure internals.

### 13.2 SAST (Static Application Security Testing) in Parallel

Perform continuous static security analysis on code as it is being written:

1. **Code Analyzer Checks:**
   - Run Roslyn and .NET security code analyzers against new/updated files (`Commands`, `Handlers`, `Validators`, `DTOs`, `Repositories`).
   - Ensure zero critical, high, or medium compiler and security analyzer warnings (`CA` and `Security` rules).

2. **Data Exposure & Logging:**
   - Verify audit logging records the modification event (`EmployeeUpdated`) without logging sensitive data (passwords, auth tokens, full PII payloads) into plain text logs.
   - Ensure no hardcoded credentials or debug flags exist in the codebase.

3. **Validation Completeness:**
   - Ensure FluentValidation validators cover all editable fields and edge cases before reaching handler logic.
   - Verify entity mapping uses explicit DTO-to-Domain mapping rather than direct blind object binding (preventing over-posting vulnerabilities).

### 13.3 Penetration Testing & Abuse Scenarios in Parallel

Simulate real-world attack scenarios against the Edit Employee flow:

1. **Privilege Escalation:**
   - Attempt to promote an employee's role to `SYSTEM_ADMIN` or `ADMIN` using a lower-privileged account (`SUPERVISOR` or `EMPLOYEE`).
   - Attempt to modify an Admin's employee profile using a Manager or Supervisor token.

2. **Hierarchy & Self-Referential Abuse:**
   - Attempt to assign the employee as their own manager (`managerId == employee.Id`).
   - Attempt to inject circular manager dependencies or invalid manager references.

3. **Race Conditions & Concurrency Attacks:**
   - Send concurrent simultaneous `PUT` requests attempting to assign identical email or mobile numbers across two different employees to test duplicate check race conditions.

4. **State Desynchronization:**
   - Attempt editing non-existent, soft-deleted, or inactive employees.

## 14. End-of-Module Security Testing Report Deliverable

Upon completion of the Edit Employee module development and test execution, an official, comprehensive Markdown report must be generated and committed:

- **Report File Location:** `docs/Edit_Employee_Security_Report.md` (or root `FFM-EDIT-EMPLOYEE-SECURITY-REPORT.md`)
- **Format:** Downloadable, clean GitHub-flavored Markdown (`.md`)

### 14.1 Required Report Structure

The generated security report must contain the following sections:

1. **Executive Summary & Verdict:**
   - High-level overview of the Edit Employee module implementation.
   - Overall Security Verdict (`PASS` / `FAIL`).
   - Date and environment of execution.

2. **Scope of Assessment:**
   - Endpoints tested (e.g., `PUT /api/v1/Employees/{id}`).
   - User roles tested (`SYSTEM_ADMIN`, `ADMIN`, `MANAGER`, `SUPERVISOR`, `EMPLOYEE`, `EXECUTIVE`, Unauthenticated).
   - Source code files evaluated (`Api`, `Application`, `Domain`, `Infrastructure`, `Tests`).

3. **DAST (Dynamic Security Testing) Results:**
   - Detailed results table of all evaluated DAST attack techniques.
   - Findings, remediation actions taken during development, and confirmed remaining count.

4. **SAST (Static Code Security Testing) Results:**
   - Code analyzer scan summary.
   - Validation rules coverage analysis.
   - Data protection and audit log sanitization verification.

5. **Penetration Testing & Abuse Scenario Outcomes:**
   - Scenario test results (Privilege escalation, BOLA/IDOR, Circular hierarchy, Parameter tampering, Concurrency).
   - Actual vs expected HTTP status codes and response bodies.

6. **Confirmed Vulnerability Breakdown Table:**
   - Breakdown by Severity: Critical, High, Medium, Low, Informational.
   - Breakdown by Status: Initial Findings, Remediated In-flight, Remaining Open.

7. **Verification Evidence & Test Proof:**
   - Test execution logs (Unit tests, Integration tests, Security tests).
   - Audit trail verification log proof confirming `EmployeeUpdated` entry in PostgreSQL.

8. **Sign-off & Acceptance Criteria:**
   - Strict condition: **0 Critical, 0 High, 0 Medium unmitigated vulnerabilities**.
   - Confirmation of adherence to Clean Architecture and existing Workforce Service conventions.

## 15. Important Constraints

Before modifying code:

1. Inspect the existing Create Employee implementation.
2. Inspect the current `Employee` entity.
3. Inspect the existing authorization/current-user implementation.
4. Inspect the existing audit-log implementation.
5. Reuse existing patterns wherever possible.
6. Do not modify unrelated modules.
7. Do not create or apply an EF migration unless a schema change is actually required.
8. Perform security testing (DAST, SAST, Penetration Testing) in parallel with feature development.
9. Build the Workforce Service after implementation.
10. Run all existing Workforce tests and newly created security tests.
11. Generate the final downloadable security test report (`docs/Edit_Employee_Security_Report.md`) at the conclusion of the module.
12. Report the files changed, build result, test result, security test report link, and any remaining issues.

Do not change Employee ID type or database schema as part of this task unless the current code requires it for the Edit Employee implementation.

