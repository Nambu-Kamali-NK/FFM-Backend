# Get Employee & Get All Employees Specification

## 1. Purpose

Provide secure, role-controlled endpoints to retrieve employee details:
1. **Get Employee by ID (`GET /api/v1/Employees/{id}`)**: Retrieves a single employee's profile by their numeric Employee ID.
2. **Get All Employees (`GET /api/v1/Employees`)**: Retrieves a list of employees accessible to the caller based on role and organizational unit boundaries.

---

## 2. API Endpoints

### 2.1 Get Employee by ID
- **Method:** `GET`
- **Endpoint:** `/api/v1/Employees/{id}`
- **Path Parameter:** `{id}` — 32-bit signed integer (`int`)

### 2.2 Get All Employees
- **Method:** `GET`
- **Endpoint:** `/api/v1/Employees`
- **Parameters:** None (Zero query/path parameters)

---

## 3. Data Transfer Object (Response)

Both endpoints return data mapped to `EmployeeDto`:

```json
{
  "id": 1,
  "employeeCode": "EMP1001",
  "userId": 101,
  "name": "Jane Doe",
  "roleId": 6,
  "orgUnitId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "managerId": 2,
  "email": "jane.doe@example.com",
  "mobile": "9876543210",
  "status": "ACTIVE",
  "createdAt": "2026-09-30T10:00:00Z",
  "createdBy": 1
}
```

---

## 4. Role-Based Access Control & BOLA Rules

The system enforces strict Broken Object Level Authorization (BOLA/IDOR) protection:

| Caller Role | Access Scope (`GET /api/v1/Employees/{id}`) | Access Scope (`GET /api/v1/Employees`) |
| :--- | :--- | :--- |
| **SYSTEM_ADMIN** | Can access any employee in the system | Returns all employees in the system |
| **ADMIN** | Can access any employee in the system | Returns all employees in the system |
| **EXECUTIVE** | Can access any employee in the system | Returns all employees in the system |
| **MANAGER** | Can access self, employees in same `OrgUnitId`, or direct reports | Returns self + employees in same `OrgUnitId` + direct reports |
| **SUPERVISOR** | Can access self or direct reports (`managerId == supervisor.Id`) | Returns self + direct reports (`managerId == supervisor.Id`) |
| **EMPLOYEE** | Can access ONLY their own record (`UserId == currentUserId`) | Returns ONLY their own record (`UserId == currentUserId`) |
| **Unauthenticated** | Denied (`401 Unauthorized`) | Denied (`401 Unauthorized`) |

---

## 5. Processing Flow

### 5.1 Get Employee by ID Flow
```text
Request GET /api/v1/Employees/{id}
  ↓
Authenticate Bearer Token (JWT)
  ↓
Validate Employee ID (> 0)
  ↓
Query Employee Repository (GetByIdAsync)
  ↓
Entity Exists? ──(No)──> 404 Not Found
  ↓ (Yes)
Evaluate Authorization Rules (Roles & BOLA Boundaries)
  ↓
Authorized? ──(No)──> 403 Forbidden
  ↓ (Yes)
Map Employee Entity to EmployeeDto
  ↓
Return 200 OK (EmployeeDto)
```

### 5.2 Get All Employees Flow
```text
Request GET /api/v1/Employees
  ↓
Authenticate Bearer Token (JWT)
  ↓
Query Employee Repository (GetAllAsync)
  ↓
Evaluate Caller Roles (GetRoles & GetUserId)
  ↓
Apply Role-Based Scope Filter (SystemAdmin/Admin/Executive vs Manager vs Supervisor vs Employee)
  ↓
Map Filtered Entities to List<EmployeeDto>
  ↓
Return 200 OK (List<EmployeeDto>)
```

---

## 6. Error Responses

All error responses strictly comply with RFC 7807 Problem Details and omit internal system details:

- **401 Unauthorized:** Missing or invalid JWT Bearer token.
- **403 Forbidden:** Caller lacks permission to view the requested employee record.
- **404 Not Found:** Employee with the requested numeric ID does not exist.

---

## 7. Security Verification Criteria

1. **BOLA / IDOR Verification:** Automated test suites verify that non-admin callers cannot access unauthorized records outside their org unit or reporting hierarchy.
2. **PII & Data Leak Prevention:** Audit fields and response DTOs sanitize password hashes, MFA keys, and internal secrets.
3. **Numeric Identity Consistency:** Employee IDs are integer identity sequences (`int`).
