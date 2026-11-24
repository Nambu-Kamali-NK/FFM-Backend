# Formal Technical Specification Document (Backend SDD)

## Module: Authentication & Access Control – Login Module
### System: Field Force Management (FFM) Platform

| Document Attribute | Specification Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-AUTH-LOGIN-V1.1` |
| **Standard Compliance** | ISO/IEC/IEEE 29148, OAuth 2.0 (RFC 6749), OpenID Connect Core 1.0 |
| **Module Classification** | Tier‑1 Core Security Infrastructure |
| **Status** | Approved Formal Specification with Approved Implementation Baseline (Single Source of Truth) |
| **Target Clients** | FFM Web Portal (Admin/Dispatcher) & FFM Mobile / PWA Client (Field Agent) |
| **Perspective** | **Backend / Server‑Side Only** |

---

## 1️⃣ Pre‑development Checklist & Commands Used

### 1.1 Checklist
- All NuGet packages restore cleanly (`dotnet restore`).
- PostgreSQL connection string in `appsettings.json` points to a reachable DB; role `ffmid_user` has LOGIN rights.
- EF Core migrations are applied (`dotnet ef database update`) or `EnsureCreated` runs on startup.
- JWT settings (Issuer, Audience, signing key) are defined and meet length requirements.
- External services (MailKit, SMS gateway) have valid credentials.
- `ASPNETCORE_ENVIRONMENT` set appropriately.
- Docker images built; host ports 5000/5001 free.

### 1.2 Commands Executed During Development
#### .NET / EF Core
- `dotnet restore`
- `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL`
- `dotnet ef migrations add InitIdentityPg`
- `dotnet ef database update`

#### Docker
- `docker compose build`
- `docker compose up -d --build`
- `docker exec -it ffm-identity dotnet ef database update --no-build`
- `docker logs ffm-identity`

#### Testing
- ```bash
  curl -X POST http://localhost:5000/api/Auth/login \
       -H "Content-Type: application/json" \
       -d '{"email":"nambukamali@example.com","password":"nambu123"}'
  ```
- Open `http://localhost:5000/swagger` in a browser.

All commands are reflected in the project's `Dockerfile` and `docker‑compose.yml`.

---

## 2️⃣ Technical Glossary
- **Backend / Server** – Code that runs in the data‑center handling logic, DB queries, and security.
- **Authentication** – Proving *who* the caller is (password verification).
- **Authorization / RBAC** – Granting access based on role.
- **Hash / Argon2id / bcrypt** – One‑way password hashing; only hashes are stored.
- **Access Token (JWT)** – Short‑lived (≈15 min) token for API calls.
- **Refresh Token** – Long‑lived (≈30 days) token that rotates on each use.
- **MFA** – Multi‑Factor Authentication (OTP via SMS/Email).
- **Device Binding** – Binding a user account to a specific mobile device UUID.
- **Rate Limiting / Brute‑Force Safeguard** – Locking after repeated failures.
- **Audit Log** – Immutable record of every authentication event.

---

## 3️⃣ Scope & System Overview
The Login Module is the security perimeter for the FFM platform. It provides:
- Credential verification against Argon2id/bcrypt hashes.
- Account status and lock‑out handling.
- Optional step‑up MFA.
- Optional device validation (prevent buddy‑punching).
- Dual‑token issuance (Access + Refresh) and secure token refresh.
- Full audit logging of all auth events.

---

## 4️⃣ Architectural Overview
- **Protocol**: OpenID Connect on top of OAuth 2.0.
- **Token Pattern**: Short‑lived Access Token (RS256) + long‑lived Refresh Token (hashed, rotated).
- **Deployment**: Stateless ASP.NET Core Web API running in a Docker container, backed by PostgreSQL.
- **Health‑Check**: Docker Compose health‑check ensures DB readiness before the API starts.

---

## 5️⃣ Functional Requirements (FR)
| ID | Description |
|---|---|
| **FR1 – User Authentication** | Verify identifier + password using Argon2id (fallback bcrypt). Constant‑time checks; generic error messages. |
| **FR2 – Account Validation** | Ensure `account_status == ACTIVE` and not locked. Reset lock after successful login. |
| **FR3 – MFA Verification** | Generate 6‑digit OTP (300 s TTL), store HMAC, dispatch via MailKit/SMS, return `202 Accepted` with `mfaSessionId`. Verify code, rotate refresh token. |
| **FR4 – Device Validation** | If enabled, confirm `deviceId` is registered, approved, and belongs to the user. Auto‑bind first device per policy. |
| **FR5 – Token Generation** | Issue signed Access Token (15 min Web / 30 min Mobile) and Refresh Token (30 days). Store refresh token hash, rotate on each refresh. |
| **FR6 – Failed Login & Lock‑out** | Increment `failed_attempts`; on ≥ 5 attempts set `locked_until = NOW() + 15m` and return `423 Locked`. |
| **FR7 – Audit Logging** | Record immutable audit entry for every auth event (success/failure, IP, device, reason). |

---

## 6️⃣ Use Cases (UC)
### UC1 – Successful Login (Happy Path)
1. Client POST `/api/v1/auth/login` with `identifier`, `password`, `clientId`, optional `deviceId`.
2. Service validates credentials (FR1).
3. Service checks account status (FR2).
4. If MFA required → FR3; otherwise continue.
5. If device validation enabled → FR4.
6. Service resets `failed_attempts`, updates `last_login_at`.
7. Service issues Access & Refresh Tokens (FR5) and returns `200 OK`.
8. Audit log entry `LOGIN_SUCCESS` created.

### UC2 – Invalid Credentials
1. POST `/api/v1/auth/login` with wrong identifier/password.
2. Service fails hash verification, increments `failed_attempts` (FR6).
3. If `failed_attempts >= 5` → lock account, return `423 Locked`; else return `401 Unauthorized`.
4. Audit log entry `LOGIN_FAILED` created.

### UC3 – Step‑Up MFA
1. After credential validation, service determines MFA is required.
2. Generates OTP, stores HMAC, dispatches via SMS/Email, returns `202 Accepted` with `mfaSessionId`.
3. Client collects OTP, calls `POST /api/v1/auth/mfa/verify`.
4. Service validates OTP, marks challenge verified, then proceeds to device check and token issuance.
5. Invalid/expired OTP yields `400/401`; three invalid attempts invalidate the challenge.

### UC4 – Unauthorized Device (Buddy‑Punch Prevention)
1. Credentials valid, device validation enabled.
2. Service looks up `deviceId` in `user_devices`.
3. If not approved or belongs to another user → return `403 Forbidden` (`AUTH_DEVICE_UNAUTHORIZED`).
4. Audit log entry `LOGIN_FAILED` with reason recorded.

---

## 7️⃣ Business Rules (BR)
| Rule ID | Rule | Enforcement |
|---|---|---|
| **BR‑1** | Valid Credentials | Password hashed with Argon2id/bcrypt; generic failure messages; no secret leakage. |
| **BR‑2** | Active Account | Only `ACTIVE` accounts may authenticate; others rejected with appropriate HTTP status. |
| **BR‑3** | MFA Requirement | Mandatory for `SYSTEM_ADMIN`/`MANAGER`; risk‑based for field roles. OTP expires after 300 s. |
| **BR‑4** | Device Binding | When enabled, `deviceId` must be registered and approved. |
| **BR‑5** | Failed Attempt Lock‑out | ≥ 5 consecutive failures → lock for 15 min. Counter resets on successful login. |
| **BR‑6** | Secure Transport | All traffic over HTTPS/TLS 1.3 (minimum TLS 1.2). |
| **BR‑7** | Audit Logging | Immutable log entries; secrets redacted. |

---

## 8️⃣ API Contracts (OpenAPI 3.1 Summary)
### 8.1 POST `/api/v1/auth/login`
- **Headers**: `Content-Type: application/json`, optional `X-Correlation-ID`.
- **Request**:
```json
{ "identifier": "user@example.com", "password": "P@ssw0rd", "clientId": "ffm-mobile", "deviceId": "550e8400-e29b-41d4-a716-446655440000" }
```
- **Responses**:
  - `200 OK` – tokens and user claims.
  - `202 Accepted` – MFA required (`mfaSessionId`).
  - `401 Unauthorized` – invalid credentials.
  - `403 Forbidden` – inactive/blocked account or unauthorized device.
  - `423 Locked` – account temporarily locked.

### 8.2 POST `/api/v1/auth/mfa/verify`
```json
{ "mfaSessionId": "abc123", "verificationCode": "489201" }
```
- **200 OK** – same payload as successful login.
- **400/401** – expired or invalid code.

### 8.3 POST `/api/v1/auth/refresh-token`
- **Cookie**: `refreshToken=<token>`
- **200 OK** – new Access Token (and new Refresh Token).
- **401 Unauthorized** – refresh token invalid or revoked.

---

## 9️⃣ Data Model Overview (ER Diagram Summary)
- **USERS** → `REFRESH_TOKENS`, `USER_DEVICES`, `MFA_CHALLENGES`, `AUTH_AUDIT_LOGS`.
- Key fields: `password_hash`, `account_status`, `failed_attempts`, `locked_until`, `last_login_at`.
- Device table stores `device_uuid`, `is_approved`.
- MFA table stores HMAC of OTP, expiry, verification flag.

---

## 🔟 Security & Non‑Functional Requirements (NFR)
- **Password Hashing**: Argon2id (m=65536, t=3, p=4) or bcrypt (cost 12).
- **Transport**: TLS 1.3 mandatory.
- **Token Signing**: RS256 asymmetric keys; JWKS endpoint exposed.
- **Performance**: 95th‑percentile login latency ≤ 250 ms.
- **Scalability**: Stateless service; horizontal scaling via Docker/Kubernetes; connection pooling for PostgreSQL.
- **Availability**: 99.95% SLA.
- **Fail‑Secure**: Dependent services (SMS, email) failures must not expose internal details.

---

## 11️⃣ Error Dictionary
| Error Code | HTTP | User Message | Internal Reason |
|---|---|---|---|
| `AUTH_INVALID_CREDENTIALS` | 401 | Invalid username, email, or password. | Credential mismatch |
| `AUTH_ACCOUNT_INACTIVE` | 403 | Account not eligible. Contact support. | Account status not ACTIVE |
| `AUTH_ACCOUNT_LOCKED` | 423 | Account temporarily locked due to repeated failures. | Brute‑force lockout |
| `AUTH_MFA_REQUIRED` | 202 | MFA challenge sent. Verify code. | MFA challenge created |
| `AUTH_MFA_FAILED` | 401 | Invalid verification code. | OTP mismatch |
| `AUTH_MFA_EXPIRED` | 400 | Verification code expired. Please retry. | OTP TTL elapsed |
| `AUTH_DEVICE_UNAUTHORIZED` | 403 | Device not authorized for this user. | Device not bound/approved |
| `AUTH_RATE_LIMITED` | 429 | Too many attempts. Please wait. | Rate limit exceeded |

---

## 12️⃣ Acceptance Criteria
| AC | Requirement | Verification |
|---|---|---|
| **AC‑01** | Valid credentials authenticate active user | Integration test returns `200 OK` with valid JWT. |
| **AC‑02** | Invalid credentials rejected without tokens | Negative test returns `401`; no tokens returned. |
| **AC‑03** | Inactive/blocked accounts rejected | Test returns `403`/`423` accordingly. |
| **AC‑04** | MFA enforced when required | E2E test with MFA policy returns `202` then successful login after OTP. |
| **AC‑05** | Expired or invalid OTP rejected | Fault injection returns `400`/`401`. |
| **AC‑06** | Unauthorized device rejected | Device spoof test returns `403`. |
| **AC‑07** | Tokens contain correct `exp` claim | JWT inspection matches 900 s (Web) or 1800 s (Mobile). |
| **AC‑08** | Audit log created for every attempt | DB query confirms entry for each request. |
| **AC‑09** | No plaintext secrets in logs | Log scanning regex finds none. |
| **AC‑10** | 5 consecutive failures lock account for 15 min | Brute‑force test verifies `423` on 5th attempt and lock duration. |

---

## 13. Approved Implementation Baseline and Resolved Clarifications

This section is an approved amendment to the Login Module specification. It records decisions made after the initial version. Where this section conflicts with an earlier section, **this section takes precedence**. These decisions are requirements for the current implementation and are not optional recommendations.

### 13.1 Completion Rule

The implementer shall review and implement the **entire** Login Module specification, including this amendment, before declaring the module complete.

- A successful compilation proves only that the current source code can compile.
- Successful compilation, a health check, or a single successful API call shall **not** be treated as feature completion, specification compliance, code-review approval, or production readiness.
- Completion requires every mandatory functional, security, integration, reliability, operational, and test requirement in this document to be implemented and verified.
- All acceptance criteria in Section 12, plus the additional criteria in Section 13.15, must pass before the module is approved for production.
- The implementer shall report any incomplete requirement explicitly and shall not represent a partial foundation as a completed Login Module.

### 13.2 Approved Technology and Deployment Decisions

The following are approved technical decisions for this Login Module:

- ASP.NET Core Web API on the current supported .NET LTS version.
- PostgreSQL as the Identity service's relational database and Entity Framework Core for persistence/migrations.
- Redis for temporary authentication state, failed-attempt/rate-limit counters, and short-lived MFA state where applicable. Redis is not a permanent source of truth.
- RabbitMQ for asynchronous integration only. RabbitMQ must not be required to make the synchronous login, MFA verification, device-validation, or token-issuance decision.
- Linux Docker containers.
- OAuth 2.0 and OpenID Connect.
- JWT access tokens signed with RSA SHA-256 (`RS256`).
- Argon2id password hashing. bcrypt cost 12 remains a supported fallback for existing approved bcrypt password hashes.

Prisma shall not be used. Passwords, access tokens, refresh tokens, MFA secrets, OTP values, signing keys, connection strings, and other credentials shall never be written to application logs. All secrets and environment-specific values shall be supplied through secure configuration and shall not be hard-coded.

### 13.3 Canonical Roles and Authorization Model

The canonical FFM roles are:

- `SYSTEM_ADMIN`
- `ADMIN`
- `MANAGER`
- `SUPERVISOR`
- `EMPLOYEE`

`SYSTEM_ADMIN` and `ADMIN` are distinct roles and shall not be automatically mapped to one another.

Authorization shall use RBAC plus permissions. The permission model is `Resource.Action.Scope`; examples include `Attendance.View.Own`, `Task.Assign.Department`, and `Leave.Approve.Department`. JWT claims may contain roles and permissions, but every protected backend API shall enforce authorization server-side. Authentication failure returns `401`; authenticated callers lacking permission return `403`.

### 13.4 MFA Policy and Delivery

MFA shall be policy-driven.

- MFA is mandatory for `SYSTEM_ADMIN` and `ADMIN`.
- MFA may be required for `MANAGER`, `SUPERVISOR`, and `EMPLOYEE` according to configured policy or approved elevated-risk conditions.
- Initial elevated-risk conditions may include a new/unrecognized device, repeated failed authentication attempts, sensitive administrative operations, and any policy-required additional verification.
- MFA policy must be configurable and must not permanently hard-code MFA for field roles.
- An OTP is six digits, expires after 300 seconds, is stored only as an HMAC for verification, and must be invalidated after the configured maximum number of invalid attempts. The approved initial default is five invalid attempts per challenge.
- A successfully verified, expired, invalidated, or otherwise consumed challenge must never be reusable.
- Before issuing tokens after MFA, Identity must re-check the user account status, lock state, and required device state.

Identity & Access Service owns MFA challenge creation, authentication state, expiry, attempt counting, and verification. Notification Service owns actual SMS/email delivery.

Identity shall create the challenge and reliably create a `MfaDeliveryRequested` integration request through the transactional outbox. RabbitMQ delivery and Notification Service failures must not mark MFA as verified or change an authentication decision to successful. Notification Service shall use bounded retries, record delivery failures, and use a dead-letter process for permanent failures. OTP values, passwords, access tokens, refresh tokens, and other secrets shall not be placed in RabbitMQ messages or logs. Local development may use a controlled development-only OTP delivery sink; it must never be enabled in production.

### 13.5 Device Binding Policy

Device binding is configuration-driven and is primarily for `ffm-mobile` and `ffm-pwa`; web clients do not require it by default.

- A user may have one active registered mobile device unless configuration permits more.
- An unregistered device shall not silently become trusted unless auto-binding is explicitly enabled by configuration.
- The default behavior requires successful device registration/binding before unrestricted mobile authentication.
- Each device record belongs to exactly one user.
- Supported device states are `PENDING`, `ACTIVE`, `BLOCKED`, `REVOKED`, `REPLACED`.
- An authorized `SYSTEM_ADMIN` or `ADMIN` with the appropriate permission may revoke an existing device before a replacement device is registered.
- A replaced device shall not remain valid.
- A mobile/PWA request subject to device binding that has no device ID must be rejected or return `DeviceRegistrationRequired`; validation must not be silently bypassed.
- Use an application-generated device identifier or supported secure device identity mechanism. IMEI and other restricted hardware identifiers shall not be used.

Required device registration, approval, blocking, revocation, and replacement APIs/workflows are part of the Identity & Access Service implementation.

### 13.6 Canonical API Contract

The canonical paths are:

- `POST /api/v1/auth/login`
- `POST /api/v1/auth/mfa/verify`
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/auth/me`

The `/api/v1/auth/refresh` path supersedes the earlier `/api/v1/auth/refresh-token` path. The older `/api/Auth/login` development example is not a supported canonical path.

Login request:

```json
{
  "usernameOrEmail": "string",
  "password": "string",
  "clientId": "string",
  "deviceId": "string or null"
}
```

Successful authentication response:

```json
{
  "status": "Authenticated",
  "userId": "uuid",
  "accessToken": "string",
  "expiresIn": 900,
  "tokenType": "Bearer",
  "roles": ["EMPLOYEE"],
  "permissions": ["string"]
}
```

For non-browser clients, the response may additionally include `refreshToken`. Browser refresh tokens shall use an HTTPS-only cookie with `HttpOnly=true`, `Secure=true`, `SameSite=Lax` by default, a restricted path, and explicit expiration. `SameSite=None; Secure` is permitted only for approved cross-site authentication needs.

MFA required response:

```json
{ "status": "MfaRequired", "challengeId": "uuid", "expiresIn": 300 }
```

Device registration required response:

```json
{ "status": "DeviceRegistrationRequired" }
```

All errors shall use a consistent structured error response.

### 13.7 Registered Clients

The only initial canonical client IDs are `ffm-web`, `ffm-mobile`, and `ffm-pwa`. Client configuration shall define enabled state, allowed authentication flow, device-binding requirement, MFA-policy overrides, and allowed redirect URIs for OIDC flows. Identity shall not trust arbitrary caller-supplied client IDs.

### 13.8 Refresh Tokens and Sessions

Refresh tokens must be revocable, stored only as secure hashes, and rotated after use. Reuse of a rotated or revoked refresh token shall revoke the affected token family/session and be audited.

Multiple concurrent sessions are allowed by default; their maximum is configurable. Every session must reference the user, client, applicable device, issue time, expiry time, revoked state, and replacement/rotation relationship. Logout revokes the active session. Password or security-sensitive account changes may revoke all sessions according to policy.

### 13.9 OAuth 2.0, OpenID Connect, and Token Requirements

Identity & Access Service is the FFM Authorization Server/OpenID Provider unless an approved external enterprise identity provider replaces it.

- Provide standards-compatible discovery at `/.well-known/openid-configuration`.
- Provide JWKS at `/.well-known/jwks.json`, or the selected OIDC framework-standard JWKS endpoint.
- Support Authorization Code with PKCE for applicable interactive public clients.
- Support refresh-token flow.
- Support Client Credentials only where an explicit service-to-service requirement is approved.
- Do not use Resource Owner Password Credentials as a general OAuth flow. The internal first-party `/auth/login` endpoint must not be described as OAuth ROPC.
- Use RS256, active signing keys identified by `kid`, public-key publication through JWKS, and signing-key rotation.
- Configure issuer and audiences per environment.
- Initial scopes are `openid`, `profile`, `offline_access`, and `ffm.api`.
- Recommended claims are `sub`, `iss`, `aud`, `exp`, `iat`, `jti`, `client_id`, `role`, `permissions`, and approved organization/team scope identifiers. Tokens shall not include unnecessary sensitive personal data.

### 13.10 Rate Limiting and Account Protection

Identity & Access Service owns authentication-specific rate limiting; API Gateway may add general traffic protection.

- Use Redis-backed distributed counters when Identity has multiple instances.
- Evaluate combinations of IP address, normalized account identifier where known, device identifier where applicable, and client ID. IP address alone is insufficient.
- Five consecutive failed account authentication attempts within 15 minutes trigger temporary account protection by default; thresholds remain configuration-driven.
- Configure IP/client request throttling to reduce automated abuse.
- Limit MFA verification attempts per challenge; the approved default is five invalid attempts.
- Return `429 AUTH_RATE_LIMITED` for API request-rate policy violations.
- Return `423 AUTH_ACCOUNT_LOCKED` for temporary account-security lockouts.
- Expired temporary locks must be cleared before evaluating a new failed login attempt.

### 13.11 TLS, Account Statuses, Database, and Audit Controls

TLS 1.2 is the minimum supported protocol and TLS 1.3 is preferred where supported. Insecure protocols and weak cipher suites shall be disabled. Production deployment shall terminate HTTPS using an approved TLS-capable container, reverse proxy, ingress, or load balancer; HTTP-only deployment is not compliant.

Canonical account statuses are `PENDING_ACTIVATION`, `ACTIVE`, `LOCKED`, `SUSPENDED`, `BLOCKED`, and `DEACTIVATED`.

- `LOCKED` returns `423`.
- `PENDING_ACTIVATION`, `SUSPENDED`, `BLOCKED`, and `DEACTIVATED` return `403` with public detail limited to prevent account enumeration.
- Invalid credentials return `401`.

Identity domain entities use UUID primary keys. Explicit foreign keys, database uniqueness constraints, and referential integrity are mandatory. Required unique/indexed fields include normalized user identifiers, role names, permission codes, user-role and role-permission relationships, normalized device identifiers, refresh-token hashes, and audit timestamp/user/result/correlation/security-event fields.

Audit records are append-oriented. Application users must not modify them; audit access is restricted to authorized security/administrative roles. Authentication/security audit retention is configurable with an initial default of one year. Records subject to legal/compliance hold shall not be automatically deleted.

### 13.12 Required Asynchronous Events

RabbitMQ events are asynchronous side effects and must be idempotent. Consumers must use unique event IDs, durable queues where required, bounded-backoff retry, dead-letter queues for permanent failure, and correlation IDs. Audit-critical facts must first be persisted by Identity. Where reliable delivery is required, use the transactional outbox pattern.

Required initial events are `AuthenticationSucceeded`, `AuthenticationFailed`, `AccountLocked`, and `MfaDeliveryRequested`. They must contain the approved identifiers, timestamps, correlation ID, and non-secret context. They must never contain passwords, OTP values, access tokens, refresh tokens, or MFA secrets.

### 13.13 Implementation and Quality Rules

- Follow Clean Architecture/modular boundaries within the independent Identity & Access Service; controllers shall not contain business logic.
- Use dependency injection and asynchronous I/O.
- Use EF Core migrations and PostgreSQL-compatible mappings.
- Use centralized exception/error handling and strongly typed configuration/options.
- Use UTC timestamps.
- Generate OpenAPI/Swagger documentation.
- Use structured logging with correlation IDs, metrics, distributed tracing, and health checks.
- API authorization is always server-side.
- Redis and RabbitMQ are infrastructure dependencies, not synchronous authentication decision makers.

### 13.14 Dependency Boundaries

The following are required external dependencies or integrations:

| Dependency | Responsibility |
|---|---|
| Redis | Distributed temporary state and rate limiting |
| RabbitMQ | Asynchronous event transport only |
| Notification Service | SMS/email delivery, retries, delivery history/monitoring for MFA notifications |
| SMS/email provider | Delivery capability used by Notification Service |
| TLS-capable deployment component | Production HTTPS/TLS termination |
| Other protected FFM APIs | Validate Identity JWTs/JWKS and enforce permission policies |

### 13.15 Additional Acceptance Criteria

| AC | Requirement | Verification |
|---|---|---|
| **AC-11** | Full specification review is complete before delivery | Traceability matrix maps every mandatory requirement to code and test evidence. |
| **AC-12** | Rate limits are distributed and enforced | Multi-instance integration test verifies Redis-backed `429` behavior. |
| **AC-13** | MFA delivery is asynchronous and secure | RabbitMQ/Notification integration test verifies delivery request, bounded retry, and no secret in event/log. |
| **AC-14** | MFA challenges cannot be replayed | Test verifies a verified, expired, or invalidated challenge cannot issue another token. |
| **AC-15** | Device lifecycle is enforced | Tests cover registration, approval, block, revoke, replacement, and missing-device behavior. |
| **AC-16** | OIDC/JWKS are available | Contract test validates discovery, JWKS, `kid`, and token validation by another service. |
| **AC-17** | Refresh reuse is contained | Test verifies reuse revokes only the affected token family/session and records an audit event. |
| **AC-18** | Production transport is secure | Deployment test verifies TLS 1.2+ and rejects insecure protocols. |
| **AC-19** | Authorization is enforced | Integration tests verify `401` for unauthenticated and `403` for insufficient permissions. |
| **AC-20** | Operational requirements are implemented | Tests/inspection verify structured logs, correlation IDs, metrics, tracing, health checks, and secret redaction. |

*The implementation shall be considered complete only when Sections 1–13 are implemented and all applicable acceptance criteria pass. A successful compilation alone is not completion.*

---

### 13.16 Related Specifications
- **Password Reset & Recovery**: See [FFM_Identity_Password_Reset_Module_Spec.md](file:///c:/Users/NambuKamali.AzureAD/Desktop/Nambu%20Kamali/FFM-Backend-22/docs/FFM_Identity_Password_Reset_Module_Spec.md) for full OWASP-compliant forgot-password, token hashing, and password reset flows.

