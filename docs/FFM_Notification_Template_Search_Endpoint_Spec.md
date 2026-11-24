# FFM Notification Service — Search & List Templates Endpoint Specification

## 1. Document Overview
* **Document Reference:** `FFM-SPEC-NOTIF-TEMPLATES-SEARCH-V1.0`
* **Service:** `FFM.Notification` (Port 5040)
* **Module:** Module 2: Template Management — Unified Search & List
* **Method & Route:** `GET /api/v1/Notifications/templates`
* **Architectural Standard:** Strictly Zero Path Parameters. Single optional query parameter `query` searching across all database columns.
* **Authentication:** Bearer JWT (Roles: `ADMIN`, `SYSTEM_ADMIN`, `MANAGER`) or DevBypass.

---

## 2. Business Scope & Purpose
Replaces fragmented lookup endpoints (`/templates/list` and `/templates/by-code`) with a single, high-performance query endpoint.
Accepts an optional universal search term `query` that matches across:
1. `TemplateCode` (e.g. `TASK_ASSIGNED`, `GEOFENCE_BREACH`)
2. `Channel` (e.g. `PUSH`, `SMS`, `EMAIL`)
3. `Title` (e.g. `Urgent Assignment`, `Geofence Alert`)
4. `BodyTemplate` (e.g. `TaskNumber`, `Apollo Hospital`)
5. `Description` (e.g. `breakdown`, `supervisor alert`)

If `query` is empty or omitted, returns all notification templates in alphabetical order.

---

## 3. API Contract

### Request: `GET /api/v1/Notifications/templates`
**Query Parameters:**
* `query` (string, optional) — Single search term matching across any column in the database.

**Examples:**
* `GET /api/v1/Notifications/templates` $\rightarrow$ Returns all templates.
* `GET /api/v1/Notifications/templates?query=CANCEL` $\rightarrow$ Returns `TASK_CANCELLED`.
* `GET /api/v1/Notifications/templates?query=breakdown` $\rightarrow$ Matches `Title` / `Description`.
* `GET /api/v1/Notifications/templates?query=PUSH` $\rightarrow$ Returns all templates using channel `PUSH`.

### Success Response (`HTTP 200 OK`):
```json
{
  "success": true,
  "message": "Templates retrieved successfully.",
  "data": [
    {
      "templateId": 1,
      "templateCode": "TASK_ASSIGNED",
      "channel": "PUSH",
      "title": "Urgent Assignment: {{TaskNumber}}",
      "bodyTemplate": "Hello {{EmployeeName}}, please immediately review task {{TaskNumber}} at {{SiteName}}.",
      "description": "Updated priority push alert for field technicians",
      "isActive": true,
      "createdByUserId": 1,
      "createdAt": "2026-11-12T04:47:49.82744Z",
      "updatedAt": "2026-11-12T11:45:14.084846Z"
    }
  ],
  "error": null,
  "timestamp": "2026-11-13T08:55:23.2499472Z"
}
```

