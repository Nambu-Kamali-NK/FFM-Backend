# FFM Notification Service — Template Management API 100 Test Cases

**API Endpoints:**
* `POST /api/v1/Notifications/templates/create`
* `POST /api/v1/Notifications/templates/update`
* `POST /api/v1/Notifications/templates/render`
* `GET /api/v1/Notifications/templates/by-code`
* `GET /api/v1/Notifications/templates/list`

**Architectural Standards:** Zero Path Parameters, RFC 7807, RS256 JWT RBAC, Safe Regex Interpolation Engine  
**Authorized Roles:**
* **Mutation (Create/Update):** `ADMIN`, `SYSTEM_ADMIN`
* **Rendering & Preview:** `ADMIN`, `SYSTEM_ADMIN`, `MANAGER`, `SUPERVISOR`
* **Query / Audit:** `ADMIN`, `MANAGER`, `SUPERVISOR` (Internal dispatchers)

---

## 1. Test Cases Summary Matrix

| Category | Count | Primary Focus |
|---|:---:|---|
| **1. Positive / Functional** | 15 | PUSH, SMS, EMAIL, IN_APP template lifecycle, update, activate/deactivate, render preview, list & by-code queries |
| **2. Placeholders & Interpolation** | 15 | Safe regex variable substitution, unbalanced braces `{{Name`, case insensitivity, missing key fallbacks, whitespace |
| **3. Channel Constraints & Lengths**| 15 | Push (500 chars), SMS (160 chars), Email (10,000 chars), channel whitelist (`PUSH`, `SMS`, `EMAIL`, `IN_APP`), invalid channels |
| **4. Authentication & RBAC Policy** | 15 | Restricting create/update to ADMIN, allowing MANAGER preview, blocking EMPLOYEE, token expiration, claim validation |
| **5. HTTP & Routing Standards** | 15 | Zero path parameter enforcement, method tampering (PUT/DELETE/GET), Content-Type negotiation, JSON schema validations |
| **6. Concurrency, Edge & Stress**| 25 | Duplicate code/channel conflict (409), inactive template filtering, high-concurrency render, SQLi/XSS, Unicode emojis |
| **TOTAL** | **100** | **Comprehensive Full-Spectrum Test Suite** |

---

## 2. Complete 100 Test Cases Specification Table

| Test ID | Category | Scenario Description | Input Payload / Headers | Status | Expected Behavior |
|:---:|---|---|---|:---:|---|
| **TC-TMPL-001** | Positive | Create standard PUSH template with title & body | `{"templateCode": "TASK_ASSIGNED", "channel": "PUSH", "title": "New Task: {{TaskNumber}}", "bodyTemplate": "Hi {{EmployeeName}}, visit {{SiteName}}."}` | 201 Created | Template persisted, `IsActive=true`, unique ID returned |
| **TC-TMPL-002** | Positive | Create standard SMS template (no title required) | `{"templateCode": "OTP_ALERT", "channel": "SMS", "title": null, "bodyTemplate": "Your FFM verification code is {{Code}}."}` | 201 Created | SMS template saved; title defaults to null or empty |
| **TC-TMPL-003** | Positive | Create standard EMAIL template with HTML body | `{"templateCode": "WORK_ORDER_SUMMARY", "channel": "EMAIL", "title": "Work Order Report", "bodyTemplate": "<h3>Task {{TaskNumber}}</h3><p>Status: {{Status}}</p>"}` | 201 Created | HTML markup stored accurately without truncation |
| **TC-TMPL-004** | Positive | Create IN_APP notification bell template | `{"templateCode": "GEOFENCE_ALERT", "channel": "IN_APP", "title": "Perimeter Alert", "bodyTemplate": "You have arrived at {{SiteName}}."}` | 201 Created | Template registered for mobile notification inbox |
| **TC-TMPL-005** | Positive | Update template body and title | `{"templateId": 1, "title": "⚡ Urgent Task", "bodyTemplate": "Visit {{SiteName}} ASAP."}` | 200 OK | Fields updated; `UpdatedAt` timestamp populated |
| **TC-TMPL-006** | Positive | Deactivate template (`IsActive=false`) | Update with `{"templateId": 1, "isActive": false}` | 200 OK | Template soft-deactivated; preserved for historical audit |
| **TC-TMPL-007** | Positive | Re-activate template (`IsActive=true`) | Update with `{"templateId": 1, "isActive": true}` | 200 OK | Template re-enabled for active event rendering |
| **TC-TMPL-008** | Positive | Render template with complete dictionary | `{"templateCode": "TASK_ASSIGNED", "channel": "PUSH", "placeholders": {"EmployeeName": "Ramesh", "TaskNumber": "TK-101", "SiteName": "Bank ATM"}}` | 200 OK | Returns rendered title and body with all placeholders substituted |
| **TC-TMPL-009** | Positive | Render template with case-insensitive keys | Dict: `{"employeename": "Ramesh", "tasknumber": "TK-101"}` against `{{EmployeeName}}` | 200 OK | Case-insensitive match succeeds; values substituted |
| **TC-TMPL-010** | Positive | List all templates | `GET /api/v1/Notifications/templates/list` | 200 OK | Returns array of all registered templates |
| **TC-TMPL-011** | Positive | List templates filtered by channel | `GET /api/v1/Notifications/templates/list?channel=PUSH` | 200 OK | Returns only templates configured for PUSH channel |
| **TC-TMPL-012** | Positive | List only active templates | `GET /api/v1/Notifications/templates/list?onlyActive=true` | 200 OK | Inactive templates excluded from result set |
| **TC-TMPL-013** | Positive | Query single template by code and channel | `GET /api/v1/Notifications/templates/by-code?code=TASK_ASSIGNED&channel=PUSH` | 200 OK | Returns exact matching template record |
| **TC-TMPL-014** | Positive | Sequential creation of multiple channel templates for same event code | Create `TASK_ASSIGNED` for `PUSH`, then for `SMS`, then for `EMAIL` | 201 Created | All 3 coexist independently under same code |
| **TC-TMPL-015** | Positive | Verify audit trail fields | Create template via ADMIN token (userId: 1) | 201 Created | `CreatedByUserId=1` and `CreatedAt` populated |
| **TC-TMPL-016** | Placeholders | Unbalanced opening brace `{{Name` | `{"bodyTemplate": "Hello {{EmployeeName, welcome."}` | 400 Bad Request | Validation error: `Malformed or unbalanced '{{' and '}}'` |
| **TC-TMPL-017** | Placeholders | Unbalanced closing brace `Name}}` | `{"bodyTemplate": "Hello EmployeeName}}, welcome."}` | 400 Bad Request | Validation error: `Malformed or unbalanced '{{' and '}}'` |
| **TC-TMPL-018** | Placeholders | Multiple unbalanced braces | `{"bodyTemplate": "Task {{T1 and {{T2}} and T3}}"}` | 400 Bad Request | Validation error: Unbalanced braces detected |
| **TC-TMPL-019** | Placeholders | Missing dictionary key (graceful fallback) | Template has `{{SiteName}}`; dictionary does not provide it | 200 OK | Missing key replaced by empty string without crashing |
| **TC-TMPL-020** | Placeholders | Empty dictionary provided | Template has 3 variables; dictionary is empty `{}` | 200 OK | All variables stripped cleanly |
| **TC-TMPL-021** | Placeholders | Multiple occurrences of same placeholder | `{{Name}} arrived. Welcome {{Name}}!` | 200 OK | All occurrences substituted with value |
| **TC-TMPL-022** | Placeholders | Alphanumeric placeholders with underscores | `{{TASK_ID_2026}}`, `{{SITE_CODE_1}}` | 200 OK | Valid variable syntax accepted |
| **TC-TMPL-023** | Placeholders | Placeholder with numbers | `{{Item1}} and {{Item2}}` | 200 OK | Alphanumeric names supported |
| **TC-TMPL-024** | Placeholders | Static template with zero placeholders | `BodyTemplate: "Emergency broadcast: please return to office."` | 200 OK | Render returns raw body unmodified |
| **TC-TMPL-025** | Placeholders | Special characters in placeholder value | Placeholders: `{"SiteName": "St. Mary's & Sons #4"}` | 200 OK | Punctuation and symbols rendered cleanly |
| **TC-TMPL-026** | Placeholders | Unicode text in placeholder value | Placeholders: `{"EmployeeName": "ரமேஷ் குமார்"}` | 200 OK | UTF-8 characters preserved in output |
| **TC-TMPL-027** | Placeholders | Numeric values in dictionary | Placeholders: `{"TaskNumber": "1001", "Minutes": "15"}` | 200 OK | Formatted cleanly |
| **TC-TMPL-028** | Placeholders | Empty string as variable value | Placeholders: `{"SiteName": ""}` | 200 OK | Empty string substituted safely |
| **TC-TMPL-029** | Placeholders | Null dictionary value | Placeholders: `{"SiteName": null}` | 200 OK | Treated as empty string without NullReferenceException |
| **TC-TMPL-030** | Placeholders | High-density placeholders (10+ tags in body) | Body with 10 variables | 200 OK | All 10 substituted cleanly |
| **TC-TMPL-031** | Constraints | Push body exactly 500 characters | Body of 500 chars | 201 Created | Accepted at upper boundary |
| **TC-TMPL-032** | Constraints | Push body exceeding 500 characters (501 chars) | Body of 501 chars | 400 Bad Request | Validation error: `Push notification body cannot exceed 500 characters` |
| **TC-TMPL-033** | Constraints | Push body massively exceeding limit (1,000 chars) | Body of 1,000 chars | 400 Bad Request | Validation error: Length constraint failed |
| **TC-TMPL-034** | Constraints | SMS body exactly 160 characters | Body of 160 chars | 201 Created | Accepted at upper boundary |
| **TC-TMPL-035** | Constraints | SMS body exceeding 160 characters (161 chars) | Body of 161 chars | 400 Bad Request | Validation error: `SMS body cannot exceed 160 characters` |
| **TC-TMPL-036** | Constraints | Email body within 10,000 characters | HTML body of 5,000 chars | 201 Created | Accepted |
| **TC-TMPL-037** | Constraints | Email body exceeding 10,000 characters | HTML body of 10,001 chars | 400 Bad Request | Validation error: `Email HTML body cannot exceed 10,000 characters` |
| **TC-TMPL-038** | Constraints | Unsupported channel: WHATSAPP | `{"channel": "WHATSAPP"}` | 400 Bad Request | Validation error: `Unsupported channel` |
| **TC-TMPL-039** | Constraints | Unsupported channel: TELEGRAM | `{"channel": "TELEGRAM"}` | 400 Bad Request | Validation error: `Unsupported channel` |
| **TC-TMPL-040** | Constraints | Unsupported channel: SLACK | `{"channel": "SLACK"}` | 400 Bad Request | Validation error: `Unsupported channel` |
| **TC-TMPL-041** | Constraints | Lowercase channel normalized to uppercase | `{"channel": "push"}` | 201 Created | Normalized and stored as `PUSH` |
| **TC-TMPL-042** | Constraints | Mixed-case channel normalized to uppercase | `{"channel": "EmAiL"}` | 201 Created | Normalized and stored as `EMAIL` |
| **TC-TMPL-043** | Constraints | Null channel value | `{"channel": null}` | 400 Bad Request | Validation error: `Channel is required` |
| **TC-TMPL-044** | Constraints | Empty string channel | `{"channel": ""}` | 400 Bad Request | Validation error: `Channel is required` |
| **TC-TMPL-045** | Constraints | Whitespace-only channel | `{"channel": "   "}` | 400 Bad Request | Validation error: `Channel is required` |
| **TC-TMPL-046** | Auth & RBAC | ADMIN role creates template | Valid JWT with ADMIN role | 201 Created | Permitted |
| **TC-TMPL-047** | Auth & RBAC | SYSTEM_ADMIN role creates template | Valid JWT with SYSTEM_ADMIN role | 201 Created | Permitted |
| **TC-TMPL-048** | Auth & RBAC | MANAGER role attempts template creation | Valid JWT with MANAGER role | 403 Forbidden | Access denied: Administrative privileges required |
| **TC-TMPL-049** | Auth & RBAC | SUPERVISOR role attempts template creation | Valid JWT with SUPERVISOR role | 403 Forbidden | Access denied: Administrative privileges required |
| **TC-TMPL-050** | Auth & RBAC | EMPLOYEE role attempts template creation | Valid JWT with EMPLOYEE role | 403 Forbidden | Access denied: Administrative privileges required |
| **TC-TMPL-051** | Auth & RBAC | MANAGER role allowed to render/preview | Valid JWT with MANAGER role | 200 OK | Permitted to preview templates for dispatching |
| **TC-TMPL-052** | Auth & RBAC | SUPERVISOR role allowed to render/preview | Valid JWT with SUPERVISOR role | 200 OK | Permitted to preview templates |
| **TC-TMPL-053** | Auth & RBAC | EMPLOYEE role blocked from render endpoint | Valid JWT with EMPLOYEE role | 403 Forbidden | Access denied |
| **TC-TMPL-054** | Auth & RBAC | Missing Authorization header | Request without Bearer token | 401 Unauthorized | Missing authentication credentials |
| **TC-TMPL-055** | Auth & RBAC | Expired JWT token | Bearer token past expiration | 401 Unauthorized | Token expired |
| **TC-TMPL-056** | Auth & RBAC | Forged JWT signature | Token signed with unknown key | 401 Unauthorized | Signature validation failed |
| **TC-TMPL-057** | Auth & RBAC | Wrong JWT Issuer | Token with invalid `iss` claim | 401 Unauthorized | Issuer validation failed |
| **TC-TMPL-058** | Auth & RBAC | Wrong JWT Audience | Token with invalid `aud` claim | 401 Unauthorized | Audience validation failed |
| **TC-TMPL-059** | Auth & RBAC | CreatedByUserId strictly derived from JWT | Body passes `{"createdByUserId": 99}` | 201 Created | Ignored; backend extracts user ID from authenticated JWT |
| **TC-TMPL-060** | Auth & RBAC | Empty Bearer header | `Authorization: "Bearer "` | 401 Unauthorized | Malformed authorization token |
| **TC-TMPL-061** | HTTP & Routing | Path parameter tampering on create | `POST /api/v1/Notifications/templates/create/1` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-TMPL-062** | HTTP & Routing | Path parameter tampering on update | `POST /api/v1/Notifications/templates/update/1` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-TMPL-063** | HTTP & Routing | Path parameter tampering on render | `POST /api/v1/Notifications/templates/render/TASK_1` | 404 Not Found | Zero Path Parameter standard strictly enforced |
| **TC-TMPL-064** | HTTP & Routing | GET request on create endpoint | `GET /api/v1/Notifications/templates/create` | 405 Method Not Allowed | Method tampering blocked |
| **TC-TMPL-065** | HTTP & Routing | PUT request on create endpoint | `PUT /api/v1/Notifications/templates/create` | 405 Method Not Allowed | Method tampering blocked |
| **TC-TMPL-066** | HTTP & Routing | DELETE request on create endpoint | `DELETE /api/v1/Notifications/templates/create` | 405 Method Not Allowed | Method tampering blocked |
| **TC-TMPL-067** | HTTP & Routing | POST request on list endpoint | `POST /api/v1/Notifications/templates/list` | 405 Method Not Allowed | Method tampering blocked |
| **TC-TMPL-068** | HTTP & Routing | Content-Type text/plain | `Content-Type: text/plain` with JSON body | 415 Unsupported Media Type | Requires application/json |
| **TC-TMPL-069** | HTTP & Routing | Content-Type application/xml | `Content-Type: application/xml` | 415 Unsupported Media Type | Requires application/json |
| **TC-TMPL-070** | HTTP & Routing | Empty JSON object body `{}` | `POST /create` with `{}` | 400 Bad Request | Required fields missing |
| **TC-TMPL-071** | HTTP & Routing | Malformed JSON syntax | Missing closing bracket in payload | 400 Bad Request | JSON parsing error |
| **TC-TMPL-072** | HTTP & Routing | Null body payload | `POST /create` with `null` | 400 Bad Request | `Request body cannot be empty` |
| **TC-TMPL-073** | HTTP & Routing | Extra unknown JSON properties | `{"extraField": "value", ...}` | 201 Created | Safely ignored; standard DTO deserialization |
| **TC-TMPL-074** | HTTP & Routing | Missing TemplateCode field | Payload omitting `"templateCode"` | 400 Bad Request | Validation error: `TemplateCode is required` |
| **TC-TMPL-075** | HTTP & Routing | Missing BodyTemplate field | Payload omitting `"bodyTemplate"` | 400 Bad Request | Validation error: `BodyTemplate is required` |
| **TC-TMPL-076** | Edge & Stress | Duplicate `(TemplateCode, Channel)` conflict | Create `TASK_ASSIGNED` on `PUSH` when already exists | 409 Conflict | Error code: `DUPLICATE_TEMPLATE` |
| **TC-TMPL-077** | Edge & Stress | Update non-existent TemplateId | `{"templateId": 999999, ...}` | 404 Not Found | Error: `Notification template with ID 999999 was not found` |
| **TC-TMPL-078** | Edge & Stress | Update with TemplateId = 0 | `{"templateId": 0, ...}` | 400 Bad Request | Validation error: `Valid TemplateId is required` |
| **TC-TMPL-079** | Edge & Stress | Update with negative TemplateId | `{"templateId": -5, ...}` | 400 Bad Request | Validation error: `Valid TemplateId is required` |
| **TC-TMPL-080** | Edge & Stress | Render non-existent template code | `{"templateCode": "UNKNOWN_CODE", "channel": "PUSH"}` | 404 Not Found | Error: `TEMPLATE_NOT_FOUND` |
| **TC-TMPL-081** | Edge & Stress | Render inactive template (`IsActive=false`) | Render a template previously deactivated | 404 Not Found | Inactive templates cannot be rendered |
| **TC-TMPL-082** | Edge & Stress | High-concurrency template rendering (500 simultaneous calls) | 500 parallel workers rendering `TASK_ASSIGNED` | 200 OK | Sub-2ms interpolation execution; 100% pass |
| **TC-TMPL-083** | Edge & Stress | SQL Injection attempt in TemplateCode | `{"templateCode": "TASK'; DROP TABLE \"NotificationTemplates\";--"}` | 400 Bad Request | Rejected by alphanumeric regex validator |
| **TC-TMPL-084** | Edge & Stress | SQL Injection attempt in BodyTemplate | Body: `' OR '1'='1` with EF Core | 201 Created | Parameterized query stores string safely |
| **TC-TMPL-085** | Edge & Stress | XSS script injection in BodyTemplate | `{"bodyTemplate": "<script>alert('XSS')</script>"}` | 201 Created | Stored safely as raw string |
| **TC-TMPL-086** | Edge & Stress | XSS script injection inside runtime variable | Dict: `{"EmployeeName": "<img src=x onerror=alert(1)>"}` | 200 OK | Substituted safely into text |
| **TC-TMPL-087** | Edge & Stress | Unicode emoji in Title and Body | `Title: "🚨 Urgent Task #{{TaskNumber}} 🚀"` | 201 Created | UTF-8 emojis preserved |
| **TC-TMPL-088** | Edge & Stress | Multilingual template body (Tamil) | `BodyTemplate: "வணக்கம் {{EmployeeName}}, புதிய பணி ஒதுக்கப்பட்டுள்ளது."` | 201 Created | Full UTF-8 language support verified |
| **TC-TMPL-089** | Edge & Stress | TemplateCode minimum length boundary (exactly 3 chars) | `{"templateCode": "OTP"}` | 201 Created | Accepted at lower boundary |
| **TC-TMPL-090** | Edge & Stress | TemplateCode < 3 chars (e.g. 2 chars) | `{"templateCode": "TK"}` | 400 Bad Request | Validation error: Minimum 3 characters |
| **TC-TMPL-091** | Edge & Stress | TemplateCode maximum length boundary (exactly 50 chars) | TemplateCode of 50 chars | 201 Created | Accepted at upper boundary |
| **TC-TMPL-092** | Edge & Stress | TemplateCode exceeding 50 chars (51 chars) | TemplateCode of 51 chars | 400 Bad Request | Validation error: Max 50 characters |
| **TC-TMPL-093** | Edge & Stress | TemplateCode with dashes rejected | `{"templateCode": "TASK-ASSIGNED"}` | 400 Bad Request | Rejection: Alphanumeric and underscores only |
| **TC-TMPL-094** | Edge & Stress | Title maximum length (150 chars) | Title of 150 chars | 201 Created | Accepted at upper boundary |
| **TC-TMPL-095** | Edge & Stress | Title exceeding 150 chars (151 chars) | Title of 151 chars | 400 Bad Request | Validation error: Title cannot exceed 150 chars |
| **TC-TMPL-096** | Edge & Stress | Description maximum length (300 chars) | Description of 300 chars | 201 Created | Accepted |
| **TC-TMPL-097** | Edge & Stress | Description exceeding 300 chars | Description of 301 chars | 400 Bad Request | Validation error: Description exceeds limit |
| **TC-TMPL-098** | Edge & Stress | Query non-existent template by code | `GET /by-code?code=MISSING&channel=PUSH` | 404 Not Found | Error: `TEMPLATE_NOT_FOUND` |
| **TC-TMPL-099** | Edge & Stress | Query by-code with missing query parameters | `GET /by-code?code=TASK_ASSIGNED` (missing channel) | 400 Bad Request | Error: Both code and channel required |
| **TC-TMPL-100** | Edge & Stress | Zero Path Parameter contract integrity audit across all 5 endpoints | Verify no endpoints define `{id}` in routes | 200 OK | Full zero path parameter compliance verified |

