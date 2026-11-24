# FFM Notification Service — FCM & Push Dispatch Test Cases

## 1. Document Overview
* **Document Reference:** `FFM-TC-NOTIF-FCM-DISPATCH-V1.0`
* **Service:** `FFM.Notification` (Port 5040)
* **Scope:** Module 1 (Device Registration), Module 2 (Template Search), Module 3 (Push Notification Dispatch & Google FCM Integration)
* **Target Audience:** QA Engineers, Backend Developers, Tech Leads

---

## 2. Test Execution Summary
* **Total Automated Tests Executed:** 54 Test Cases
* **Passing:** 54 / 54 (100%)
* **Framework:** xUnit, Moq, FluentValidation, Microsoft.AspNetCore.Mvc.Testing

---

## 3. Detailed Test Matrix

### Category A: Google OAuth2 & FCM Server Authentication (`GET /api/v1/Notifications/fcm/access-token`)

| Test ID | Test Scenario | Input Data | Expected Result | Automated Test |
| :--- | :--- | :--- | :--- | :---: |
| **TC-FCM-001** | Live Google OAuth2 Handshake | Valid `firebase-service-account.json` | Returns HTTP 200 with `accessToken` starting with `ya29.` and 3600s TTL. | `FcmAuthServiceTests` |
| **TC-FCM-002** | Missing Service Account Key | Non-existent path | Throws `FileNotFoundException` and logs error. | `FcmAuthServiceTests` |
| **TC-FCM-003** | Controller Success Response | Valid Mocked Service | Returns HTTP 200 with structured `ApiResponse<FcmTokenResult>`. | `FcmControllerTests` |
| **TC-FCM-004** | Google Server Unreachable | Service throws exception | Controller catches and returns HTTP 500 (`FCM_AUTH_FAILED`). | `FcmControllerTests` |
| **TC-FCM-005** | Token Project ID Verification | Valid credentials | Verifies `projectId` matches `"ffm-notification-api"`. | `FcmAuthServiceTests` |

---

### Category B: Push Notification Dispatch (`POST /api/v1/Notifications/send`)

| Test ID | Test Scenario | Input Data | Expected Result | Automated Test |
| :--- | :--- | :--- | :--- | :---: |
| **TC-DSP-001** | Happy Path Push Delivery | Recipient with active device, valid template | Dispatches to FCM, returns `status: "DELIVERED"`, records Google message ID. | `SendNotificationCommandHandlerTests` |
| **TC-DSP-002** | Recipient Has No Active Device | Employee logged out or no registered device | Returns `status: "NO_ACTIVE_DEVICE"`, audit log saved with warning. | `SendNotificationCommandHandlerTests` |
| **TC-DSP-003** | Template Not Found | `templateCode: "UNKNOWN"` | Throws `KeyNotFoundException`, returns HTTP 404 (`TEMPLATE_NOT_FOUND`). | `DispatchControllerTests` |
| **TC-DSP-004** | Inactive Template Dispatch | `isActive: false` | Throws `InvalidOperationException`, returns HTTP 400 (`TEMPLATE_INACTIVE`). | `DispatchControllerTests` |
| **TC-DSP-005** | Empty Request Body | `null` | Returns HTTP 400 (`INVALID_PAYLOAD`). | `DispatchControllerTests` |
| **TC-DSP-006** | Invalid Recipient Employee ID | `recipientEmployeeId: 0` or negative | Validation fails, returns HTTP 400 (`VALIDATION_ERROR`). | `DispatchControllerTests` |
| **TC-DSP-007** | Invalid Delivery Channel | `channel: "CARRIER_PIGEON"` | Validation fails: channel must be PUSH, SMS, EMAIL, IN_APP. | `DispatchControllerTests` |
| **TC-DSP-008** | Dynamic Placeholder Merging | `{{TaskNumber}}`, `{{SiteName}}` | Replaces variables in title and body with payload dictionary values. | `SendNotificationCommandHandlerTests` |
| **TC-DSP-009** | Missing Placeholders Graceful Fallback | Template has `{{MissingKey}}` | Replaces missing variable with empty string gracefully without error. | `TemplateInterpolationEngineTests` |
| **TC-DSP-010** | Multiple Active Devices Priority | Employee has 2 registered devices | Dispatches to the device with the most recent `LastActiveAt` timestamp. | `SendNotificationCommandHandlerTests` |

---

### Category C: Device Registration & FCM Token Management (`POST /devices/register`, `POST /devices/token`)

| Test ID | Test Scenario | Input Data | Expected Result | Automated Test |
| :--- | :--- | :--- | :--- | :---: |
| **TC-DEV-001** | Register New Device Token | New `deviceId` and valid `fcmToken` | Creates new device row, sets `isActive = true`, returns HTTP 200. | `RegisterDeviceCommandHandlerTests` |
| **TC-DEV-002** | Refresh Existing Device Token | Same `deviceId` with updated `fcmToken` | Updates token in place, refreshes `lastActiveAt`, maintains ID. | `RegisterDeviceCommandHandlerTests` |
| **TC-DEV-003** | Deactivate Other Devices on Login | Same employee logging into Phone B | Deactivates Phone A for that platform, sets Phone B active. | `RegisterDeviceCommandHandlerTests` |
| **TC-DEV-004** | Lookup Active Device Token | `POST /devices/token` with `employeeId: 1` | Returns active `fcmToken`, `platform`, `deviceModel`. | `DevicesControllerTests` |
| **TC-DEV-005** | Lookup Device for Non-existent Employee | `POST /devices/token` with `employeeId: 99999` | Returns HTTP 404 (`DEVICE_NOT_FOUND`). | `DevicesControllerTests` |
| **TC-DEV-006** | Empty FCM Token Validation | `fcmToken: ""` | Validation rejects: token must not be empty. | `DeviceValidatorsTests` |
| **TC-DEV-007** | Platform Normalization | `platform: "android"` | Automatically stored in uppercase `"ANDROID"`. | `RegisterDeviceCommandHandlerTests` |

---

### Category D: Unified Template Search (`GET /api/v1/Notifications/templates`)

| Test ID | Test Scenario | Input Data | Expected Result | Automated Test |
| :--- | :--- | :--- | :--- | :---: |
| **TC-TMP-001** | Search with No Query Parameter | `GET /templates` | Returns all 7 notification templates. | `TemplatesControllerTests` |
| **TC-TMP-002** | Search by Code | `?query=CANCEL` | Matches `TASK_CANCELLED` by `TemplateCode`. | `TemplatesControllerTests` |
| **TC-TMP-003** | Search by Channel | `?query=PUSH` | Returns all PUSH templates. | `TemplatesControllerTests` |
| **TC-TMP-004** | Search Across Description / Body | `?query=breakdown` | Returns `EMERGENCY_EQUIPMENT_REPAIR` matching description. | `TemplatesControllerTests` |
| **TC-TMP-005** | Case-Insensitive Search | `?query=assigned` | Returns `TASK_ASSIGNED` and `TASK_DISPATCHED`. | `TemplatesControllerTests` |
| **TC-TMP-006** | No Match Search Query | `?query=NON_EXISTENT_XYZ` | Returns HTTP 200 with empty list `[]`. | `TemplatesControllerTests` |

