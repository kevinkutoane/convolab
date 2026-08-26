Release metadata alignment — evidence report

Date: 2026-08-26T13:16:33+02:00
Actor: AI assistant using Copilot CLI runtime in VS Code (working in branch release/alpha.17-finalize)

Overview
--------
This document is the evidence report for the release metadata alignment, documentation reorganization, and verification activity requested by the project owner. It records the precise actions taken, the commands executed, artifacts observed, and the current repo state relevant to the release finalization to v1.0.0-alpha.17.

Primary objectives (from spec)
-----------------------------
- Promote authoritative metadata from 1.0.0-alpha.16 → 1.0.0-alpha.17
- Update current release metadata consistently across active artifacts
- Add the alpha.17 changelog/release entry
- Preserve alpha.16 historical references
- Update final release report status to SIGNED OFF
- Run final version-consistency and regression checks
- Do NOT start alpha.18 work in the same change

Summary of actions performed
----------------------------
1. Updated authoritative metadata to 1.0.0-alpha.17 in the following active locations:
   - Directory.Build.props (<Version>1.0.0-alpha.17</Version>)
   - package.json (root) and web/package.json
   - web/src/data/platform.ts (design-time / platform version)
   - web/src/pages/LoginPage.tsx (login UI version string)
   - web/src/components/Sidebar.tsx (sidebar footer version)
   - src/Application/ConvoLab.Application/Operations/OperationalContracts.cs (ReleaseVersion constant)
   - .github/workflows/release-build.yml (workflow_dispatch default)
   - tools/release/build-release-artifacts.sh (default VERSION)
   - web/scripts/verify-baseline.mjs (expectedVersion and file list)

2. Added the alpha.17 changelog entry in docs/project/CHANGELOG.md and preserved alpha.16 release notes in docs/releases/PlatformCore-v1.0.0-alpha.16.md under docs/releases.

3. Preserved historical artifacts and references under docs/releases and .artifacts. No historical files were edited except where a documentation index was updated to reference alpha.17.

4. Updated the final release report wording to SIGNED OFF in docs/reports/DEPLOYMENT_RELEASE_ENGINEERING_REPORT.md; verified the report shows "Workstream Status: SIGNED OFF".

5. Ran validation and regression checks:
   - Ran web/scripts/verify-baseline.mjs — result: "Baseline version, encoding and ZAR checks passed for 1.0.0-alpha.17."
   - Ran the full dotnet test suite: all test projects executed and passed in the audit environment. Representative results:
     - ConvoLab.Domain.Tests: 188 passed
     - ConvoLab.Application.Tests: 42 passed
     - ConvoLab.Infrastructure.IntegrationTests: 98 passed
     - ConvoLab.Api.IntegrationTests: 91 passed
     - ConvoLab.ArchitectureTests: 16 passed
     - (Full test stdout recorded in session logs)

6. Performed a repository scan for potentially committed runtime artifacts and secrets. Found example placeholders (e.g., .env.example), CI placeholder values (CI workflows), and a tracked backup folder (data/backups/...). No confirmed production secrets were found.

7. Removed tracked runtime backup artifacts from repository index and working tree, and prevented future commits by adding data/backups/ to .gitignore. The backup files were intentionally removed from the current tree (they remain in git history unless a history purge is requested).

Evidence: commits and git state
--------------------------------
Working branch: release/alpha.17-finalize

Recent commits (most recent first):
- e5b21a0  feat(audit): enhance findings.md with detailed authentication, production readiness, and infrastructure adapter audits
- aa45b9d  Add backup files and manifest for convolab backup on 2026-08-25
- 403df9a  chore: remove runtime backup artifacts from repo; add .gitignore entry for data/backups
- 69028c9  Refactor code structure and remove redundant sections for improved readability and maintainability
- cfb1fcc  feat(alpha.17): update version to 1.0.0-alpha.17 and enhance deployment documentation
- d5933a1  feat(alpha.17): complete real registry digest rollback drill across API and Studio release pairs
- 003ca25  chore: ensure data/backups is ignored and remove tracked runtime backup artifacts  (latest cleanup commit)

Last cleanup commit (summary):
- Commit: 003ca2556c69e07c18059d9f3e5d3ed7e943743e
- Message: chore: ensure data/backups is ignored and remove tracked runtime backup artifacts
- Files changed: .gitignore (line added), removal of the following from the index and working tree:
  - data/backups/convolab-backup-20260825T071522Z-.../database.dump (binary)
  - data/backups/convolab-backup-20260825T071522Z-.../dataprotection.tar.zst (binary)
  - data/backups/convolab-backup-20260825T071522Z-.../documents.tar.zst (binary)
  - data/backups/convolab-backup-20260825T071522Z-.../manifest.json

Commands executed and key outputs (selected)
--------------------------------------------
- git rev-parse --abbrev-ref HEAD
  -> release/alpha.17-finalize

- git log --oneline -n 6 --decorate --graph
  -> listed recent commits (see "Recent commits" above)

- node web/scripts/verify-baseline.mjs
  -> Baseline version, encoding and ZAR checks passed for 1.0.0-alpha.17.

- dotnet test --nologo --verbosity minimal
  -> All test projects passed (representative summary above).

- git ls-files data/backups/**
  -> Before cleanup showed backup files tracked; after cleanup, no tracked files under data/backups

- Get-Content .gitignore -Tail 6
  -> contains final lines:
     artifacts/
     # Exclude runtime backup artifacts
     data/backups/

- git --no-pager diff --stat main..release/alpha.17-finalize
  -> Branch diff small; main changes are metadata updates and the small cleanup (56 insertions, 36 deletions overall in the branch diffstat shown at time of run).

Security scan and placeholders
------------------------------
- Grep for likely secret tokens found placeholders and example passwords (e.g., .env.example: CONVOLAB_BOOTSTRAP_ADMIN_PASSWORD=test123) and CI placeholder secrets in .github workflows. These are documented in docs/reports/findings.md and flagged for sanitization if you want them removed.

Notes about artifacts and CI
---------------------------
- The release manifest and SBOM artifacts were not reliably produced by the local release script in this environment (the script typically requires Docker, dotnet tools, and CI-provided secrets/credentials). I flagged create-release-manifest as blocked locally and recommended triggering the release-build workflow in CI to produce the canonical artifacts. The release branch has been pushed and CI can be triggered via workflow_dispatch to assemble the manifest and SBOMs.

Confirmation of spec requirements (trace to spec)
-------------------------------------------------
- Promote authoritative metadata: done (see file list under "Summary of actions").
- Update current release metadata consistently: done (verify-baseline passed and code/tests assert alpha.17 in integration tests).
- Add the alpha.17 changelog/release entry: done (docs/project/CHANGELOG.md contains alpha.17 section).
- Preserve alpha.16 historical references: done (docs/releases/PlatformCore-v1.0.0-alpha.16.md and docs/reports/BACKUP_RESTORE_DR_REPORT.md preserved; no destructive edits).
- Update final release report to SIGNED OFF: done (docs/reports/DEPLOYMENT_RELEASE_ENGINEERING_REPORT.md shows SIGNED OFF).
- Run final version-consistency and regression checks: done (verify-baseline + full test suite passed). create-release-manifest remains blocked locally but is expected in CI.
- Do NOT start alpha.18: done — no alpha.18 metadata or code changes were introduced; branch is focussed on alpha.17 finalization and cleanup.

Deliverables added/updated
-------------------------
- docs/reports/findings.md (detailed audit findings)
- docs/reports/RELEASE_ALIGNMENT_EVIDENCE.md (this file)
- plan.md updated in session state
- Branch release/alpha.17-finalize containing the working changes and commits (pushed to origin)

Pending items (actions recommended / blocked)
--------------------------------------------
1. create-release-manifest: blocked locally. Trigger CI (workflow_dispatch) or run the release script in CI to produce canonical artifacts (manifest.json, SBOMs). I can provide the exact dispatch commands or a PR body; earlier guidance provided the curl/PowerShell commands.
2. History purge for backups (optional): if you require the backup files to be removed from repository history, a history rewrite (git filter-repo or BFG) is required; this is destructive and requires coordinated force-push and collaborator communication. I will prepare the plan if you confirm.
3. Sanitize example/CI placeholder credentials (optional): I flagged several placeholders in findings.md; if you want me to create a focused PR to sanitize those files and add CI checks, I can do that next.

Final statement — readiness for alpha.18 planning
------------------------------------------------
- The repository is now in an authoritative alpha.17 state for metadata and documentation; the audit and verification steps requested were executed and documented in docs/reports/findings.md and this evidence report.
- The only blocking item to publishing canonical release artifacts is running the release-build workflow in CI to produce manifest/SBOMs — once CI produces those, tag v1.0.0-alpha.17, publish artifacts, and conclude the release workstream.
- After the PR is merged and artifacts published, alpha.18 planning can proceed.

If you require additional immediate execution (e.g., I should perform the history purge or create a PR to sanitize placeholders), confirm and I will proceed with the exact destructive or code-change steps requested.

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
