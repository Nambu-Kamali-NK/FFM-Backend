# Get All Client Sites Module Specification

## 1. Document Metadata

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-FLD-GET-ALL-SITES-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Service** | Field Operations Service (`FFM.FieldOperations`) |
| **Module** | Client Site & Geofence Directory — Get All Client Sites |
| **Endpoint** | `GET /api/v1/ClientSites` |
| **Compliance** | ISO/IEC/IEEE 29148, OpenAPI 3.1, RFC 9110, RFC 7807 |
| **Authentication** | Bearer JWT RS256 (issued by Identity & Access Service) |
| **Classification** | Tier‑1 Core Operational Query |
| **Status** | Approved Formal Specification |

---

## 2. Objective & Functional Scope

The **Get All Client Sites** endpoint provides a centralized directory query mechanism for web applications (Manager/Dispatcher dashboards) and mobile clients (Field Technicians). It retrieves client sites, their physical geographic coordinates (Latitude, Longitude), contact information, and configured geofence radii.

### Key Capabilities:
1. **Active Status Filtering:** Supports optional query parameter `activeOnly` (default `true`) to filter out retired or inactive customer locations.
2. **Geofence Directory Synchronization:** Enables the mobile client to cache authoritative site boundaries for offline GPS proximity evaluations.
3. **Task Creation Lookup:** Supplies dispatchers with eligible client destinations when scheduling tasks.
4. **Idempotency & Safe Method:** Read-only operation conforming to RFC 9110 HTTP GET semantics with zero side-effects on database state.

---

## 3. Authorization & RBAC Policy

| Role | Allowed? | Scope / Enforcement |
| :--- | :---: | :--- |
| `SYSTEM_ADMIN` | Yes | Retrieves all client sites across the platform. |
| `ADMIN` | Yes | Retrieves all client sites within the organization tenant. |
| `MANAGER` | Yes | Retrieves active/inactive sites for dispatching and operational reporting. |
| `DISPATCHER` | Yes | Retrieves active client sites to allocate work orders. |
| `EMPLOYEE` | Yes | Retrieves active client sites to locate assigned customer premises. |

* **Unauthenticated Requests:** Returns `401 Unauthorized`.
* **Token Verification:** Validates RS256 signature, expiry (`exp`), and audience (`aud: ffm-api`).

---

## 4. Business Rules & Query Validation Policies

| Rule ID | Title | Rule Description | Enforcement |
| :--- | :--- | :--- | :--- |
| **BR-GAS-01** | Default Filter Behavior | If `activeOnly` query parameter is omitted, the service defaults to `activeOnly = true`, returning only active sites (`IsActive == true`). | Applied in Controller parameter default. |
| **BR-GAS-02** | Inactive Site Inclusion | When `activeOnly = false`, the query returns both active and deactivated client sites (useful for historical task audits). | Repository filter condition evaluation. |
| **BR-GAS-03** | Boolean Model Binding | The `activeOnly` parameter must be a valid boolean (`true`, `false`, `1`, `0`). Invalid string tokens return HTTP 400 Bad Request. | ASP.NET Core model validation. |
| **BR-GAS-04** | Empty Dataset Contract | If no client sites match the filter criteria (or the database is empty), the service returns HTTP 200 OK with `data: []` (empty array), never `404 Not Found` or `null`. | Standard API collection response contract. |
| **BR-GAS-05** | Read Consistency | Queries are executed under `READ COMMITTED` transaction isolation to prevent dirty reads during concurrent site registration. | EF Core PostgreSQL default isolation. |

---

## 5. API Contract & Interface Specification

### 5.1 Request Details
* **HTTP Method:** `GET`
* **Route:** `/api/v1/ClientSites`
* **Query Parameters:**

| Parameter | Type | Required? | Default | Description |
| :--- | :---: | :---: | :---: | :--- |
| `activeOnly` | `boolean` | No | `true` | When `true`, filters only sites with `IsActive == true`. When `false`, returns all sites. |

* **Headers:**
  - `Authorization: Bearer <JWT_TOKEN>` (Mandatory)
  - `Accept: application/json`

---

### 5.2 Response Contracts

#### Success Response (HTTP 200 OK):
```json
{
  "success": true,
  "message": "Client sites retrieved successfully",
  "data": [
    {
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
    }
  ],
  "errorCode": null,
  "errors": null
}
```

#### Empty List Response (HTTP 200 OK):
```json
{
  "success": true,
  "message": "Client sites retrieved successfully",
  "data": [],
  "errorCode": null,
  "errors": null
}
```

#### Validation Error (HTTP 400 Bad Request):
```json
{
  "success": false,
  "message": "The value 'abc' is not valid for activeOnly.",
  "data": null,
  "errorCode": "VALIDATION_ERROR",
  "errors": [
    "The value 'abc' is not valid for activeOnly."
  ]
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

## 6. Security & Hardening Policies
1. **SQL Injection Defense:** All queries utilize EF Core LINQ expressions mapped to parameterized SQL (`WHERE "IsActive" = @p0`).
2. **Cross-Site Scripting (XSS):** Query parameters are validated via strongly typed boolean binders with zero HTML reflection.
3. **Header Security:** Responses enforce `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and suppress server banners (`Kestrel`).
4. **Transport Encryption:** Strictly restricted to TLS 1.3 / HTTPS.
