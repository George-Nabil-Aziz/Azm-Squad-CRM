# Story 01 — Project skeleton (.NET API + React) (Story: CRM-1)

## Prerequisites

- None. This is the first story. The repository has no application code yet.
- Work on branch **`feature/crm-1-project-skeleton`** (already created).

---

## Story Goal

Create the solution skeleton every later story builds on:

1. A **.NET 10** backend in `server/` with four layered projects (`Crm.Api` → `Crm.Application` → `Crm.Domain` ← `Crm.Infrastructure`) and two xUnit test projects, all in one solution so `dotnet test` from `server/` runs everything.
2. `GET /api/health` returns **200** with `{"status":"ok"}`.
3. A **React + Vite + TypeScript** app in `client/` with **Vitest + React Testing Library**; `npm test` runs once and passes.
4. The Vite dev server proxies `/api` to the API; the home page calls `/api/health` through a typed API client and shows **"ok"**.

**Not in scope:** auth (CRM-2), layout / Tailwind / shadcn/ui (CRM-3), i18n / RTL (CRM-4), ProblemDetails (CRM-5), database / EF Core, Docker, CI.

---

## Context — Read These Files First

1. `CLAUDE.md` — whole file (~85 lines). Rules you must follow: **Backend rules** (layers, Domain has no EF Core / ASP.NET dependency), **Frontend rules** (API calls only through `client/src/api`, test behavior with RTL queries by role/text), **TDD** (test first, see it fail), **Conventions** (commit message `CRM-1: <summary>`).
2. `.gitignore` — already ignores `bin/`, `obj/`, `node_modules/`, `dist/`, `coverage/`, `.env*`. **Do not** add duplicates; the Vite template also creates `client/.gitignore` — keep it.
3. `.squad/stories/foundation/CRM-1/intake.md` — acceptance criteria 1–4 and **Out of scope** list.
4. Existing top-level entries you must **not** move or delete: `.claude/`, `.squad/`, `.mcp.json`, `CLAUDE.md`, `README.md`, `skills-lock.json`, `STUDY-NOTES.md`.

Verified toolchain (checked while planning): .NET SDK **10.0.400**, Node **24**, `dotnet new sln` creates **`.slnx`** by default, `dotnet new webapi` (net10.0) generates a minimal-API `Program.cs` with a `/weatherforecast` demo and `Microsoft.AspNetCore.OpenApi` **10.0.11**, `dotnet new xunit` uses **xunit 2.9.3**, `npm create vite@latest -- --template react-ts` gives **Vite 8 / React 19 / TypeScript 6** with no test runner, Vitest **5.0.3** supports Vite 8.

---

## Backend Tasks

All commands run from the repo root unless stated.

### 1 — Create the solution and projects

```bash
mkdir server && cd server
dotnet new sln -n Crm
dotnet new webapi    -n Crm.Api            -o src/Crm.Api            -f net10.0 --no-https
dotnet new classlib  -n Crm.Application    -o src/Crm.Application    -f net10.0
dotnet new classlib  -n Crm.Domain         -o src/Crm.Domain         -f net10.0
dotnet new classlib  -n Crm.Infrastructure -o src/Crm.Infrastructure -f net10.0
dotnet new xunit     -n Crm.UnitTests            -o tests/Crm.UnitTests            -f net10.0
dotnet new xunit     -n Crm.Api.IntegrationTests -o tests/Crm.Api.IntegrationTests -f net10.0
dotnet sln Crm.slnx add src/Crm.Api src/Crm.Application src/Crm.Domain src/Crm.Infrastructure tests/Crm.UnitTests tests/Crm.Api.IntegrationTests
```

Delete template leftovers: `src/Crm.Application/Class1.cs`, `src/Crm.Domain/Class1.cs`, `src/Crm.Infrastructure/Class1.cs`, `tests/Crm.UnitTests/UnitTest1.cs`, `tests/Crm.Api.IntegrationTests/UnitTest1.cs`, `src/Crm.Api/Crm.Api.http`.

### 2 — Project references (layering)

Run from `server/`:

```bash
dotnet add src/Crm.Application    reference src/Crm.Domain
dotnet add src/Crm.Infrastructure reference src/Crm.Application src/Crm.Domain
dotnet add src/Crm.Api            reference src/Crm.Application src/Crm.Infrastructure
dotnet add tests/Crm.UnitTests            reference src/Crm.Domain src/Crm.Application
dotnet add tests/Crm.Api.IntegrationTests reference src/Crm.Api
dotnet add tests/Crm.Api.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing --version <same version as Microsoft.AspNetCore.OpenApi in src/Crm.Api/Crm.Api.csproj>
```

**Do not** add any reference from `Crm.Domain` to another project or package.

### 3 — Tests first (Red)

Write these two test files **before** tasks 4–5, run `dotnet test` from `server/`, and confirm they fail (compile error or 404 counts as Red).

**Create file: `server/tests/Crm.Api.IntegrationTests/HealthEndpointTests.cs`**

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Crm.Api.IntegrationTests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_Returns200WithStatusOk()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("ok", body?.Status);
    }

    private sealed record HealthBody(string Status);
}
```

**Create file: `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs`** — guards the `CLAUDE.md` rule "Domain has no dependency on EF Core or ASP.NET".

```csharp
namespace Crm.UnitTests.Architecture;

public class LayerDependencyTests
{
    private static readonly string[] ForbiddenPrefixes =
        ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Crm.Infrastructure", "Crm.Api"];

    [Fact]
    public void Domain_DoesNotReferenceFrameworkOrOuterLayers() =>
        AssertNoForbiddenReferences(typeof(Crm.Domain.AssemblyReference).Assembly);

    [Fact]
    public void Application_DoesNotReferenceAspNetCoreOrOuterLayers() =>
        AssertNoForbiddenReferences(typeof(Crm.Application.AssemblyReference).Assembly);

    private static void AssertNoForbiddenReferences(System.Reflection.Assembly assembly)
    {
        var offending = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => ForbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offending);
    }
}
```

### 4 — Assembly markers (Green for unit tests)

**Create file: `server/src/Crm.Domain/AssemblyReference.cs`**

```csharp
namespace Crm.Domain;

/// <summary>Marker type used to locate the Domain assembly (architecture tests, DI scanning).</summary>
public static class AssemblyReference;
```

**Create file: `server/src/Crm.Application/AssemblyReference.cs`** — same shape, namespace `Crm.Application`.

### 5 — Health endpoint (Green for integration test)

**Create file: `server/src/Crm.Api/Endpoints/HealthEndpoints.cs`**

```csharp
namespace Crm.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok")))
           .WithName("GetHealth");
        return app;
    }
}

public sealed record HealthResponse(string Status);
```

**File: `server/src/Crm.Api/Program.cs`** — replace the whole generated file (it only contains the `/weatherforecast` demo) with:

```csharp
using Crm.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();

app.Run();

// Exposes Program to WebApplicationFactory<Program> in Crm.Api.IntegrationTests.
public partial class Program;
```

ASP.NET Core serializes `HealthResponse` as `{"status":"ok"}` (camelCase by default) — no extra JSON config.

### 6 — Fixed local port

**File: `server/src/Crm.Api/Properties/launchSettings.json`** — the template picks a random port. In profile **`http`**, set `"applicationUrl": "http://localhost:5080"`. Leave the rest of the profile as generated. The Vite proxy (Frontend task 4) depends on this exact URL.

---

## Frontend Tasks

### 1 — Create the Vite app

From the repo root:

```bash
npm create -y vite@latest client -- --template react-ts
cd client
npm install
npm install -D vitest@^5 jsdom @testing-library/react @testing-library/jest-dom
```

Delete the template demo: `src/App.css`, the folder `src/assets/`. Keep `src/index.css`, `src/main.tsx`, `public/`, `client/.gitignore`, `.oxlintrc.json`.

### 2 — Test runner config

**File: `client/package.json`** — add to `"scripts"`:

```json
"test": "vitest run",
"test:watch": "vitest"
```

**Create file: `client/src/test/setup.ts`**

```ts
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

afterEach(() => {
  cleanup()
})
```

### 3 — Tests first (Red)

Write both test files before tasks 4–6; run `npm test` and confirm they fail.

**Create file: `client/src/api/health.test.ts`**

```ts
import { afterEach, describe, expect, it, vi } from 'vitest'
import { getHealth } from './health'

describe('getHealth', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('calls /api/health and returns the parsed body', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ status: 'ok' }), { status: 200 }),
    )
    vi.stubGlobal('fetch', fetchMock)

    await expect(getHealth()).resolves.toEqual({ status: 'ok' })
    expect(fetchMock).toHaveBeenCalledWith('/api/health', expect.anything())
  })

  it('throws when the API responds with an error status', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 503 })))

    await expect(getHealth()).rejects.toThrow('503')
  })
})
```

**Create file: `client/src/App.test.tsx`**

```tsx
import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { getHealth } from './api/health'

vi.mock('./api/health', () => ({ getHealth: vi.fn() }))

describe('App', () => {
  beforeEach(() => {
    vi.mocked(getHealth).mockReset()
  })

  it('shows "ok" when the API is healthy', async () => {
    vi.mocked(getHealth).mockResolvedValue({ status: 'ok' })
    render(<App />)
    expect(await screen.findByText('ok')).toBeInTheDocument()
  })

  it('shows "unavailable" when the API call fails', async () => {
    vi.mocked(getHealth).mockRejectedValue(new Error('503'))
    render(<App />)
    expect(await screen.findByText('unavailable')).toBeInTheDocument()
  })
})
```

### 4 — Vite proxy + test config

**File: `client/vite.config.ts`** — replace the generated file (it only registers `react()`):

```ts
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Must match applicationUrl of the "http" profile in server/src/Crm.Api/Properties/launchSettings.json
      '/api': 'http://localhost:5080',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
```

### 5 — Typed API client (Green for `health.test.ts`)

**Create file: `client/src/api/client.ts`** — the single place that calls `fetch` (rule from `CLAUDE.md`).

```ts
export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, { headers: { Accept: 'application/json' }, signal })
  if (!response.ok) {
    throw new Error(`GET ${path} failed with status ${response.status}`)
  }
  return (await response.json()) as T
}
```

**Create file: `client/src/api/health.ts`**

```ts
import { apiGet } from './client'

export interface HealthResponse {
  status: string
}

export function getHealth(signal?: AbortSignal): Promise<HealthResponse> {
  return apiGet<HealthResponse>('/api/health', signal)
}
```

### 6 — Home page (Green for `App.test.tsx`)

**File: `client/src/App.tsx`** — replace the whole generated demo with:

```tsx
import { useEffect, useState } from 'react'
import { getHealth } from './api/health'

type ApiState = 'loading' | 'unavailable' | string

function App() {
  const [apiStatus, setApiStatus] = useState<ApiState>('loading')

  useEffect(() => {
    const controller = new AbortController()
    getHealth(controller.signal)
      .then((health) => setApiStatus(health.status))
      .catch(() => {
        if (!controller.signal.aborted) setApiStatus('unavailable')
      })
    return () => controller.abort()
  }, [])

  // Temporary placeholder text: i18n arrives in CRM-4, layout in CRM-3.
  return (
    <main>
      <h1>Customer Support CRM</h1>
      <p>
        API status: <strong>{apiStatus}</strong>
      </p>
    </main>
  )
}

export default App
```

`client/src/main.tsx` needs **no changes** (it renders `App` and imports `index.css`).

---

## Docs Task

**File: `README.md`** — append a section after **Workflow**:

````markdown
## Run locally

```bash
# API (http://localhost:5080)
cd server
dotnet run --project src/Crm.Api --launch-profile http

# Client (http://localhost:5173, proxies /api to the API)
cd client
npm install
npm run dev
```

## Tests

```bash
cd server && dotnet test
cd client && npm test
```
````

---

## Edge Cases & Failure Modes

- **Port 5080 already in use** → `dotnet run` fails to bind. Change `applicationUrl` in `server/src/Crm.Api/Properties/launchSettings.json` **and** the proxy target in `client/vite.config.ts` together; they must always match.
- **API not running while the client runs** → the proxy returns an error status; `apiGet` throws and `App` shows **"unavailable"** (covered by `App.test.tsx` second test and `health.test.ts` second test).
- **Component unmounts before the request finishes** (React StrictMode double-mount in dev) → the `AbortController` in `App.tsx` aborts the request and the `catch` ignores the abort, so no state update after unmount and no false "unavailable".
- **`WebApplicationFactory<Program>` cannot find `Program`** → happens if `public partial class Program;` is missing from `Program.cs`; the integration test then fails to compile.
- **Domain or Application accidentally gains an ASP.NET / EF Core reference** in a later story → `LayerDependencyTests` fails. Note: `GetReferencedAssemblies()` only lists assemblies the code actually uses, so the guard catches real usage, not unused package references.
- **`npm create vite` prompts interactively** → the `-y` flag plus the explicit `--template react-ts` avoid prompts; if a prompt still appears (e.g. "directory not empty"), stop — `client/` must not exist before this task.
- **Windows line endings**: git warns `LF will be replaced by CRLF`; harmless, do not add a `.gitattributes` in this story.
- **Mismatched Mvc.Testing version** (e.g. an 11.x preview) → use exactly the `Microsoft.AspNetCore.OpenApi` version from `Crm.Api.csproj` so all ASP.NET packages stay on the same 10.0.x patch.

---

## Test Plan

1. **Integration** — `server/tests/Crm.Api.IntegrationTests/HealthEndpointTests.cs` → `GetHealth_Returns200WithStatusOk` (AC 1).
2. **Unit (architecture)** — `server/tests/Crm.UnitTests/Architecture/LayerDependencyTests.cs` → `Domain_DoesNotReferenceFrameworkOrOuterLayers`, `Application_DoesNotReferenceAspNetCoreOrOuterLayers` (AC 2, and enforces `CLAUDE.md` layering).
3. **Unit (frontend API client)** — `client/src/api/health.test.ts` → calls `/api/health` and parses body; throws on non-2xx (AC 4, client side).
4. **Component** — `client/src/App.test.tsx` → shows "ok" when healthy; shows "unavailable" on failure (AC 3, AC 4).
5. **Manual smoke** — run API + client together and open the home page (Verification step 5) — proves the proxy (AC 4) end to end.

No existing tests to modify or remove (first story).

---

## Verification Steps

1. **Backend builds:** in `server/` run `dotnet build` — 0 errors, 0 warnings from our code.
2. **Backend tests:** in `server/` run `dotnet test` — **3 tests passed** (1 integration, 2 unit), 0 failed.
3. **Frontend tests:** in `client/` run `npm test` — **4 tests passed**, process exits (not watch mode).
4. **Frontend builds:** in `client/` run `npm run build` — `tsc -b` and `vite build` succeed.
5. **End to end:** terminal 1: `cd server && dotnet run --project src/Crm.Api --launch-profile http`; terminal 2: `cd client && npm run dev`; open `http://localhost:5173` → page shows **"API status: ok"**. Also `curl http://localhost:5080/api/health` → `{"status":"ok"}`.
6. **Regression:** `git status` shows no changes to `.claude/`, `.squad/` (except this plan's overview/index), `.mcp.json`, `CLAUDE.md`.

---

## Done Criteria

- [ ] `GET /api/health` returns 200 with `{"status":"ok"}` (integration test green).
- [ ] `dotnet test` in `server/` runs xUnit projects with passing tests.
- [ ] `npm test` in `client/` runs Vitest with passing tests and exits.
- [ ] Home page calls `/api/health` through `client/src/api/health.ts` and shows "ok" via the Vite proxy.
- [ ] `server/` has `Crm.Api`, `Crm.Application`, `Crm.Domain`, `Crm.Infrastructure` with references Api→Application+Infrastructure, Infrastructure→Application+Domain, Application→Domain, Domain→nothing.
- [ ] Template demo code removed (`/weatherforecast`, `Class1.cs`, `UnitTest1.cs`, `App.css`, `src/assets/`).
- [ ] `README.md` has **Run locally** and **Tests** sections.
- [ ] Committed on `feature/crm-1-project-skeleton` with message `CRM-1: project skeleton (.NET API + React)`.
- [ ] Overview `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 02.**
