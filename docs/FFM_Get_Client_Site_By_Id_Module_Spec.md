# Get Client Site By ID Module Specification

## 1. Document Metadata

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-FLD-GET-SITE-BY-ID-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Service** | Field Operations Service (`FFM.FieldOperations`) |
| **Module** | Client Site & Geofence Directory — Get Client Site By ID |
| **Endpoint** | `GET /api/v1/ClientSites/{id}` |
| **Compliance** | ISO/IEC/IEEE 29148, OpenAPI 3.1, RFC 9110, RFC 7807 |
| **Authentication** | Bearer JWT RS256 (issued by Identity & Access Service) |
| **Classification** | Tier‑1 Core Operational Query |
| **Status** | Approved Formal Specification |

---

## 2. Objective & Functional Scope

The **Get Client Site By ID** endpoint provides direct, high-performance lookup of an individual client site by its primary database key (`id`). It returns comprehensive site telemetry, including geographic coordinates, allowable geofence radius, full street address, point of contact details, and activation status.

### Key Capabilities:
1. **Direct Entity Retrieval:** Fetches authoritative details for a single client site.
2. **Pre-Check-In Geofence Resolution:** Mobile devices query this endpoint to retrieve site geofence radius and target coordinates before field personnel initiate on-site check-in.
3. **Task Detail Integration:** Displays customer site metadata on work order detail views in dispatcher portals.
4. **Strict Route Constraint:** Protected by `{id:int}` ASP.NET Core route constraint to reject non-integer path segments before reaching application logic.

---

## 3. Authorization & RBAC Policy

| Role | Allowed? | Scope / Enforcement |
| :--- | :---: | :--- |
| `SYSTEM_ADMIN` | Yes | Unrestricted read access across all client sites. |
| `ADMIN` | Yes | Read access to any client site within the tenant. |
| `MANAGER` | Yes | Read access to customer sites for work order planning and dispatching. |
| `DISPATCHER` | Yes | Read access to inspect customer locations for task assignment. |
| `EMPLOYEE` | Yes | Read access to inspect assigned task destination, phone contacts, and geofence parameters. |

* **Unauthenticated Requests:** Returns `401 Unauthorized`.
* **Token Verification:** Validates RS256 cryptographic signature, expiration time, and audience claims.

---

## 4. Business Rules & Validation Policies

| Rule ID | Title | Rule Description | Enforcement |
| :--- | :--- | :--- | :--- |
| **BR-GSI-01** | Integer Route Constraint | The path parameter `{id}` must be a valid positive 32-bit signed integer (`1` to `2,147,483,647`). Non-integer values (e.g. `abc`, `1.5`) fail the route constraint and return `404 Not Found`. | Enforced via route template `[HttpGet("{id:int}")]`. |
| **BR-GSI-02** | Entity Existence Check | If no client site exists with the specified `Id`, the service returns HTTP 404 Not Found with error code `NOT_FOUND`. | Verified by repository lookup in controller. |
| **BR-GSI-03** | Status Independence | Direct ID lookup retrieves the site regardless of whether `IsActive` is `true` or `false` (allowing managers to inspect historical sites bound to completed tasks). | Repository query does not filter on `IsActive`. |
| **BR-GSI-04** | Idempotency & Cache Safety | The operation is strictly read-only and idempotent. Repeating the request produces identical results without altering entity timestamps or states. | RFC 9110 HTTP GET compliance. |

---

## 5. API Contract & Interface Specification

### 5.1 Request Details
* **HTTP Method:** `GET`
* **Route:** `/api/v1/ClientSites/{id}`
* **Path Parameters:**

| Parameter | Type | Required? | Constraints | Description |
| :--- | :---: | :---: | :---: | :--- |
| `id` | `integer` | Yes | Min: `1`, Max: `2147483647` (Int32) | Primary key ID of the target client site. |

* **Headers:**
  - `Authorization: Bearer <JWT_TOKEN>` (Mandatory)
  - `Accept: application/json`

---

### 5.2 Response Contracts

#### Success Response (HTTP 200 OK):
```json
{
  "success": true,
  "message": "Client site retrieved successfully",
  "data": {
    "id": 1,
    "siteCode": "SITE-CHN-001",
    "name": "Apollo Hospitals Main Campus",
    "contactPerson": "Dr. Sivakumar",
    "contactPhone": "+919840123456",
    "contactEmail": "admin@apollo.com",
    "latitude": 13.0826800,
    "longitude": 80.2707100,
    "radiusMeters": 150,
    "address": "21 Greams Lane, Thousand Lights, Chennai",
    "city": "Chennai",
    "state": "Tamil Nadu",
    "postalCode": "600006",
    "isActive": true,
    "createdAt": "2026-10-21T10:30:00Z"
  },
  "errorCode": null,
  "errors": null
}
```

#### Entity Not Found Response (HTTP 404 Not Found):
```json
{
  "success": false,
  "message": "Client site with ID 999 not found.",
  "data": null,
  "errorCode": "NOT_FOUND",
  "errors": null
}
```

#### Route Constraint Mismatch (HTTP 404 Not Found):
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

#### Unauthorized Error (HTTP 401 Unauthorized):
```json
{
  "success": false,
  "message": "Unauthorized: Missing or invalid authentication token.",
  "data": null,
  "errorCode": "UNAUTHORIZED",
  "errors": null
}
```

---

## 6. Security & Attack Defenses
1. **Path Traversal & Injection Defense:** Because `{id:int}` enforces strict integer parsing at the ASP.NET Core routing middleware layer, path traversal attacks (`../`), SQL injection payloads, and script tags (`<script>`) are rejected at routing before executing any database code.
2. **Buffer Overflow & Integer Overflow Defense:** 64-bit integer values exceeding `int.MaxValue` (`2,147,483,647`) fail the 32-bit integer route constraint cleanly without throwing an unhandled overflow exception.
3. **Information Disclosure Prevention:** Stack traces and internal database errors are shielded behind standard `ApiResponse<object>` error wrappers.
