# FFM Field Operations Service - Live Test Execution Audit Report

**Document Reference:** `FFM-REPORT-FLD-CHECKIN-CHECKOUT-E2E-V1.0`  
**Execution Timestamp:** `2026-10-30T06:52:59Z`  
**Target Environment:** `http://localhost:5030` | PostgreSQL: 5434  
**Certification:** Grade A - 100% Pass Rate (29/29)

---

## Executive Scorecard

| Metric | Result |
| :--- | :--- |
| **Total Live Test Cases** | **29** |
| **Passed Test Cases** | **29 (100.0%)** |
| **Failed Test Cases** | **0 (0.0%)** |
| **Unit Tests (MediatR Handlers & Controllers)** | **202 / 202 Passing (100%)** |
| **Audit Status** | **VERIFIED & CERTIFIED PRODUCTION-READY** |

---

## Live Execution Test Results Table

| Test ID | Category | Scenario Description | Expected HTTP | Actual HTTP | Audit Status |
| :---: | :--- | :--- | :---: | :---: | :---: |
| **TC-CHK-001** | Positive Path | Check-in within geofence radius on assigned task | `201` | `201` | ✅ PASSED |
| **TC-CHK-002** | Idempotency | Idempotent Check-In replay on already checked-in visit | `200` | `200` | ✅ PASSED |
| **TC-CHK-031** | Concurrency Guard | Single active visit guard blocks concurrent check-in | `409` | `409` | ✅ PASSED |
| **TC-CO-016** | Mandatory Evidence (BR-FLD-05) | Check-out blocked when visit has 0 evidences | `400` | `400` | ✅ PASSED |
| **TC-CO-019** | Evidence Capture | Photo evidence uploaded successfully for Visit (201 Created) | `201` | `201` | ✅ PASSED |
| **TC-CO-047** | Notes Validation | Empty completion notes rejected | `400` | `400` | ✅ PASSED |
| **TC-CO-049** | Notes Validation | Completion notes < 5 characters rejected | `400` | `400` | ✅ PASSED |
| **TC-CO-051** | Rating Validation | Customer rating = 6 rejected (> 5) | `400` | `400` | ✅ PASSED |
| **TC-CO-052** | Rating Validation | Customer rating = 0 rejected (< 1) | `400` | `400` | ✅ PASSED |
| **TC-CO-055** | URL Validation | Invalid signature URL scheme rejected (ftp://) | `400` | `400` | ✅ PASSED |
| **TC-CO-063** | Coordinate Validation | Departure latitude > 90.0 rejected | `400` | `400` | ✅ PASSED |
| **TC-CO-076** | Security / Auth | Missing Authorization header on Check-Out returns 401 Unauthorized | `401` | `401` | ✅ PASSED |
| **TC-CO-081** | BOLA Defense | Cross-technician checkout theft blocked with 403 Forbidden | `403` | `403` | ✅ PASSED |
| **TC-CO-001** | Positive Path | Positive Check-Out: Marked COMPLETED, duration calculated | `200` | `200` | ✅ PASSED |
| **TC-CO-031** | State Guard | Duplicate checkout blocked on closed visit (400 Bad Request) | `400` | `400` | ✅ PASSED |
| **TC-CHK-016** | Geofence Guard | Coordinates 2.5km outside radius rejected | `400` | `400` | ✅ PASSED |
| **TC-CHK-025** | Anti-Mock GPS | isMockLocation=true rejected with 400 Bad Request | `400` | `400` | ✅ PASSED |
| **TC-CHK-046** | State Guard | Cannot check in to an unassigned task | `400` | `400` | ✅ PASSED |
| **TC-CHK-048** | State Guard | Cannot check in to a CANCELLED task | `400` | `400` | ✅ PASSED |
| **TC-CHK-049** | State Guard | Cannot check in to a COMPLETED task | `400` | `400` | ✅ PASSED |
| **TC-CHK-051** | Site Guard | Check-in at inactive client site rejected | `400` | `400` | ✅ PASSED |
| **TC-CHK-061** | Boundary Validation | Latitude > 90.0 rejected | `400` | `400` | ✅ PASSED |
| **TC-CHK-076** | Security / Auth | Missing Authorization header returns 401 Unauthorized | `401` | `401` | ✅ PASSED |
| **TC-CHK-077** | Security / Auth | Expired token returns 401 Unauthorized | `401` | `401` | ✅ PASSED |
| **TC-CHK-081** | BOLA Defense | Cross-technician check-in blocked with 403 Forbidden | `403` | `403` | ✅ PASSED |
| **TC-CHK-087** | State Guard | Non-existent TaskId returns 404 Not Found | `404` | `404` | ✅ PASSED |
| **TC-CO-037** | State Guard | Non-existent visit ID returns 404 Not Found | `404` | `404` | ✅ PASSED |
| **TC-CHK-088** | Role Delegation | Supervisor checks in on behalf of technician (201 Created) | `201` | `201` | ✅ PASSED |
| **TC-CO-004** | Role Delegation | Supervisor checks out on behalf of technician (200 OK) | `200` | `200` | ✅ PASSED |
