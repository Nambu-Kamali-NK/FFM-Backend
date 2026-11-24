# Splunk Cloud Complete Mastery Guide
**Comprehensive Course Handbook for Splunk Cloud Administration & Architecture**

---

## Table of Contents
1. [Module 1: Splunk Cloud Architecture & Experience Models](#module-1-splunk-cloud-architecture--experience-models)
2. [Module 2: User Access & Role-Based Access Control (RBAC)](#module-2-user-access--role-based-access-control-rbac)
3. [Module 3: Index Management, Storage & Data Retention](#module-3-index-management-storage--data-retention)
4. [Module 4: Getting Data In (GDI) — Universal Forwarders & Credentials](#module-4-getting-data-in-gdi--universal-forwarders--credentials)
5. [Module 5: HTTP Event Collector (HEC) & Heavy Forwarders](#module-5-http-event-collector-hec--heavy-forwarders)
6. [Module 6: Managing Apps, Add-ons & App Inspect](#module-6-managing-apps-add-ons--app-inspect)
7. [Module 7: Cloud Monitoring Console (CMC) & Performance Health](#module-7-cloud-monitoring-console-cmc--performance-health)
8. [Module 8: Hands-On Lab Exercises & Practice Scenarios](#module-8-hands-on-lab-exercises--practice-scenarios)
9. [Module 9: Splunk Cloud Certification Exam Practice Q&A](#module-9-splunk-cloud-certification-exam-practice-qa)

---

## Module 1: Splunk Cloud Architecture & Experience Models

### 1. The Shared Responsibility Model
In Splunk Cloud, infrastructure and operations are divided between Splunk and the customer:

| Component / Function | Managed by Splunk | Managed by Customer |
|---|:---:|:---:|
| Cloud infrastructure (AWS / GCP) | ✅ | ❌ |
| Operating System & Security Patches | ✅ | ❌ |
| Splunk Core Software Upgrades | ✅ | ❌ |
| Indexer Cluster Scaling & High Availability | ✅ | ❌ |
| Disaster Recovery & Daily Backups | ✅ | ❌ |
| Forwarder Installation & Maintenance | ❌ | ✅ |
| Data Inputs (`inputs.conf`) & Source Setup | ❌ | ✅ |
| App Selection, Vetting & Configuration | ❌ | ✅ |
| Index Creation & Retention Limits | ❌ | ✅ |
| User Provisioning, Roles & Permissions | ❌ | ✅ |

### 2. Victoria Experience vs. Classic Experience
Splunk Cloud runs in two architectures depending on your stack version:

```
[ Victoria Experience ]                [ Classic Experience ]
├── Self-service private app install   ├── Splunk Admin assisted app installs
├── Direct UI app package upload       ├── Cluster Master CLI deployment
├── Modern Search Head Clustering      ├── Legacy Deployment Server
└── IDXC dynamic scaling               └── Fixed topology provisioning
```

* **Victoria Experience:**
  * Self-service app installs enabled directly in the UI.
  * Private apps are vetted automatically via **Splunk App Inspect** API.
  * Search Head Clustering (SHC) is cloud-native.
* **Classic Experience:**
  * Requires support tickets or Splunk Cloud Admin assistance for complex private app deployments.
  * Heavy Forwarders and Deployment Servers handle on-premises synchronization.

---

## Module 2: User Access & Role-Based Access Control (RBAC)

### 1. Default Splunk Roles
* **`admin` (in Cloud: `sc_admin`)**: Highest customer administrative role in Splunk Cloud. Can manage indexes, apps, users, and inputs.
* **`power`**: Can create and share knowledge objects (dashboards, alerts, reports) across teams.
* **`user`**: Can search allowed indexes, save personal searches, and view shared dashboards.
* **`can_delete`**: Special role required to run the `| delete` command (empties indexed data from search view without freeing disk).

### 2. Custom Role Architecture
When creating custom roles for teams (e.g., `Security_Analyst` or `DevOps_Auditor`):
1. **Inheritance**: Inherit capabilities from base roles (`user` or `power`).
2. **Search Constraints**:
   * **Allowed Indexes**: Specific indexes the role can search (`index=app_prod`, `index=security_logs`).
   * **Default Search Indexes**: Searched when no `index=` is specified.
   * **Search Time Window**: Restrict historical searches (e.g., max 30 days).
   * **Concurrent Search Limits**: Cap how many searches a user can execute concurrently.

### 3. Enterprise Identity Integrations (SAML & SSO)
* **SAML 2.0 Providers**: Okta, Microsoft Azure AD (Entra ID), PingFederate.
* **Mapping**: Map corporate Active Directory security groups directly to Splunk Roles.
* **SCIM (System for Cross-domain Identity Management)**: Automates user onboarding and deprovisioning when employees join or leave.

---

## Module 3: Index Management, Storage & Data Retention

### 1. Index Types
* **Events Index**: Standard index storing timestamped raw log text (e.g. syslog, JSON, Windows event logs).
* **Metrics Index**: Optimized numeric time-series storage (CPU, memory, temperature, network throughput). 20x to 50x faster and uses up to 70% less disk space.

### 2. Data Life Cycle in Splunk Cloud
```
[ Ingestion / Memory ]
         │
         ▼
[ Hot Buckets ] ──> Actively written to (stored on high-speed NVMe storage)
         │
         ▼
[ Warm Buckets ] ──> Read-only, indexed, searched regularly
         │
         ▼
[ Cold Buckets ] ──> Older historical data
         │
         ▼
[ Frozen Tier ] ──> Data reaches max size or retention age
         │
         ├── Option A: Permanently Deleted (Default)
         └── Option B: Dynamic Data Self-Storage (DDSS) / Dynamic Data Active Archive (DDAA)
```

### 3. Retention Policies
* **Max Size (`maxTotalDataSizeMB`)**: Total disk quota allocated to the index.
* **Max Age (`frozenTimePeriodInSecs`)**: Maximum number of seconds logs remain searchable (e.g., 90 days = 7,776,000 seconds).
* **Freezing Policy**: If either limit is hit, the oldest bucket rolls to frozen.

---

## Module 4: Getting Data In (GDI) — Universal Forwarders & Credentials

### 1. The Universal Forwarder (UF)
The Universal Forwarder is an ultra-lightweight agent installed on endpoints. It does not parse or index data; it simply captures logs and securely forwards them.

### 2. Splunk Cloud Credentials Package (`splunkclouduf.spl`)
Because Splunk Cloud is behind public endpoints with strict mTLS encryption, forwarders require a pre-generated credential app:

1. In Splunk Cloud UI, navigate to: **Settings > Forwarder Management > Universal Forwarder Credentials**.
2. Download the app package: `splunkclouduf.spl`.
3. Install this package onto your on-premises Universal Forwarder:
   ```bash
   $SPLUNK_HOME/bin/splunk install app /path/to/splunkclouduf.spl -auth admin:password
   ```
4. This installs an app under `$SPLUNK_HOME/etc/apps/splunkclouduf/` containing:
   * `server.conf` with private certificates (`server.pem`, `cacert.pem`).
   * `outputs.conf` configured with the Splunk Cloud endpoint and port `9997`:
   ```ini
   [tcpout]
   defaultGroup = splunkcloud

   [tcpout:splunkcloud]
   server = input-<stackname>.cloud.splunk.com:9997
   clientCert = $SPLUNK_HOME/etc/apps/splunkclouduf/default/server.pem
   sslPassword = <encrypted_pass>
   ```

### 3. Monitoring Files (`inputs.conf`)
On the Universal Forwarder, create `$SPLUNK_HOME/etc/apps/<custom_app>/local/inputs.conf`:
```ini
[monitor:///var/log/nginx/access.log]
disabled = false
index = web_traffic
sourcetype = nginx:plus:access
```

---

## Module 5: HTTP Event Collector (HEC) & Heavy Forwarders

### 1. HTTP Event Collector (HEC)
HEC is a high-throughput token-based REST endpoint for sending JSON, raw text, or metrics without a forwarder agent:

* **Cloud URL Format**: `https://http-inputs-<stackname>.cloud.splunk.com:8088/services/collector`
* **HEC Header**: `Authorization: Splunk <HEC-TOKEN-GUID>`
* **Sample Payload**:
  ```bash
  curl -k https://http-inputs-mycompany.cloud.splunk.com:8088/services/collector \
     -H "Authorization: Splunk aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" \
     -d '{"event": "User logged in successfully", "sourcetype": "app:audit", "index": "security"}'
  ```

### 2. When to Use a Heavy Forwarder (HF)
A Heavy Forwarder is a full Splunk Enterprise instance dedicated to pre-processing before data reaches Splunk Cloud:
* **Data Anonymization / Masking**: Using `props.conf` and `transforms.conf` with regex `SEDCMD` to mask Credit Cards, Passwords, or PII before sending to cloud.
* **Routing**: Sending audit data to Splunk Cloud while routing duplicate copies to on-premise cold archival.
* **Syslog Aggregation**: Listening on UDP/TCP port `514` on your local network.

---

## Module 6: Managing Apps, Add-ons & App Inspect

### 1. Self-Service Apps via Splunkbase
Customers can browse and install approved apps (Cisco, AWS, Microsoft 365, Palo Alto, Linux TA) directly from Splunkbase with 1-click in the web interface.

### 2. Private Apps & App Inspect
When deploying your own custom app or dashboard package, Splunk Cloud requires it to pass **App Inspect**:
* **Folder Structure**:
  ```
  my_custom_app/
  ├── app.manifest
  ├── default/
  │   ├── app.conf
  │   ├── data/ui/views/
  │   └── savedsearches.conf
  ├── metadata/
  │   └── default.meta
  └── README/
  ```
* **Security Rules Checked by App Inspect**:
  * No external OS system calls (`os.system`, `subprocess`) running unvalidated scripts.
  * Python 3.9+ syntax only (no legacy Python 2).
  * No hardcoded plaintext passwords or private keys.
  * Valid `app.manifest` specifying version and licensing.

---

## Module 7: Cloud Monitoring Console (CMC) & Health

The **Cloud Monitoring Console (CMC)** is your administrative cockpit for observing stack performance:

1. **License Usage Dashboard**:
   * Tracks daily ingestion in GB against your purchased license volume.
   * Warns of potential quota overages.
2. **Search Concurrency & Performance**:
   * Identifies slow, unoptimized searches using unbounded wildcards (`*`).
   * Highlights skipped scheduled searches due to pool saturation.
3. **Forwarder Connection Health**:
   * Real-time monitoring of connected Universal and Heavy Forwarders.
   * Detects forwarders that have stopped sending data or experiencing network lag.

---

## Module 8: Hands-On Lab Exercises & Practice Scenarios

### Lab 1: Configure a Universal Forwarder to Cloud
1. Download `splunkclouduf.spl` from your Splunk Cloud instance.
2. Unpack it into `$SPLUNK_HOME/etc/apps/` on your test server.
3. Verify connection:
   ```bash
   $SPLUNK_HOME/bin/splunk btool outputs list --debug
   ```
4. Confirm port `9997` connectivity:
   ```bash
   nc -zv input-<stackname>.cloud.splunk.com 9997
   ```

### Lab 2: Send Real-Time Logs via HEC Token
1. In Splunk Cloud UI: Go to **Settings > Data Inputs > HTTP Event Collector**.
2. Click **New Token**, name it `DevToken`, and assign default index `main`.
3. Send a test curl event and verify in Splunk Search:
   ```spl
   index=main sourcetype="app:audit"
   ```

---

## Module 9: Splunk Cloud Certification Exam Practice Q&A

**Q1: Which component is customer-managed in the Splunk Cloud Shared Responsibility Model?**  
A. Operating system patches  
B. Search Head Cluster provisioning  
C. Universal Forwarder installation & inputs configuration  
D. Indexer hardware upgrades  
*Answer: **C**.*

**Q2: What is the primary purpose of the `splunkclouduf.spl` package?**  
A. To install the Splunk Web interface  
B. To configure SSL certificates and outputs.conf pointing to Splunk Cloud indexers  
C. To upgrade the Universal Forwarder version  
D. To create user accounts  
*Answer: **B**.*

**Q3: In Splunk Cloud Victoria Experience, how are private apps installed?**  
A. By contacting Splunk Support to deploy via Cluster Master CLI  
B. Directly via the Splunk Cloud Web UI with automated App Inspect validation  
C. By copying files directly into `/opt/splunk/etc/apps` via SSH  
D. Private apps cannot be installed in Victoria  
*Answer: **B**.*

**Q4: Which index type should be selected when ingesting pure numeric time-series system performance metrics?**  
A. Events Index  
B. Metrics Index  
C. Summary Index  
D. Cold Index  
*Answer: **B**.*

---
*Created as part of the FFM Splunk Cloud Training Series.*
