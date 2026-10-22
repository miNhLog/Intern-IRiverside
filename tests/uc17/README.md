# UC17 Playwright scenarios

Scope: approved S01–S14 only. No production-source modifications. Provision is a console utility for isolated SQL environment lifecycle, not a test framework. Assertions use Playwright Test/APIRequestContext; visual evidence uses Playwright MCP.

Prerequisites: .NET 8, Node, SQL Server LocalDB, existing trusted HTTPS localhost certificate, free 17401/17402, built API/WEB assemblies matching source. Start from repository root. Never point at a business database.

After Owner approval, `powershell -ExecutionPolicy Bypass -File tests/uc17/setup.ps1` creates a GUID-named database with SQL ownership property, EF schema, additive upgrade and minimum synthetic fixtures. It starts the actual app binaries using isolated content roots. No repository appsettings or Development seeder. Runtime pointer is disposable, never commit it. Abort if inherited configuration overrides or ownership mismatch exists. SMTP/AI target closed loopback ports; Google is not configured. Do not invoke unrelated endpoints.

Install the pinned package into `<runtime>/tooling`, not repository node_modules: `npm install --prefix <runtime>/tooling @playwright/test@1.61.1 --no-audit --no-fund`. Set `UC17_RUNTIME`, `NODE_PATH=<runtime>/tooling/node_modules`, `PLAYWRIGHT_BROWSERS_PATH=<runtime>/browsers`; install Chromium with the tooling Playwright CLI. Execute its `test -c tests/uc17/playwright.config.cjs` once. Automatic retries are disabled. Do not repeat a completed initial pass without approval.

`coverage.spec.cjs` records remaining subcases exercised during this initial session (WEB create, non-Manager WEB access, changed intent/double submit). It is not a full-suite retest. Its create subcase requires the console `booking` fixture action. Customer credential login is explicitly rejected by current AuthService; do not fabricate an auth session to hide this coverage limitation.

Raw reports, auth state, profiles, downloaded browser/tooling, credentials, app configuration and logs are temporary. Preserve only sanitized summaries/screenshots. `cleanup.ps1` verifies manifest/process identity and the SQL ownership property, drops only the owned random database, verifies absence and removes only this run's disposable directories. Clear MCP storage/cookies and close the browser before cleanup. Stop on any ownership ambiguity. Preserve test sources and sanitized evidence. Never run cleanup against shared/business data.

No commit, push, production fix, or subsequent retest is authorized by these instructions.
