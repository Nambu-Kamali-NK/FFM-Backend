# FFM Security Testing Specification

## 1\. Objective

Perform security testing of the FFM application using three separate testing approaches:

1. DAST – Dynamic Application Security Testing
2. SAST – Static Application Security Testing
3. Penetration Testing

Each testing approach must be performed **separately** and must produce an independent security report.

The purpose is to identify security vulnerabilities in the FFM application and provide the **exact vulnerability name and confirmed vulnerability count** for each testing approach.

Testing techniques and attack scenarios must be distinguished from confirmed vulnerabilities. A testing technique or attack attempt must not be counted as a vulnerability unless a genuine security weakness is confirmed.

\---

# 2\. Testing Order

The testing must be performed in the following order:

```plaintext
FFM Security Testing
        |
        +---- 1. DAST
        |       |
        |       +---- DAST Security Report
        |
        +---- 2. SAST
        |       |
        |       +---- SAST Security Report
        |
        +---- 3. Penetration Testing
                |
                +---- Penetration Testing Report
```

Do not combine the findings from the three testing approaches into a single phase.

Each phase must have its own vulnerability list and confirmed count.

If the same underlying security issue is identified by more than one testing approach, keep the finding in each applicable report and mark it as a **cross-method finding**.

\---

# 3\. Phase 1 – DAST

## 3.1 Objective

Perform Dynamic Application Security Testing against the **running FFM application and APIs**.

The testing must interact with the application through HTTP/API requests and analyze runtime behavior and responses.

DAST must evaluate the security of the running application from an external perspective.

\---

## 3.2 DAST Scope

Test the available FFM APIs, including:

* Identity APIs
* Authentication APIs
* Workforce APIs
* Employee APIs
* Attendance APIs
* Task APIs
* Expense APIs
* Visit Verification APIs
* GPS APIs
* Other available FFM endpoints

**Each available API endpoint must be tested individually where applicable.**

Testing should include:

* Unauthenticated requests
* Authenticated requests
* Invalid requests
* Malicious input
* Different user roles
* Access to resources belonging to other users where applicable

Use only the authorized development/test environment.

Do not perform destructive testing against production or real user data.

\---

## 3.3 DAST Testing Techniques

At minimum, test the following where applicable:

1. Security configuration testing
2. HTTP method tampering
3. API parameter tampering
4. Authentication testing
5. Authorization testing
6. BOLA/IDOR testing
7. Input validation testing
8. Rate-limiting testing
9. Information-disclosure testing
10. JWT/token handling testing
11. File-upload security testing
12. Other relevant API security testing techniques

These are **testing techniques**, not automatic vulnerability counts.

Only confirmed security vulnerabilities discovered through these techniques must be included in the DAST count.

\---

## 3.4 FFM Role-Based DAST Testing

Where the roles exist in the authorized test environment, test API access for:

```plaintext
SYSTEM\_ADMIN
ADMIN
EXECUTIVE
MANAGER
SUPERVISOR
EMPLOYEE
```

Verify that:

* Authorized roles can access permitted resources.
* Unauthorized roles are blocked.
* One user cannot access another user's protected data without permission.
* Privileged operations cannot be performed by lower-privileged users.

Pay particular attention to:

* Employee APIs
* Attendance APIs
* Task APIs
* Expense APIs
* GPS APIs
* Visit Verification APIs

\---

## 3.5 DAST Report Requirement

At the end of DAST testing, generate:

`FFM-DAST-SECURITY-REPORT.md`

The report must contain:

|Field|Requirement|
|-|-|
|Confirmed Vulnerability Name|Exact vulnerability name|
|Count|Number of confirmed unique findings|
|Severity|Critical / High / Medium / Low|
|Affected Endpoint|API endpoint|
|HTTP Method|GET / POST / PUT / PATCH / DELETE etc.|
|Test Technique|Technique used to discover the issue|
|Description|What the vulnerability means|
|Evidence|Request/response or other evidence|
|Impact|Possible or demonstrated security impact|
|Recommendation|Recommended remediation|
|Status|Open / Fixed / Not Applicable|
|Cross-Method Finding|Yes / No, where applicable|

### DAST Summary

Use confirmed vulnerability names in the final summary, not testing-technique names.

Example:

```plaintext
DAST Vulnerability Summary

Broken Access Control / BOLA: X
Information Disclosure: X
Security Misconfiguration: X
SQL Injection: X
Rate Limiting Weakness: X
JWT / Token Security Weakness: X
File Upload Security Weakness: X
Other Confirmed Vulnerability: X

Total DAST Vulnerabilities: X
```

Do not estimate or assume the count.

If a category was tested and no vulnerability was found, report:

```plaintext
SQL Injection: 0
```

\---

# 4\. Phase 2 – SAST

## 4.1 Objective

Perform Static Application Security Testing against the FFM source code.

The primary SAST analysis must be performed without executing the application.

\---

## 4.2 SAST Scope

Analyze relevant FFM source code, including:

* Identity service
* Workforce service
* API layer
* Application layer
* Infrastructure layer
* Authentication/authorization implementation
* Database access code
* File handling
* Logging
* Cryptographic implementations
* Configuration-related code
* Security-sensitive business logic

\---

## 4.3 SAST Security Checks

At minimum, check for:

1. Hardcoded Secrets
2. Insecure Deserialization
3. Integer Overflow
4. Weak Cryptography
5. Sensitive Data in Logs
6. SQL Injection
7. Command Injection
8. Cross-Site Scripting (XSS), where applicable
9. Path Traversal

Also identify other genuine code-level security vulnerabilities discovered during analysis.

The listed items are **SAST checks/categories**, not automatic findings.

Only confirmed code-level vulnerabilities must be included in the final SAST count.

\---

## 4.4 SAST Report Requirement

At the end of SAST testing, generate:

`FFM-SAST-SECURITY-REPORT.md`

The report must contain:

|Field|Requirement|
|-|-|
|Confirmed Vulnerability Name|Exact vulnerability name|
|Count|Number of confirmed unique findings|
|Severity|Critical / High / Medium / Low|
|File|Affected source file|
|Location|Class / method / line if available|
|Description|Security weakness|
|Evidence|Relevant code reference|
|Impact|Possible security impact|
|Recommendation|Recommended remediation|
|Status|Open / Fixed / False Positive|
|Cross-Method Finding|Yes / No, where applicable|

### SAST Summary

Example:

```plaintext
SAST Vulnerability Summary

Hardcoded Secrets: X
Insecure Deserialization: X
Integer Overflow: X
Weak Cryptography: X
Sensitive Data in Logs: X
SQL Injection: X
Command Injection: X
Cross-Site Scripting: X
Path Traversal: X

Total SAST Vulnerabilities: X
```

The count must represent **actual unique confirmed findings**, not the number of files scanned.

False positives must be recorded separately and must not be included in the confirmed vulnerability count.

\---

# 5\. Phase 3 – Penetration Testing

## 5.1 Objective

Perform authorized application-level penetration testing against the FFM application.

The objective is to manually investigate whether security weaknesses can be exploited and to validate their actual security impact.

Penetration testing may use manual techniques together with supporting security tools.

\---

## 5.2 Penetration Testing Scope

Test the FFM application's:

* Authentication
* Authorization
* Role-based access
* JWT/session handling
* API workflows
* Business logic
* File uploads
* Concurrent operations
* User privilege boundaries

\---

## 5.3 Penetration Testing Scenarios

At minimum, test the following where applicable:

1. Brute Force Attack
2. Race Condition
3. Privilege Escalation
4. Session / JWT Attacks
5. Workflow Bypass
6. File Upload Attacks
7. BOLA / IDOR
8. Authentication Bypass
9. Authorization Bypass
10. Business Logic Abuse
11. Parameter Manipulation
12. Other relevant application-level attack scenarios

These are **penetration-testing scenarios/attack techniques**.

A failed attack attempt is not automatically a vulnerability.

Only a confirmed exploitable security weakness must be counted as a penetration-testing finding.

\---

## 5.4 Safety Restrictions

Penetration testing must be performed only against the authorized FFM test/development environment.

Do not:

* Attack external systems
* Attack third-party services
* Perform uncontrolled DoS/DDoS
* Delete production data
* Modify real user data
* Exfiltrate real sensitive information
* Perform destructive exploitation

Use controlled test accounts and test data.

\---

## 5.5 Penetration Testing Report Requirement

At the end of penetration testing, generate:

`FFM-PENETRATION-TEST-REPORT.md`

The report must contain:

|Field|Requirement|
|-|-|
|Confirmed Vulnerability Name|Exact vulnerability name|
|Count|Number of confirmed unique findings|
|Severity|Critical / High / Medium / Low|
|Affected Endpoint|API endpoint/functionality|
|Test Scenario|Attack/test scenario used|
|Evidence|Request/response or other evidence|
|Exploitation Result|Successful / Failed|
|Impact|Demonstrated security impact|
|Recommendation|Recommended remediation|
|Status|Open / Fixed|
|Cross-Method Finding|Yes / No, where applicable|

### Penetration Testing Summary

Example:

```plaintext
Penetration Testing Vulnerability Summary

Broken Access Control: X
Authentication Bypass: X
Privilege Escalation: X
BOLA / IDOR: X
Session / JWT Security Weakness: X
Workflow Bypass: X
Race Condition: X
File Upload Security Weakness: X
Business Logic Vulnerability: X
Other Confirmed Vulnerability: X

Total Penetration Testing Findings: X
```

Do not count an attack scenario merely because the scenario was attempted.

\---

# 6\. Finding and Counting Methodology

## 6.1 Confirmed Finding

A finding must be counted only when sufficient evidence confirms that a genuine security weakness exists.

\---

## 6.2 Unique Finding

Multiple observations of the same underlying vulnerability must not be counted as separate vulnerabilities within the same testing phase unless the reporting methodology explicitly defines them as separate findings.

Example:

```plaintext
GET /api/Employees/1
GET /api/Employees/2
GET /api/Employees/3
```

If all three demonstrate the same underlying BOLA authorization flaw, report one BOLA finding and list the affected endpoints/resources as evidence.

\---

## 6.3 Zero Findings

If testing was performed but no vulnerability was confirmed:

```plaintext
SQL Injection: 0
```

Do not fabricate findings.

\---

## 6.4 False Positives

False positives must be recorded separately and excluded from confirmed vulnerability counts.

\---

## 6.5 Cross-Method Findings

If the same underlying weakness is discovered by multiple testing approaches, for example:

```plaintext
DAST     → Broken Access Control
SAST     → Authorization implementation weakness
Pentest  → Exploitable Broken Access Control
```

keep the finding in each applicable report and mark it as a **cross-method finding**.

Do not merge the phase-specific counts.

\---

# 7\. Final Combined Summary

After completing all three phases, generate:

`FFM-SECURITY-TESTING-SUMMARY.md`

Use the following structure:

```plaintext
FFM Security Testing Summary

-----------------------------------------
DAST
-----------------------------------------
Total DAST Vulnerabilities: X

-----------------------------------------
SAST
-----------------------------------------
Total SAST Vulnerabilities: X

-----------------------------------------
Penetration Testing
-----------------------------------------
Total Penetration Testing Findings: X

-----------------------------------------
Overall
-----------------------------------------
DAST: X
SAST: X
Penetration Testing: X
```

The final summary must clearly distinguish:

* Vulnerabilities identified by DAST
* Vulnerabilities identified by SAST
* Vulnerabilities identified by Penetration Testing

Do not replace these with one combined vulnerability number.

\---

# 8\. Expected Deliverables

At the end of the complete security-testing activity, produce:

1. `FFM-DAST-SECURITY-REPORT.md`
2. `FFM-SAST-SECURITY-REPORT.md`
3. `FFM-PENETRATION-TEST-REPORT.md`
4. `FFM-SECURITY-TESTING-SUMMARY.md`

Each report must contain:

* Exact confirmed vulnerability names
* Confirmed vulnerability counts
* Severity
* Evidence
* Affected area
* Impact
* Recommended remediation
* Status

Do not fabricate vulnerabilities or counts.

If a vulnerability category was tested but no vulnerability was found, report a zero count instead of omitting the category.

\---

# 9\. Implementation Instructions for Antigravity

Before performing each phase:

1. Inspect the current FFM application/source code and authorized test environment.
2. Identify the exact scope available for the selected testing phase.
3. Do not mix DAST, SAST, and penetration-testing findings during execution.
4. Maintain a separate finding list for each phase.
5. Record evidence for every confirmed vulnerability.
6. Distinguish test attempts from confirmed vulnerabilities.
7. Do not fabricate missing evidence or counts.
8. Use only the authorized test/development environment.
9. After each phase, generate its independent security report.
10. Run the phases in this exact order:

    * DAST
    * SAST
    * Penetration Testing
11. After all three phases, generate the combined summary report.
12. Clearly report:

    * Testing performed
    * APIs/files/scenarios covered
    * Vulnerabilities found
    * Vulnerabilities with zero findings
    * Confirmed counts
    * False positives
    * Cross-method findings
    * Limitations or untested areas

All testing results must be evidence-based and reproducible.

