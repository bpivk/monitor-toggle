# Phase 26 — External API Capability Coverage

**Detector:** `api-coverage.cjs --json` → `detected: true` (signal: `rest` / GitHub Releases REST API)
**External API integrated:** GitHub REST API v3 — `https://api.github.com` (public, unauthenticated)
**Repository under check:** `bpivk/monitor-toggle` (confirmed via `git remote -v`)
**Client:** hand-rolled `System.Net.Http.HttpClient` + `System.Text.Json` (BCL only — zero new NuGet packages, per CLAUDE.md "What NOT to Use" and `.planning/research/STACK.md` §1)

---

## Capability Matrix

| Capability | Endpoint / Surface | Disposition | Reason |
|------------|--------------------|-------------|--------|
| Read the repo's latest published release | `GET /repos/bpivk/monitor-toggle/releases/latest` | **INTEGRATE** | Exactly what UPDATE-02 requires. Endpoint already excludes drafts and prereleases server-side, so no client-side filtering is needed. `User-Agent` header is mandatory (GitHub returns 403 without one). |
| Download a release asset | `GET` on `assets[].browser_download_url` (redirects to `objects.githubusercontent.com`) | **INTEGRATE** | Exactly what UPDATE-04 requires — the single attached `RigToggle.App.exe`. |
| Download the checksum sidecar asset | `GET` on the `.sha256` asset's `browser_download_url` | **INTEGRATE** | D-10/D-11 — the published checksum the updater verifies the downloaded exe against before the swap. Same asset-download mechanism as the row above; listed separately because it is a distinct capability the phase newly depends on `release.yml` publishing. |
| List all releases | `GET /repos/{owner}/{repo}/releases` | **OPT-OUT** | Not needed — this app only ever reads its own *latest* release. A full listing would add pagination handling and rate-limit pressure for zero behavioural gain. |
| Read a release by tag / by id | `GET /repos/{owner}/{repo}/releases/tags/{tag}`, `.../releases/{id}` | **OPT-OUT** | Not needed — the app never targets a specific historical version; "skip this version" (D-02) is a local suppression marker, not a request for a different release. |
| Create / update / delete a release | `POST`/`PATCH`/`DELETE /repos/{owner}/{repo}/releases[/{id}]` | **OPT-OUT** | Not needed — this app only ever reads its own latest release and downloads one asset, never writes to GitHub. Release creation is `release.yml`'s job, via `softprops/action-gh-release`, using the workflow's own `GITHUB_TOKEN`. |
| Upload a release asset | `POST {upload_url}` | **OPT-OUT** | Not needed — same reason as release creation. The `.sha256` asset (D-10) is attached by the CI workflow's existing `action-gh-release` step, not by the app. |
| Webhooks / release events | `POST /repos/{owner}/{repo}/hooks`, webhook receivers | **OPT-OUT** | Not needed — a desktop tray app has no public HTTP endpoint to receive a webhook on, and the on-launch poll (UPDATE-02) is the locked design. |
| Authenticated requests (PAT / GitHub App / OAuth) | `Authorization` header | **OPT-OUT** | Deliberately not used. The repo is public; the unauthenticated rate limit (60 req/hr/IP) is far above one launch-time check by a single user. Shipping a token inside a distributed exe would be a credential-disclosure liability with no benefit. |
| Rate-limit introspection | `GET /rate_limit` | **OPT-OUT** | Not needed for one check per launch. A 403 rate-limit response is handled by the same silent-no-op (automatic check) / Warning-toast (manual check, D-07) path as any other check failure. |
| GraphQL API | `POST /graphql` | **OPT-OUT** | Requires authentication for all requests. The two REST reads above are sufficient and auth-free. |

---

## Error / Degradation Contract

| Condition | App behaviour | Source |
|-----------|---------------|--------|
| Network unreachable, DNS failure, timeout | Automatic check: silent no-op. Manual check: Warning toast. | D-07, PITFALLS.md UX table |
| HTTP 403 (missing `User-Agent`, or rate limit) | Same as above; the reason string is surfaced in the manual-check toast and in `debug.log`. | PITFALLS.md Performance Traps |
| HTTP 404 (no published release yet) | Treated as "no update available", not an error. | ARCHITECTURE.md Data Flow |
| Response parses but has no exe asset | Treated as "no update available". | Anti-Pattern 4 guard |
| Asset URL is not HTTPS or not a GitHub-owned host | Download refused; treated as an apply failure (Warning toast). | Threat T-26-01 |

---

*Generated during Phase 26 planning (2026-08-22). Consumed by the AI-integration coverage gate.*
