# Activate and Deactivate Employee Specification

## 1. Document Overview

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-WORKFORCE-ACTIVATE-DEACTIVATE-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Service** | Workforce Service (`FFM.Workforce`) |
| **Compliance** | ISO/IEC/IEEE 29148, OpenAPI 3.1 |
| **Status** | Approved Specification |

---

## 2. Purpose & Scope

The **Activate and Deactivate Employee Module** provides authorized administrators and managers the capability to manage employee operational statuses. It supports both **single-employee** actions and **bulk/batch** operations for field workforce management.

### Key Business Goals:
1. **Access Revocation (Deactivation)**: Instantly revoke login and operational capabilities (check-in, leave requests, expense claims) when an employee resigns, is terminated, or is suspended.
2. **Onboarding Completion (Activation)**: Transition newly created profiles from `PENDING_ACTIVATION` to `ACTIVE` once onboarding/device binding is complete.
3. **Rehire / Reactivation**: Allow deactivated profiles to be reactivated (`ACTIVE`) without losing historical data or creating duplicate records.
4. **Batch Processing**: Allow admins/managers to select multiple employees in the UI and activate/deactivate them in a single batch request.

---

## 3. API Endpoints

### Summary Table

| HTTP Method | Endpoint Path | Action | Payload | Authorization Policy |
| :--- | :--- | :--- | :--- | :--- |
| **`POST`** | `/api/v1/Employees/status` | Update Single Employee Status | Request Body: `{ "id": 42, "isActive": true }` | `CanEditEmployee` |
| **`POST`** | `/api/v1/Employees/status-batch` | Update Multiple Employees Status | `BatchEmployeeStatusChangeRequest` JSON | `CanEditEmployee` |

---

## 4. API Contracts & Schemas

### 4.1 Single Employee Status Update (`POST /api/v1/Employees/status`)

* **Headers**: `Authorization: Bearer <JWT>`, `Content-Type: application/json`
* **Request Body** (`SingleEmployeeStatusChangeRequest`):
  ```json
  {
    "id": 42,
    "isActive": true
  }
  ```
* **Success Response (HTTP 200 OK)**:
  ```json
  {
    "success": true,
    "data": {
      "id": 42,
      "isActive": true
    },
    "message": "Employee activated successfully."
  }
  ```
* **Error Responses**:
  * `400 Bad Request`: `{"success": false, "error": {"code": "BAD_REQUEST", "message": "Invalid employee ID."}}`
  * `401 Unauthorized`: Missing or expired JWT token.
  * `403 Forbidden`: User does not possess `CanEditEmployee` policy permission.
  * `404 Not Found`: Employee with specified ID does not exist.

---

### 4.3 Batch Employee Activation (`POST /api/v1/Employees/activate-batch`)

* **Headers**: `Authorization: Bearer <JWT>`
* **Request Body (JSON)**:
  ```json
  {
    "employeeIds": [211, 212, 213]
  }
  ```
* **Success Response (HTTP 200 OK)**:
  ```json
  {
    "message": "3 employee(s) activated successfully.",
    "count": 3
  }
  ```

---

### 4.4 Batch Employee Deactivation (`POST /api/v1/Employees/deactivate-batch`)

* **Headers**: `Authorization: Bearer <JWT>`
* **Request Body (JSON)**:
  ```json
  {
    "employeeIds": [211, 212, 213]
  }
  ```
* **Success Response (HTTP 200 OK)**:
  ```json
  {
    "message": "3 employee(s) deactivated successfully.",
    "count": 3
  }
  ```

---

## 5. Domain State Machine & Status Transitions

### Status Enum (`EmployeeStatus`):
* `PENDING_ACTIVATION` (0)
* `ACTIVE` (1)
* `DEACTIVATED` (2)

### State Transition Matrix:

```mermaid
stateDiagram-v8
    [*] --> PENDING_ACTIVATION : Employee Created
    PENDING_ACTIVATION --> ACTIVE : Activate Endpoint
    ACTIVE --> DEACTIVATED : Deactivate Endpoint
    DEACTIVATED --> ACTIVE : Activate Endpoint (Reactivation)
```

| Current Status | Target Action | Allowed? | Resulting Status | Exception / Response |
| :--- | :--- | :--- | :--- | :--- |
| `PENDING_ACTIVATION` | Activate | ✅ Yes | `ACTIVE` | Success (200 OK) |
| `DEACTIVATED` | Activate | ✅ Yes | `ACTIVE` | Success (200 OK) |
| `ACTIVE` | Activate | ❌ No | Unchanged | `InvalidOperationException`: "Employee is already active." |
| `ACTIVE` | Deactivate | ✅ Yes | `DEACTIVATED` | Success (200 OK) |
| `PENDING_ACTIVATION` | Deactivate | ✅ Yes | `DEACTIVATED` | Success (200 OK) |
| `DEACTIVATED` | Deactivate | ❌ No | Unchanged | `InvalidOperationException`: "Employee is already deactivated." |

---

## 6. Business & Security Rules

| Rule ID | Rule | Description |
| :--- | :--- | :--- |
| **BR-ACT-1** | **Role Authorization** | Only users with `SYSTEM_ADMIN`, `ADMIN`, or `MANAGER` roles may perform single or batch activation/deactivation. |
| **BR-ACT-2** | **BOLA Protection** | Operational actors can only activate/deactivate employees within their authorized scope/organization hierarchy. |
| **BR-ACT-3** | **State Enforcement** | An operation is idempotent: activating an already `ACTIVE` employee or deactivating an already `DEACTIVATED` employee is rejected. |
| **BR-ACT-4** | **Batch Validation** | Batch requests must contain at least one valid employee ID (`employeeIds.Count > 0`). |
| **BR-ACT-5** | **Data Retention** | Deactivation does not delete records; historical check-ins, leave records, and expense claims are preserved for audit purposes. |

---

## 7. Error Handling & HTTP Status Codes

| Status Code | Reason | Example Response Body |
| :--- | :--- | :--- |
| **`400 Bad Request`** | Invalid request payload or idempotent state rule violation | `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Bad Request","status":400,"detail":"Employee is already active."}` |
| **`401 Unauthorized`** | Missing or expired JWT Access Token | `{"message":"Unauthorized"}` |
| **`403 Forbidden`** | Insufficient role permissions | `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.4","title":"Forbidden","status":403,"detail":"Actor does not have permission to activate employees."}` |
| **`404 Not Found`** | Employee ID does not exist | `{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"Employee with ID 999 not found."}` |
