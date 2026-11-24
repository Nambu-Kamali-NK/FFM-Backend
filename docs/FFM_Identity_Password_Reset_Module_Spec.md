# Formal Technical Specification Document (Backend SDD)

## Module: Identity & Access Control – Password Reset & Recovery Module
### System: Field Force Management (FFM) Platform

| Document Attribute | Specification Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-AUTH-PASSWORD-RESET-V1.0` |
| **Standard Compliance** | ISO/IEC/IEEE 29148, OWASP ASVS v4.0 (Authentication), NIST SP 800-63B |
| **Module Classification** | Tier‑1 Core Security Infrastructure |
| **Status** | Approved Formal Specification |
| **Target Clients** | FFM Web Portal & FFM Mobile Client |
| **Perspective** | **Backend / Server‑Side Only** |
| **Parent Document** | `FFM-Identity&Access Service-Login module.md` |

---

## 1️⃣ Business Requirements & Security Policies

### 1.1 Anti-User Enumeration Defense (OWASP Recommendation)
- When a user submits an email for password recovery via `POST /api/auth/forgot-password`, the API response **MUST ALWAYS** be uniform:
  - HTTP 200 OK: `{"success": true, "message": "If an account exists with the provided email, a password reset link has been sent."}`
  - The API must take approximately the same time whether the email exists or not (preventing timing attacks).

### 1.2 Cryptographic Token Security
- Reset tokens must be generated using a cryptographically secure pseudo-random number generator (`RandomNumberGenerator`).
- Minimum entropy: 256 bits (32 bytes / 64 hexadecimal characters).
- **Zero-Storage of Raw Tokens**: Raw tokens are **never** stored in plaintext in the database. Only the **SHA-256 hash** of the token is persisted.
- If the database is compromised, active reset tokens cannot be reconstructed or reused by attackers.

### 1.3 Token Expiration & Single-Use
- **Time-to-Live (TTL)**: Tokens expire exactly **15 minutes** after issuance.
- **Single-Use**: Once a token is used to change a password, it is marked `is_used = true` and cannot be replayed.
- Requesting a new token invalidates any prior unused reset tokens issued for the same user account.

### 1.4 Post-Reset Session Invalidation
- Upon successful password reset, **all active refresh tokens** belonging to the user are immediately revoked across all devices (`REVOKED` state / purged), forcing re-authentication everywhere.
- Any lockout penalties (`FailedAttempts`, `LockedUntil`) are cleared.

---

## 2️⃣ Data Model & Schema Definition

### Table: `password_reset_tokens` (Schema: `public` / Identity DB)

| Column Name | Data Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `id` | `INTEGER` (PK, Auto-inc) | No | Unique record identifier |
| `user_id` | `INTEGER` (FK -> users.id) | No | Identifier of the account requesting reset |
| `token_hash` | `VARCHAR(64)` | No | SHA-256 hexadecimal hash of the secret token |
| `expires_at` | `TIMESTAMP WITH TIME ZONE` | No | Absolute expiration timestamp (UTC) |
| `is_used` | `BOOLEAN` | No | Flag indicating if token was already consumed |
| `used_at` | `TIMESTAMP WITH TIME ZONE` | Yes | Timestamp when token was consumed |
| `created_at` | `TIMESTAMP WITH TIME ZONE` | No | Creation timestamp (UTC) |
| `ip_address` | `VARCHAR(45)` | Yes | Originating IPv4/IPv6 client address |

---

## 3️⃣ API Endpoint Contracts

### 3.1 Forgot Password (`POST /api/auth/forgot-password`)
Initiates the password reset workflow by generating a secret token and dispatching the notification.

#### Request Schema:
```json
{
  "email": "employee@example.com"
}
```

#### Response (HTTP 200 OK):
```json
{
  "success": true,
  "message": "If an account exists with the provided email, a password reset link has been sent."
}
```

---

### 3.2 Reset Password (`POST /api/auth/reset-password`)
Consumes the token, validates password strength, updates the BCrypt hash, and revokes sessions.

#### Request Schema:
```json
{
  "token": "4f8a3c9b8e217d65f4128a9b7e324c568d71e9fa0b123456789abcdef0123456",
  "newPassword": "NewStrongPassword123!"
}
```

#### Validation Rules for `newPassword`:
- Minimum length: 8 characters.
- Must contain at least one uppercase letter (`A-Z`).
- Must contain at least one lowercase letter (`a-z`).
- Must contain at least one digit (`0-9`).
- Must contain at least one special character (`!@#$%^&*()_+-=[]{}|;:,.<>?`).

#### Success Response (HTTP 200 OK):
```json
{
  "success": true,
  "message": "Password has been reset successfully. Please log in with your new credentials."
}
```

#### Error Response (HTTP 400 Bad Request):
```json
{
  "success": false,
  "errorCode": "INVALID_OR_EXPIRED_TOKEN",
  "message": "The password reset token is invalid, expired, or has already been used."
}
```
