# FFM Field Operations — Load Testing & Performance Specification

## 1. Document Metadata & Control

| Attribute | Detail |
| :--- | :--- |
| **Document Reference** | `FFM-SPEC-FLD-PERF-LOAD-V1.0` |
| **System** | Field Force Management (FFM) Platform |
| **Microservice** | Field Operations Service (`FFM.FieldOperations`) |
| **Runtime & Framework** | .NET 8 Web API / Kestrel / Entity Framework Core 8 / PostgreSQL 16 |
| **Port** | `5030` (Field Operations), `5000` (Identity & Access Provider) |
| **Primary Test Domains** | Server-Driven UI (SDUI), Evidence Ingestion, Task Dispatching, Geofencing |
| **Standard Compliance** | ISO/IEC 25010 (System & Software Quality), RFC 7230 (HTTP/1.1), WGS-84 Geodesy |
| **Classification** | Tier-1 Mission-Critical Enterprise Production Service |
| **Status** | Approved Formal Engineering Specification |
| **Author** | Antigravity AI Engineering & Performance Architecture Team |

---

## 2. Executive Purpose & Performance Objectives

The **FFM Field Operations Service** powers high-concurrency mobile field workflows for thousands of technicians operating in the field. These workflows include loading dynamic Server-Driven UI (SDUI) screens, reporting GPS coordinates, recording on-site multi-file evidence artifacts, and executing state-machine task transitions.

Following the recent architectural refactoring and API optimizations (including in-memory schema caching, batch database persistence, $O(1)$ indexed sequence numbering, and unjoined pagination counting), this specification establishes the **standardized load testing methodology, workload models, performance SLAs, test execution scenarios, and validation criteria**.

### Core Objectives:
1. **Validate Optimization Gains:** Quantify the before vs. after performance improvement under realistic production concurrency (10 to 500+ Virtual Users).
2. **Stress Verification:** Identify microservice saturation points, thread pool starvation limits, and database connection pool boundaries.
3. **Verify Zero-Friction Reliability:** Ensure atomic batch evidence submissions and geofence validation remain sub-50ms even during peak morning dispatch bursts.
4. **Endurance & Leak Verification:** Ensure long-running soak tests show zero unmanaged memory leaks, stable GC Gen 0/1/2 collection rates, and zero PostgreSQL connection leaks.

---

## 3. System Architecture & Test Boundary

```
 ┌────────────────────────────────────────────────────────────────────────┐
 │                      LOAD GENERATOR (k6 / Python)                      │
 │   - Virtual Users (VUs): 10 to 500 concurrent workers                  │
 │   - Ramp-up, Steady-State, Spike, and Soak Profiles                    │
 └──────────────────────────────────┬─────────────────────────────────────┘
                                    │ HTTP/1.1 + Bearer JWT (RS256)
                                    ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │            FFM.FieldOperations.Api (.NET 8 Kestrel - Port 5030)        │
 │  ┌──────────────────────────────────────────────────────────────────┐  │
 │  │ Pipeline: Security Headers -> Rate Limiter (5000/min) -> Auth   │  │
 │  └──────────────────────────────────┬───────────────────────────────┘  │
 │                                     ▼                                  │
 │  ┌──────────────────────────────────────────────────────────────────┐  │
 │  │ Handlers & SDUI Engine:                                          │  │
 │  │  • GetTaskScreenLayoutQueryHandler (In-Memory Template Cache)    │  │
 │  │  • GetTaskFormSchemaQueryHandler (In-Memory Schema Cache)        │  │
 │  │  • AddVisitEvidenceCommandHandler (Batch Persisting Engine)      │  │
 │  │  • Haversine Distance Geofence Validator (Pure CPU Math)         │  │
 │  └──────────────────────────────────┬───────────────────────────────┘  │
 └─────────────────────────────────────┼──────────────────────────────────┘
                                       │ EF Core 8 (Npgsql - Connection Pool: 100)
                                       ▼
 ┌────────────────────────────────────────────────────────────────────────┐
 │               PostgreSQL 16 Database (Port 5434 / Docker)              │
 │   - Tables: ClientSites, Tasks, Visits, VisitEvidences, EvidenceTypes  │
 │   - Composite Indexes: IX_Tasks_TaskNumber, IX_Visits_TaskId           │
 └────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Key Performance Indicators (KPIs) & SLA Thresholds

Every test execution must satisfy the following Service Level Agreements (SLAs). Any breach of **P95 latency**, **Error Rate**, or **CPU Saturation** constitutes a test failure.

### 4.1 Endpoint SLA Threshold Matrix

| HTTP | Route | Operational Classification | Target Throughput (RPS) | P50 SLA | P95 SLA | P99 SLA | Max Error Rate |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **GET** | `/api/v1/Tasks/screen-layout` | Full-Screen SDUI (Cached) | **≥ 1,500 RPS** | **< 3 ms** | **< 8 ms** | **< 20 ms** | `< 0.01%` |
| **GET** | `/api/v1/Tasks/form-schema` | Task Form Schema (Cached) | **≥ 2,000 RPS** | **< 2 ms** | **< 5 ms** | **< 15 ms** | `< 0.01%` |
| **GET** | `/api/v1/Tasks/actions` | State Machine Buttons | **≥ 800 RPS** | **< 10 ms** | **< 25 ms** | **< 50 ms** | `< 0.05%` |
| **POST** | `/api/v1/Visits/evidence` | Batch Evidence (3-5 Files) | **≥ 350 RPS** | **< 20 ms** | **< 45 ms** | **< 90 ms** | `< 0.10%` |
| **GET** | `/api/v1/Tasks` | Filtered Pagination (Page 1) | **≥ 500 RPS** | **< 15 ms** | **< 35 ms** | **< 75 ms** | `< 0.05%` |
| **POST** | `/api/v1/Tasks` | Task Creation & Numbering | **≥ 300 RPS** | **< 18 ms** | **< 40 ms** | **< 80 ms** | `< 0.05%` |
| **POST** | `/api/v1/Tasks/status` | Status -> EN_ROUTE/COMPLETED | **≥ 400 RPS** | **< 15 ms** | **< 35 ms** | **< 70 ms** | `< 0.05%` |
| **GET** | `/api/v1/ClientSites/{id}` | Geofenced Facility Lookup | **≥ 1,000 RPS** | **< 5 ms** | **< 12 ms** | **< 30 ms** | `< 0.01%` |

### 4.2 System Resource Utilization Limits
* **Application CPU:** $\le 75\%$ average across all cores during steady state.
* **Application Memory:** $\le 512\text{ MB}$ Working Set (zero runaway heap growth).
* **Database CPU:** $\le 70\%$ peak utilization on PostgreSQL.
* **Connection Pool Saturation:** Active pool connections must not exceed $80\%$ of `MaxPoolSize` (80 out of 100).
* **HTTP 429 / 5xx Rate:** Strictly $0\%$ unexpected HTTP 5xx server faults. (429 is expected only when intentionally testing the sliding-window limiter exceeding 5,000 req/min).

---

## 5. Load Testing Scenarios & Profiles

### Scenario 1: Baseline Smoke & Concurrency Test
* **Objective:** Establish baseline response times and verify zero warmup anomalies.
* **Workload:** 10 concurrent Virtual Users (VUs) for 60 seconds.
* **Volume:** ~1,000 requests total.
* **Success Criteria:** 100% success rate; SDUI P95 $< 5\text{ ms}$; Evidence upload P95 $< 30\text{ ms}$.

### Scenario 2: Peak Morning Shift Dispatch (Read-Heavy Burst)
* **Objective:** Simulate 500 field engineers logging into mobile apps simultaneously at 08:30 AM to download task lists, form schemas, and full-screen layouts.
* **Traffic Mix:**
  * $50\%$ `GET /api/v1/Tasks/screen-layout?taskId={id}`
  * $25\%$ `GET /api/v1/Tasks?pageNumber=1&pageSize=20`
  * $15\%$ `GET /api/v1/Tasks/form-schema?taskType={type}`
  * $10\%$ `GET /api/v1/Tasks/actions?taskId={id}`
* **Ramp Profile:**
  * Ramp up from 1 to 200 VUs over 2 minutes.
  * Hold steady at 200 VUs for 8 minutes.
  * Ramp down over 1 minute.
* **Success Criteria:** Zero connection pool timeouts; P95 response time $< 35\text{ ms}$; throughput $\ge 1,200\text{ RPS}$.

### Scenario 3: High-Volume Concurrent Evidence Submission (Write-Heavy)
* **Objective:** Test database transaction throughput, batch insert efficiency, and geofence distance calculation under heavy writes.
* **Traffic Mix:**
  * $70\%$ `POST /api/v1/Visits/evidence` (payload with 3 structured files, generic readings, GPS inside geofence).
  * $20\%$ `POST /api/v1/Tasks/status` (status: `EN_ROUTE` or `COMPLETED`).
  * $10\%$ `GET /api/v1/Visits/evidence?visitId={id}`.
* **Ramp Profile:**
  * Ramp up to 100 concurrent VUs over 60 seconds.
  * Hold steady for 5 minutes.
* **Success Criteria:** Batch insert persists atomically; average evidence submission latency $< 25\text{ ms}$; zero deadlocks in PostgreSQL.

### Scenario 4: Stress & Breaking Point Test
* **Objective:** Incrementally ramp virtual users to determine the saturation point of Kestrel and PostgreSQL.
* **Ramp Profile:**
  * Step 1: 100 VUs (2 min)
  * Step 2: 250 VUs (2 min)
  * Step 3: 500 VUs (2 min)
  * Step 4: 1,000 VUs (2 min)
* **Success Criteria:** Identify exact RPS ceiling; system must degrade gracefully (increase queuing/latency) without process crash or connection leakage.

### Scenario 5: Soak / Endurance Test
* **Objective:** Verify memory leak absence, EF Core context garbage collection, and persistent DB connection health.
* **Workload:** 50 steady VUs for 2 continuous hours.
* **Success Criteria:** Memory growth curve must plateau; GC Gen 2 collections must remain stable; P99 latency must not drift upward by more than $15\%$ from hour 1 to hour 2.

---

## 6. Test Data Requirements & Preparation

To guarantee realistic query execution plans and realistic B-tree index traversals, tests must not run against an empty database.

### 6.1 Database Seeding Specification
1. **ClientSites Table:** Minimum **500 geofenced facilities** across major cities with varying radii (50m to 500m).
2. **Tasks Table:** Minimum **25,000 task records** distributed across statuses (`CREATED`, `ASSIGNED`, `EN_ROUTE`, `CHECKED_IN`, `COMPLETED`, `CANCELLED`) and priorities (`LOW`, `MEDIUM`, `HIGH`, `URGENT`).
3. **Visits Table:** Minimum **10,000 historical visits** linked to tasks.
4. **VisitEvidences Table:** Minimum **30,000 evidence items** across categories (`PHOTO`, `METER_READING`, `SIGNATURE`, `DOCUMENT`, `INVOICE`).
5. **EvidenceTypes Table:** Seeded with 5 active master records (`PHOTO`, `METER_READING`, `SIGNATURE`, `DOCUMENT`, `INVOICE`).

### 6.2 Authentication & Token Setup
* Load testing harnesses must pre-generate or cache JWT tokens for:
  * **Technician VUs:** `tech1` to `tech100` (`Role: EMPLOYEE`).
  * **Manager VUs:** `admin@ffm.com` (`Role: ADMIN`).
* Avoid generating tokens inside the measured HTTP request loop to prevent measuring auth server latency instead of Field Operations API latency.

---

## 7. Automated Test Script Implementation (k6 Script)

Below is the standard enterprise **k6** load script (`scratch/k6_load_test.js`) implementing Scenario 2 (Peak Morning Dispatch) and Scenario 3 (Batch Evidence Submission):

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Warm-up ramp
    { duration: '3m', target: 150 },  // Steady peak
    { duration: '1m', target: 250 },  // Stress peak
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    'http_req_duration{endpoint:screen_layout}': ['p(95)<8'],
    'http_req_duration{endpoint:form_schema}': ['p(95)<5'],
    'http_req_duration{endpoint:submit_evidence}': ['p(95)<45'],
    'http_req_failed': ['rate<0.001'], // < 0.1% failures
  },
};

const BASE_URL = __ENV.TARGET_URL || 'http://localhost:5030';
const TOKEN = __ENV.BEARER_TOKEN || 'eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...';

const headers = {
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${TOKEN}`,
};

export default function () {
  // 1. Fetch Full-Screen SDUI Layout (60% weight)
  let sduiRes = http.get(`${BASE_URL}/api/v1/Tasks/screen-layout?taskId=307`, {
    headers: headers,
    tags: { endpoint: 'screen_layout' },
  });
  check(sduiRes, {
    'SDUI status is 200': (r) => r.status === 200,
    'SDUI has widgets': (r) => JSON.parse(r.body).data.widgets.length > 0,
  });

  // 2. Fetch Form Schema (20% weight)
  let schemaRes = http.get(`${BASE_URL}/api/v1/Tasks/form-schema?taskType=AC_MAINTENANCE`, {
    headers: headers,
    tags: { endpoint: 'form_schema' },
  });
  check(schemaRes, {
    'Schema status is 200': (r) => r.status === 200,
  });

  // 3. Submit Batch Evidence (20% weight)
  const evidencePayload = JSON.stringify({
    taskId: 307,
    capturedLatitude: 12.9715987,
    capturedLongitude: 77.5945627,
    remarks: 'Load test automated verification of AC compressor.',
    files: [
      {
        fileName: 'pressure-gauge.jpg',
        folderPath: 'tasks/307/evidence',
        fileUrl: 'https://ffm-field-evidence.s3.amazonaws.com/gauge.jpg',
        evidenceTypeId: 2
      },
      {
        fileName: 'completed-work.jpg',
        folderPath: 'tasks/307/evidence',
        fileUrl: 'https://ffm-field-evidence.s3.amazonaws.com/unit.jpg',
        evidenceTypeId: 1
      }
    ],
    readings: {
      compressorPressurePsi: 70.2,
      coolingTemperatureC: 17.8
    }
  });

  let evRes = http.post(`${BASE_URL}/api/v1/Visits/evidence`, evidencePayload, {
    headers: headers,
    tags: { endpoint: 'submit_evidence' },
  });
  check(evRes, {
    'Evidence status is 201': (r) => r.status === 201,
  });

  sleep(0.5); // 500ms pacing between actions
}
```

---

## 8. Failure Triage & Troubleshooting Playbook

| Symptom | Root Cause Category | Diagnostic Investigation | Immediate Remediation |
| :--- | :--- | :--- | :--- |
| **P95 Latency Spikes on SDUI** | In-Memory Cache Miss / Eviction | Verify template path resolution or check if `task_screen_templates.json` is missing from bin. | Ensure static cache boolean flag remains true and thread-safe lock does not re-read disk. |
| **PostgreSQL Pool Timeout (Timeout expired awaiting connection)** | Connection Pool Exhaustion | Check `pg_stat_activity` for active long-running connections or unclosed `FieldOperationsDbContext`. | 1) Ensure all queries use `using` / Scoped DI.<br>2) Increase `MaxPoolSize=200` in connection string.<br>3) Verify `.AsNoTracking()` is active on read paths. |
| **HTTP 429 Too Many Requests** | Rate Limiter Tripping | ASP.NET Core `FieldOperationsPolicy` permits 5,000 requests/minute. | For load testing environments, tune `opt.PermitLimit = 50000` or disable rate limiting on the test subnet. |
| **High Memory Growth during Soak Test** | Memory Leak / Untracked Snapshots | Profiling with `dotnet-dump` or `dotnet-counters` for Gen 2 heap growth. | Confirm that large DTO collections or DbContext instances are not captured in static event handlers or singleton single-tons. |
| **Geofence Distance False Rejections** | Precision Mismatch | `capturedLatitude` passed with precision truncation. | Ensure coordinates are parsed as `decimal` and converted to `double` only inside the Haversine calculation math block. |

---

## 9. Sign-Off & Verification Checklist

- [ ] All 15 endpoints verified against P95 latency thresholds.
- [ ] Database connection pool returns to 0 idle leaked connections post-test.
- [ ] 0 HTTP 5xx errors recorded across 500,000 cumulative test requests.
- [ ] CPU and memory plateau predictably with clean garbage collection cycles.
- [ ] Before vs. After optimization benchmark report attached to release artifact.

