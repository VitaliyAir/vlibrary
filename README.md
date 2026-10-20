# VLibrary

Personal virtual library: imports PDF / EPUB / DjVu / DOC / DOCX / TXT files, detects duplicates, keeps a catalog with
authors, tags, classification, categories, shelves, ratings and reading status, full-text search, notes and quotes
with page numbers, and spaced-repetition reminders of notes. Use cases: `virtual_library_use_cases.pdf`.

The book files themselves are stored in **VStorageAPI** (bucket `vlibrary-books`); VLibrary keeps only their meta info
and fetches a file from VStorage when you open it.

## Stack

| Part | Technology |
|---|---|
| API | .NET 10 Minimal API, MediatR (CQRS), FluentValidation, Result pattern → ProblemDetails |
| Data | PostgreSQL 18 (Docker), EF Core 10 + Npgsql, migrations, full-text search (`russian` config) + `pg_trgm` |
| Metadata | PdfPig (PDF), EPUB package (OPF), optional DjVuLibre `djvused`, file-name fallback |
| UI | React 19 + Vite 6 + TypeScript, TanStack Query, React Router (`VLibrary.UIReact/`) |
| Tests | xUnit v3, AwesomeAssertions, NSubstitute, Testcontainers (PostgreSQL) |

```
VLibrary.Domain          entities, rules (ISBN, names, Leitner), Result/Error
VLibrary.Application     Features/<Feature>: commands, queries, handlers, validators; behaviors
VLibrary.Infrastructure  EF Core (DbContext, configurations, migrations), metadata readers, import worker
VLibrary.Api             Minimal API endpoints (/api/v1), OpenAPI + Scalar, health checks
tests/                   Domain, Application and API integration tests
VLibrary.UIReact/        React UI
```

## First run

Ports (chosen not to clash with VGoalsPlanner: API 5016/7135, UI 5173):

| Part | Address |
|---|---|
| API | http://localhost:5080 |
| Scalar UI / OpenAPI | http://localhost:5080/scalar/v1 · http://localhost:5080/openapi/v1.json |
| UI (Vite dev server) | http://127.0.0.1:5175 |
| PostgreSQL | shared server localhost:5432, database `vlibrarydb` |

1. Database: the shared PostgreSQL on port 5432 (database `vlibrarydb` is created by the migrations).

2. API settings (once). Connection string name is `VLibraryDb`; the MediatR license key is free for personal
   use (register at https://mediatr.io). Prefer user-secrets over `appsettings.Development.json`:

   ```bash
   dotnet user-secrets set "ConnectionStrings:VLibraryDb" "Host=localhost;Port=5432;Database=vlibrarydb;Username=postgres;Password=<password>" --project VLibrary.Api
   dotnet user-secrets set "MediatR:LicenseKey" "<key>" --project VLibrary.Api
   ```

3. API (applies migrations on start, opens the Scalar UI):

   ```bash
   dotnet run --project VLibrary.Api
   ```

4. UI (http://127.0.0.1:5175, proxies `/api` to the API):

   ```bash
   cd VLibrary.UIReact && npm install && npm run dev
   ```

## Configuration (`VLibrary.Api/appsettings.json`)

- `OpenApi:Enabled` — serve `/openapi/v1.json` and the Scalar UI `/scalar/v1` (default `true`).
- `Database:ApplyMigrationsOnStartup` — apply pending migrations at start (default `true`).
- `Cors:AllowedOrigins` — origins allowed to call the API (the Vite dev server by default).
- `Import:MaxFileBytes` — largest file one upload may carry (default 1 GB, the VStorage limit).
- `Import:TempFolder` — where an upload is kept while it is imported (default `{system temp}\vlibrary-import`).
- `Import:ExcludedFolders` — folder names the UI never uploads (default `node_modules`, `.git`, `.svn`, `.vs`, `bin`, `obj`).
- `Metadata:DjvusedPath` — full path to `djvused` for DjVu metadata; empty = file name only.
- `Metadata:DdjvuPath` — full path to `ddjvu` for DjVu covers; empty = `ddjvu` next to `djvused`, if it exists.
- `Covers:Width` / `Covers:MaxHeight` / `Covers:JpegQuality` — cover thumbnail size and JPEG quality (300, 450, 80).
- `VStorage:*` — see [Book files in VStorage](#book-files-in-vstorage).

### Importing, categories

Import → **Choose folder…** picks a folder on your computer (or **Choose files…** single files). The browser only knows
the path from the picked folder down, e.g. picking `D:\Templ` gives `Templ/Library/Math/a.pdf`. Click the folder chip
where the categories start (`Library`) and optionally choose an existing **parent category**: the files go to the
categories `<parent> / Library / Math` (subfolders become subcategories), and the VStorage key of a file is its
category path + file name (`Library/Math/a.pdf`). Category names are compared ignoring case.

The browser uploads one file at a time: VLibrary hashes it, reads its metadata, stores **new** content in VStorage and
keeps the meta info (category, book, hash, size, metadata, VStorage object id). Per file:

- same category + name, same content → unchanged (only counted); a re-import of the same folder first sends a
  precheck (name, size, modification time), so unchanged files are not uploaded again;
- same category + name, other content → **Conflict**, skipped;
- same content already in the library elsewhere → **Already in library**, skipped (one stored object per content);
- same content as a deleted book or removed file → reported once ("same content as deleted book"), later only counted;
- new content → **Imported** (new book) or **Waiting for review** (looks like an existing book).

If VStorage is not reachable the upload pauses ("Retry" sends the same file again); nothing is recorded for that file.
An import left unfinished (tab closed, API restarted) is shown as cancelled ("interrupted").

Duplicate review (a new file that may be another copy of an existing book; exact rules, the best one wins):
same ISBN 100%, same title 100%, same title words in another order 90%, same title stored inside the file 90%,
same extension and file size 80%; anything else is imported as a new book. Titles are compared ignoring case,
ё/е, punctuation and spaces ("Кунгуру 3-4_2012" = "Кунгуру 3-4 2012"). A bracketed year in a file name
("(2009)") is not part of the title; a bare year is.

**Open** asks VStorage for a short-lived link (5 minutes) and opens it in a new tab: the file goes from VStorage
straight to the browser; the API records that the book was opened. Settings → Library → **Check files now**
lists the bucket and marks files whose object is gone as Missing; the UI colours books whose files cannot be opened.
Removing a book or file from the library keeps its VStorage object.

### Covers

Every PDF, EPUB and DjVu file gets a cover thumbnail (JPEG, 300 px wide) while it is imported: the first page of a
PDF (PDFium, included in the API), the cover image stored in an EPUB, the first page of a DjVu (needs DjVuLibre
`ddjvu`). The thumbnails are kept in `vlibrarydb` (table `file_covers`, a few tens of KB each); a book shows the cover
of its primary file. DOC, DOCX and TXT books show a placeholder (format + initials of the title).

Books imported before covers existed: Settings → Library → **Generate missing covers** (downloads those files from
VStorage, 5 per request, while you are signed in). **Retry files without cover** tries files again that had no
result, e.g. after installing DjVuLibre. API: `GET /api/v1/files/{id}/cover?v=…`, `GET /api/v1/covers/status`,
`POST /api/v1/covers/generate?take=5`, `POST /api/v1/covers/reset-failed`.

## Book files in VStorage

VLibrary calls VStorageAPI (http://localhost:5190) with **your** token: the API exchanges the access token of the
request for a VStorage token (Keycloak Standard Token Exchange), so imports, opening books and the check work only
while you are signed in. The bucket `vlibrary-books` is found by name or created on the first import.

One-time setup in the Keycloak admin console (realm VEco), in addition to the client `vlibrary` below:

1. **Clients → Create client** `vlibrary-api`: OpenID Connect, *Client authentication* **on** (confidential),
   *Standard flow* off, *Direct access grants* off, *Standard Token Exchange* **on**.
   **Client scopes → Add client scope** `vstorage-audience` as **Optional**. Copy the secret from **Credentials**.
2. **Clients → vlibrary → Client scopes → vlibrary-dedicated → Add mapper → By configuration → Audience**:
   *Included Client Audience* = `vlibrary-api`, *Add to access token* on (token exchange accepts only tokens issued
   for the exchanging client).
3. **Users → (you) → Role mapping → Assign role → Filter by clients** → `vstorage bucket-creator` (needed once, to
   create the bucket).

```bash
dotnet user-secrets set "VStorage:ClientSecret" "<secret of vlibrary-api>" --project VLibrary.Api
```

Settings (`VStorage` section): `BaseUrl` (Development: `http://localhost:5190`), `BucketName` (`vlibrary-books`),
`TokenEndpoint` (empty = from `Authentication:Authority`), `ClientId` (`vlibrary-api`), `Audience` (`vstorage`),
`Scope` (`vstorage-audience`), `DownloadLinkSeconds` (300), `Timeout`. Without `BaseUrl` / `ClientSecret` the API
still starts; importing, opening and the check answer 503 "VStorage is not reachable or not configured" (the log
shows Keycloak's or VStorage's answer).

## Authentication (Keycloak)

Users sign in through Keycloak (realm **VEco** on http://localhost:8083, shared with VGoalsPlanner). Only users with
the client role **Owner** of the client **vlibrary** can use the API and UI; a VGoalsPlanner role gives no access here.
`/health`, `/version`, `/name`, `/env`, `/openapi/v1.json` and `/scalar` are public.

One-time setup in the Keycloak admin console (realm VEco):

1. **Clients → Create client** `vlibrary`: OpenID Connect, *Client authentication* off (public client),
   *Standard flow* on, *Direct access grants* off; *Advanced → Proof Key for Code Exchange* = S256.
   - Valid redirect URIs: `http://127.0.0.1:5175/*`, `http://localhost:5175/*`, `http://localhost:5080/scalar/*`
   - Valid post logout redirect URIs: `http://127.0.0.1:5175/*`, `http://localhost:5175/*`
   - Web origins: `http://127.0.0.1:5175`, `http://localhost:5175`, `http://localhost:5080`
2. **Clients → vlibrary → Roles → Create role** `Owner`.
3. **Clients → vlibrary → Client scopes → vlibrary-dedicated → Add mapper → By configuration → Audience**:
   *Included Client Audience* = `vlibrary`, *Add to access token* on (the API only accepts tokens issued for `vlibrary`).
4. **Users → (you) → Role mapping → Assign role → Filter by clients** → `vlibrary Owner`.

Settings: `Authentication:Authority` (realm URL; Development: `http://localhost:8083/realms/VEco`),
`Authentication:ClientId` (`vlibrary`), `Authentication:RequiredRole` (`Owner`), `Authentication:RequireHttpsMetadata`
(off only for the local http Keycloak). UI: `VITE_OIDC_AUTHORITY`, `VITE_OIDC_CLIENT_ID` in `.env.development`.

- **UI**: not signed in → redirect to Keycloak; signed in without Owner → "no access" screen; user name and
  *Sign out* in the sidebar.
- **Scalar** (http://localhost:5080/scalar/v1): *Authorize* → scheme **Keycloak** signs you in (PKCE), or paste an
  access token into **Bearer**.
- **Audit log** (`action_logs.action_by_id`): the user name from the access token (`preferred_username`);
  work without a user request (marking interrupted imports at startup) is recorded as `system`.

## Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project VLibrary.Infrastructure --startup-project VLibrary.Api --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project VLibrary.Infrastructure --startup-project VLibrary.Api -o migrate.sql
```

## Tests

```bash
dotnet test
dotnet test --project tests/VLibrary.Domain.Tests
```

`global.json` opts into the Microsoft.Testing.Platform runner (xunit.v3 on the .NET 10 SDK), so `dotnet test` takes
`--project` / `--solution` instead of a bare path. The integration tests start a PostgreSQL container, so Docker must
be running.
