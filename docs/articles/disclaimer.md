# Disclaimer and Legal Notices

This page contains important legal information about PolarSharp. Please read it carefully before using this software.

---

## No Affiliation with Polar.sh

PolarSharp is an **independent, community-developed open-source library**. The author of PolarSharp has no affiliation, partnership, sponsorship, endorsement, certification, or relationship of any kind with:

- Polar.sh
- Polar Software Inc. (or any entity operating the Polar.sh platform)
- The operators, employees, contractors, or agents of the polar.sh website

PolarSharp is **not** an official Polar.sh SDK. It is not endorsed by, certified by, commissioned by, or in any way associated with Polar.sh. The name "Polar" and the Polar.sh brand, trademarks, and service marks are the exclusive property of their respective owners. Their use in this project is solely for the purpose of describing interoperability with the Polar.sh API and does not imply any affiliation or endorsement.

If you require an officially supported integration, contact Polar.sh directly.

---

## No Warranties — Use at Your Own Risk

THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. TO THE MAXIMUM EXTENT PERMITTED BY APPLICABLE LAW, THE AUTHOR EXPRESSLY DISCLAIMS ALL WARRANTIES, INCLUDING BUT NOT LIMITED TO:

- **Warranties of merchantability** — no warranty that this software is fit for general commercial use;
- **Warranties of fitness for a particular purpose** — no warranty that this software meets your specific requirements, technical or otherwise;
- **Warranties of accuracy or reliability** — no warranty that this software will produce correct, accurate, or reliable results in all circumstances;
- **Warranties of uninterrupted or error-free operation** — no warranty that this software will operate without interruption, bugs, defects, or failures;
- **Warranties of security** — no warranty that this software is free from vulnerabilities, security flaws, or exploitable conditions, known or unknown;
- **Warranties of API compatibility** — no warranty that this software will remain compatible with any particular version, revision, or state of the Polar.sh API; and
- **Warranties of regulatory or compliance suitability** — no warranty that use of this software satisfies any legal, regulatory, industry, or compliance requirement, including but not limited to PCI DSS, GDPR, SOC 2, ISO 27001, HIPAA, or any other standard.

**USE OF THIS SOFTWARE IS ENTIRELY AT YOUR OWN RISK.** You bear sole responsibility for evaluating the suitability of this software for your intended use case, for testing it in your environment, and for all consequences — financial, legal, operational, reputational, or otherwise — arising from its use or inability to use.

---

## Independent Testing Disclosure

PolarSharp has been designed, implemented, and tested by its author to the best of their individual ability. It has **not** been:

- independently audited or reviewed by a third-party security firm;
- penetration-tested by an external party;
- certified by any compliance body or standards organization;
- reviewed or validated by Polar.sh or any representative thereof; or
- tested by any person or organization other than the author.

All tests, benchmarks, and security measures present in this repository represent the author's own work and judgement. They do not constitute a guarantee of correctness, security, or reliability.

---

## Security Features — No Guarantee of Complete Protection

PolarSharp was designed with enterprise-class security as an explicit goal. The library incorporates significant defensive measures, including:

- **HMAC-SHA256 webhook signature verification** with timing-uniform error responses to prevent signature oracle attacks
- **Replay attack prevention** via webhook timestamp validation
- **Payload size enforcement** (1 MB default cap) to mitigate memory exhaustion denial-of-service attacks
- **Rate limiting** per IP address on webhook endpoints
- **IP allowlisting** support for restricting webhook delivery to known source ranges
- **TLS 1.2+ enforcement** with certificate revocation checking on all outbound connections
- **SSRF (Server-Side Request Forgery) mitigation** blocking RFC 1918 and metadata endpoint addresses
- **Anomaly detection metrics** surfacing elevated verification failure rates via `polar.webhooks.suspicious_activity`
- **Automatic PII redaction** in structured log output
- **Per-tenant bulkhead isolation** preventing one tenant's failure from affecting others

These features are designed to provide meaningful, layered protection against common and well-documented attack vectors. **However:**

> No software can guarantee protection against all known or unknown attack vectors. The threat landscape evolves continuously. New vulnerabilities are discovered regularly in software, dependencies, protocols, and platforms. No representation is made that the security controls in this library will be effective against all present or future attack techniques, zero-day vulnerabilities, novel attack methods, or threats that have not yet been publicly disclosed.

**It is your responsibility** to:

- Perform your own independent security assessment of this library and its dependencies before deploying to production;
- Monitor published security advisories for this library and all its dependencies (see [GitHub Security Advisories](https://github.com/mollsandhersh/Polar.sh_Nuget/security/advisories));
- Keep all dependencies up to date;
- Apply additional security controls appropriate to your specific environment, threat model, and risk tolerance;
- Engage qualified security professionals to review your integration if your application handles sensitive financial, personal, or regulated data; and
- Never rely solely on library-level protections as your only security control.

---

## Limitation of Liability

IN NO EVENT SHALL THE AUTHOR, CONTRIBUTORS, OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES, OR OTHER LIABILITY OF ANY KIND — WHETHER IN AN ACTION OF CONTRACT, TORT (INCLUDING NEGLIGENCE), STRICT LIABILITY, OR OTHERWISE — ARISING FROM, OUT OF, OR IN CONNECTION WITH THIS SOFTWARE OR THE USE OR OTHER DEALINGS IN THIS SOFTWARE.

THIS LIMITATION APPLIES TO ALL DAMAGES OF ANY KIND, INCLUDING WITHOUT LIMITATION:

- direct, indirect, incidental, special, exemplary, or consequential damages;
- loss of profits, revenue, data, business, or goodwill;
- fraudulent transactions or unauthorized charges processed through integrations using this software;
- data breaches, unauthorized access, or disclosure of personal or financial information;
- regulatory fines, penalties, or enforcement actions;
- costs of procuring substitute goods or services; and
- business interruption, however caused and on any theory of liability,

**EVEN IF THE AUTHOR HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH DAMAGES**, and even if any remedy set forth herein is found to have failed its essential purpose.

Some jurisdictions do not allow the exclusion or limitation of incidental or consequential damages, so the above limitation may not apply to you in full.

---

## Third-Party Services

This library is designed to communicate with the Polar.sh API over the internet. Your use of Polar.sh is governed exclusively by Polar.sh's own terms of service, privacy policy, data processing agreements, and acceptable use policy. Those agreements are entirely independent of this library and its author, and the author has no ability to influence, enforce, or guarantee compliance with them. Review Polar.sh's terms directly at [polar.sh](https://polar.sh) before using this library in any application.

---

## Open Source License

PolarSharp is released under the [MIT License](https://github.com/mollsandhersh/Polar.sh_Nuget/blob/main/LICENSE). The MIT License permits use, copying, modification, merging, publishing, distribution, sublicensing, and sale of this software, subject to the condition that the copyright notice and permission notice are preserved in all copies or substantial portions of the software. The MIT License does not grant any warranty, and this disclaimer supplements — not replaces — the warranty disclaimers already present in the MIT License text.

---

*Last updated: 2026. This disclaimer may be updated without notice. The version in the repository at the time of your installation governs your use of that version of the software.*
