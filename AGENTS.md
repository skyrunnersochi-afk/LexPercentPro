# LexPercentPro

Purpose: calculate interest for use of another person's funds.

Requirements are in docs/Specification-v1.1.docx; docs/Specification-v1.1.txt is a searchable extraction. Version 1.1 supersedes 1.0. Read relevant sections instead of loading the full specification on every task. Development environment: C# / .NET 10 LTS; Avalonia 12 provides the Windows UI. Source and tests are in src/ and tests/. Use scripts/dev.ps1 for build, test and publishing.

Preserve the specified module boundaries: LexPercent.Domain, Core, Documents, Storage, UI, Tests. Core depends only on Domain. Documents and UI must not calculate interest. Implement section 4 with decimal, per-interval rounding to two places using AwayFromZero, calendar-year boundaries, and deterministic event ordering. Interest begins on the accrual date (11th; January 16th), including the first accrual. Payments reduce the interest base on the NEXT calendar day. Weekends do not shift dates. The corrected reference uses 14% through September 8, 2026 and must produce 38 intervals, 673 days, 14,393.74 interest and 96,607.59 remaining principal. The printed СПРАВКА adds number, calculation date and textual totals to the supplied layout; detailed accrual/payment explanations and worked formulas belong in the separate protocol.

Keep changes focused and context concise. When calculation logic is added or changed, verify it with explicit expected results, including date boundaries, leap years, rate changes, and rounding. Document calculation assumptions and sources alongside the implementation.

Use the user's global Codex model defaults. Run only relevant checks; report when a check cannot run. Never report a build or tests as passing when no build or test suite exists.

## Publication policy authorized by the user

After each requested change, run relevant checks, update documentation and CHANGELOG, and publish the changes to this GitHub repository. Prefer a focused branch and pull request, then merge after checks pass. Do not force-push shared branches. For application changes, increment the application version and publish a new immutable installer release; never replace an existing release asset. Keep build outputs, installer binaries, caches, secrets and user calculation files out of Git history. GitHub Actions publishes installers from main after tests pass. Use the existing connector when Git CLI authentication is unavailable. Do not request the same publication authorization again.
