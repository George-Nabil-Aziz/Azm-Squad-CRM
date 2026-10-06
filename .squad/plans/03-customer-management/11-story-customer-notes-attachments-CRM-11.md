# Story 11 — Customer notes & attachments (Story: CRM-11)

## Prerequisites

- Story 10 completed: [10-story-customer-timeline-CRM-10.md](10-story-customer-timeline-CRM-10.md) (CRM-10, branch `feature/group-a-customers`). Its "5 — How later stories build on this" is **binding**: notes record `InteractionType.Note` + `noteAdded` (details = excerpt, sourceId = note id), attachments `InteractionType.Attachment` + `attachmentAdded` (details = file name, sourceId = attachment id), recorded **before** `SaveChangesAsync`; new event codes get a label in `customers.timeline.events` and in `knownEvents` of `CustomerTimeline.tsx`.
- CRM-8 child-entity rules ([08-story-customer-profiles-CRM-8.md](08-story-customer-profiles-CRM-8.md) line 1576): FK `CustomerId` `Restrict`, own configuration class, routes under `/api/customers/{id:guid}/…`, deleted customer → 404 via `ICustomerRepository.FindAsync`. CRM-7 catalogue ([../02-security-admin/07-story-roles-permissions-CRM-7.md](../02-security-admin/07-story-roles-permissions-CRM-7.md) lines 46–47): reads / downloads `customers.view`, add note / upload `customers.manage`.
- No new NuGet / npm package. **One new shadcn component:** `textarea` (`npx shadcn@4.21.2 add textarea -y`, CRM-3 rule). One migration: **`AddCustomerNotesAndAttachments`**.

---

## Story Goal

Agents keep notes and files about a customer on the customer page.

1. `POST /api/customers/{id}/notes` `{ "text": "…" }` → **201** + the note **with author name and UTC time** (AC 1); `GET /api/customers/{id}/notes?page=&pageSize=` → newest first. Empty text → 400 `errors.text`; longer than 4000 → 400.
2. `POST /api/customers/{id}/attachments` (multipart, field `file`) with an allowed type up to **10 MB (10 485 760 bytes, inclusive)** → **201** + metadata; `GET /api/customers/{id}/attachments` lists them; `GET /api/customers/{id}/attachments/{attachmentId}` downloads the exact bytes as `Content-Disposition: attachment` with the original name (AC 2).
3. A file over 10 MB → **400** `errors.file` "The file is larger than 10 MB." (AC 3).
4. A disallowed type (`.exe`, `.bat`, `.js`, `invoice.pdf.exe`, no extension) → **400** `errors.file` (AC 4). Allowed: `.pdf .png .jpg .jpeg .gif .webp .txt .csv .doc .docx .xls .xlsx` (case-insensitive).
5. Notes and uploads appear in the CRM-10 timeline (types `note`, `attachment`).
6. Client: on the customer details page a **Notes** section (textarea + "Add note", list with author and time, paging) and an **Attachments** section (file picker + "Upload", the same size / type checks before calling the API, table with name, size, uploaded by, date and a **Download** button). Add / upload only with `customers.manage`.

**Decisions**

- **`IFileStorage` (Application, `Crm.Application/Common/Files`)**: `SaveAsync(key, stream)`, `OpenReadAsync(key)` (null when missing), `DeleteAsync(key)`. **`LocalFileStorage` (Infrastructure)** writes under `FileStorage:RootPath`; when not configured: `%LOCALAPPDATA%/AzmSquadCrm/files` (outside the repository). Read lazily from `IConfiguration` (like the connection string) so the test host can point it at a temp directory. Later: Azure Blob / S3 implementations of the same interface; ticket attachments reuse it.
- **Storage key never contains user input:** `customers/{customerId:N}/{attachmentId:N}`; `LocalFileStorage` additionally rejects keys outside `[a-z0-9/]` and paths that leave the root. The original name is only metadata (sanitized with `Path.GetFileName`, control characters removed, max 255).
- **Type check by extension allow-list, content type from the server map** (`AttachmentRules`), never the client's `Content-Type`. Downloads are `attachment` (never rendered inline) and need `customers.view`. Magic-byte sniffing / antivirus are out of scope.
- **Size check in the Application validator** (`Length > 10 MB` → 400). Kestrel's default request limit (~28.6 MB) still answers 413 for much larger bodies before the endpoint runs — documented, acceptable.
- **Upload order:** validate → customer exists → save the file → add the row + timeline entry → `SaveChangesAsync`; if saving the row fails, delete the file (best effort) and rethrow.
- **Authors** are stored as user ids (`AuthorId`, `UploadedById`); names come from a left join with the Identity users in the Infrastructure queries (like CRM-10). The add endpoints re-read the saved row to return the author name.
- **Notes and attachments are separate entities** (`CustomerNotes`, `CustomerAttachments`) with their own Application services (`ICustomerNoteService`, `ICustomerAttachmentService`); no delete / edit in this story.
- **Antiforgery:** the upload endpoint uses `.DisableAntiforgery()` — the API authenticates with a bearer header, not cookies, so CSRF does not apply (minimal APIs with `IFormFile` otherwise require the antiforgery middleware).

**Not in scope:** editing / deleting notes or files, rich text, antivirus, previews, cloud storage, ticket attachments.

---

## Context — Read These Files First

1. `CLAUDE.md` lines 41–51, 63–80.
2. `.squad/stories/03-customer-management/CRM-11/intake.md`.
3. [10-story-customer-timeline-CRM-10.md](10-story-customer-timeline-CRM-10.md) "Decisions" and "5 — How later stories build on this".
4. `server/src/Crm.Application/Customers/Timeline/IInteractionRecorder.cs`, `CustomerTimelineService.cs` (validate → `FindAsync` → repository pattern to copy), `server/src/Crm.Domain/Customers/InteractionEvents.cs` (add codes), `CustomerInteraction.cs` (entity style).
5. `server/src/Crm.Infrastructure/Customers/CustomerTimelineRepository.cs` (left join to `db.Users` for names), `Persistence/Configurations/CustomerInteractionConfiguration.cs` (FK without navigation), `DependencyInjection.cs` lines 21–25 (lazy `IConfiguration` read).
6. `server/src/Crm.Api/Endpoints/CustomersEndpoints.cs` (timeline route as precedent; write routes add `RequireAuthorization(Permissions.CustomersManage)`).
7. `server/src/Crm.Api/ErrorHandling/GlobalExceptionHandler.cs` lines 55–70 (`BadHttpRequestException` → its status, e.g. 415 for a non-form body).
8. Tests: `server/tests/Crm.UnitTests/Customers/TimelineTestDoubles.cs`, `CustomerTimelineServiceTests.cs` (`OneCustomerRepository`), `server/tests/Crm.Api.IntegrationTests/Infrastructure/CrmApiFactory.cs` lines 48–53 (config values), 110–117 (`Dispose`), `Customers/CustomerTimelineTests.cs` (helpers), `CustomersAuthorizationTests.cs` (rows + policy map, count 10).
9. Client: `client/src/api/client.ts` lines 40–93 (`request` — JSON only today), `client/src/api/customers.ts`, `client/src/pages/customers/CustomerDetailsPage.tsx`, `client/src/features/customers/CustomerTimeline.tsx` (`knownEvents`), `AddContactForm.tsx` (react-hook-form + zod + server field errors pattern), `client/src/pages/customers/CustomerDetailsPage.test.tsx` (mock list of `@/api/customers`).

---

## Backend Tasks

### 1 — Unit tests first (Red)

- `server/tests/Crm.UnitTests/Customers/CustomerNoteTests.cs` (Domain): trims text; empty / too long / non-UTC throw.
- `.../CustomerAttachmentTests.cs` (Domain): storage key = `customers/{customerId:N}/{id:N}`; empty name / size ≤ 0 throw.
- `.../AttachmentRulesTests.cs`: allowed extensions → content type (case-insensitive); `.exe`, `.bat`, `.js`, `invoice.pdf.exe`, no extension → not allowed; `SafeFileName` strips folders and control characters.
- `.../UploadAttachmentValidatorTests.cs`: exactly 10 MB valid; 10 MB + 1 → `file` (AC 3); `.exe` → `file` (AC 4); no file / empty file → `file`; Arabic message.
- `.../CustomerNoteServiceTests.cs`: `Add_SavesTheNote_WithTheCurrentUserAndTime_AndRecordsNoteAdded` (AC 1), `Add_WithEmptyText_ThrowsValidationException_AndSavesNothing`, `Add_ToUnknownCustomer_ThrowsNotFound`.
- `.../CustomerAttachmentServiceTests.cs`: `Upload_StoresTheFile_SavesTheRow_AndRecordsAttachmentAdded` (AC 2), `Upload_TooLarge_ThrowsValidationException_AndStoresNothing` (AC 3), `Upload_DisallowedType_ThrowsValidationException` (AC 4), `Download_ReturnsTheStoredFile` (AC 2), `Download_OfAnotherCustomersAttachment_ThrowsNotFound`.

### 2 — Domain + Application (Green)

- Domain: `CustomerNote` (`Guid Id`, `CustomerId`, `Text` ≤ 4000, `AuthorId?`, `CreatedAt`), `CustomerAttachment` (`Guid Id`, `CustomerId`, `FileName` ≤ 255, `ContentType` ≤ 100, `long Size`, `StorageKey` ≤ 200, `UploadedById?`, `UploadedAt`); `InteractionEvents.NoteAdded = "noteAdded"`, `AttachmentAdded = "attachmentAdded"`.
- `Crm.Application/Common/Files/IFileStorage.cs`.
- `Crm.Application/Customers/Notes/`: `CustomerNoteContracts.cs` (`CustomerNoteRequest(string? Text)`, `ListCustomerNotesQuery(int? Page, int? PageSize)`, `CustomerNoteResponse(Guid Id, string Text, Guid? AuthorId, string? AuthorName, DateTime CreatedAt)`), validators, `ICustomerNoteRepository` (`Add`, `GetAsync(noteId)`, `ListAsync(customerId, page, pageSize)`, `SaveChangesAsync`), `ICustomerNoteService` / `CustomerNoteService`.
- `Crm.Application/Customers/Attachments/`: `AttachmentRules` (`MaxSizeBytes = 10 * 1024 * 1024`, `TryGetContentType`, `SafeFileName`), `CustomerAttachmentContracts.cs` (`UploadAttachmentRequest(string? FileName, long Length, Stream? Content)`, `CustomerAttachmentResponse(Guid Id, string FileName, string ContentType, long Size, Guid? UploadedById, string? UploadedByName, DateTime UploadedAt)`, `AttachmentDownload(Stream Content, string ContentType, string FileName)`), `UploadAttachmentRequestValidator` (all errors on `file`), `ICustomerAttachmentRepository` (`Add`, `FindAsync(customerId, attachmentId)`, `GetAsync(attachmentId)`, `ListAsync(customerId)`, `SaveChangesAsync`), `ICustomerAttachmentService` / `CustomerAttachmentService`.
- `CustomerText`: `NoteTextField`, `AttachmentFileField`, `AttachmentRequired`, `AttachmentEmpty`, `AttachmentTooLarge`, `AttachmentTypeNotAllowed`, `AttachmentNotFound` (en + ar).
- `Crm.Application/DependencyInjection.cs`: register both services.

### 3 — Integration tests (Red)

- `server/tests/Crm.Api.IntegrationTests/Customers/CustomerNotesTests.cs`: `AddNote_ShowsItWithAuthorAndTime` (AC 1, also in the list and in the timeline as `noteAdded`), `Notes_AreListedNewestFirst_AndPaginated`, `AddNote_WithEmptyOrTooLongText_Returns400` ×2, `Notes_OfUnknownOrDeletedCustomer_Return404`.
- `.../CustomerAttachmentsTests.cs`: `Upload_AllowedFile_IsSaved_AndDownloadable` ×3 (`report.pdf`, `photo.PNG`, `contract.docx`; AC 2: bytes, content type, `attachment; filename=…`, list, timeline `attachmentAdded`), `Upload_Exactly10MB_IsAccepted` (AC 2), `Upload_Over10MB_Returns400` (AC 3), `Upload_DisallowedType_Returns400` ×4 (AC 4), `Upload_WithoutFile_Returns400`, `Download_UnknownOrOtherCustomersAttachment_Returns404`, `Upload_StoresTheFileUnderTheConfiguredRoot` (file exists under the temp root, name not used in the path).
- `CrmApiFactory.cs`: `FileStorage:RootPath` = a per-factory temp directory (`Path.GetTempPath()/crm-tests/<guid>`), deleted in `Dispose`.
- `CustomersAuthorizationTests.cs`: 5 rows (`GET/POST …/notes`, `GET/POST …/attachments`, `GET …/attachments/{guid}`), policy map count 15.

### 4 — Infrastructure + Api (Green)

- `Crm.Infrastructure/Files/LocalFileStorage.cs` (root from config / default, key check, `FileMode.CreateNew`, async copy).
- `Persistence/Configurations/CustomerNoteConfiguration.cs`, `CustomerAttachmentConfiguration.cs` (FK `Restrict`, no navigation, index `(CustomerId, CreatedAt)` / `(CustomerId, UploadedAt)`); `DbSet`s in `CrmDbContext`.
- `Crm.Infrastructure/Customers/CustomerNoteRepository.cs`, `CustomerAttachmentRepository.cs` (left join for names).
- `DependencyInjection.cs`: repositories + `AddSingleton<IFileStorage>(sp => new LocalFileStorage(sp.GetRequiredService<IConfiguration>()["FileStorage:RootPath"]))`.
- `CustomersEndpoints.cs`: `GET/POST /{id:guid}/notes`, `GET/POST /{id:guid}/attachments` (POST: `IFormFile? file`, `.DisableAntiforgery()`, maps to `UploadAttachmentRequest(file?.FileName, file?.Length ?? 0, file?.OpenReadStream())`), `GET /{id:guid}/attachments/{attachmentId:guid}` → `Results.File(stream, contentType, fileName)`.
- Migration `AddCustomerNotesAndAttachments` (generated, not edited).
- `appsettings.json`: **no** path committed (default applies); document `FileStorage:RootPath` in the plan only.

### 5 — How later stories build on this (write nothing here; for later planners)

- **Ticket / message attachments (CRM-15, CRM-23..26):** reuse `IFileStorage` (keys `tickets/{ticketId:N}/{attachmentId:N}`), `AttachmentRules` (size + allow-list) and the download pattern (`Results.File` behind a permission). Email attachments from IMAP go through the same rules (skip or reject others).
- **Cloud storage:** add another `IFileStorage` implementation and choose it in `AddInfrastructure`; keys stay the same.
- **Deleting notes / files** (later story): soft delete (`ISoftDeletable` + `SoftDelete` filter) for notes; files: delete the row then `IFileStorage.DeleteAsync`.

---

## Frontend Tasks

### 1 — Tests first (Red)

- `client/src/api/client.test.ts` / `customers.test.ts`: `uploadCustomerAttachment` sends `FormData` (no JSON content type), `downloadCustomerAttachment` returns a `Blob`, `addCustomerNote` / `listCustomerNotes` / `listCustomerAttachments` paths.
- `client/src/pages/customers/CustomerNotesAttachments.test.tsx` (details page): `adds a note and shows it with author and time` (AC 1), `does not send an empty note`, `uploads an allowed file and lists it` (AC 2), `downloads an attachment` (AC 2), `rejects a file over 10 MB before uploading` (AC 3), `rejects a disallowed file type before uploading` (AC 4), `shows the server message when the upload is refused`, `hides note and upload forms from a user who may only view`.
- `CustomerDetailsPage.test.tsx`: add the new API functions to the mock; timeline shows `noteAdded` / `attachmentAdded` labels.

### 2 — Implementation (Green)

- `npx shadcn@4.21.2 add textarea -y`.
- `client/src/api/client.ts`: `request` accepts a `FormData` body (no `Content-Type`, the browser sets the boundary) and a `blob` response; exports `apiPostForm<T>(path, form)` and `apiGetBlob(path)`.
- `client/src/api/customers.ts`: `CustomerNote`, `CustomerAttachment` types; `listCustomerNotes`, `addCustomerNote`, `listCustomerAttachments`, `uploadCustomerAttachment`, `downloadCustomerAttachment`.
- `client/src/features/customers/attachment-rules.ts`: `MAX_ATTACHMENT_BYTES`, `ALLOWED_ATTACHMENT_EXTENSIONS` (same list as the server), `checkAttachment(file)`.
- `client/src/lib/save-file.ts`: `saveFile(blob, fileName)` (object URL + temporary `<a download>`).
- `client/src/features/customers/useCustomerNotes.ts`, `useCustomerAttachments.ts` (keys under `['customers']`).
- `CustomerNotes.tsx` (form inside `<Can customersManage>`, list with author / time, pager), `CustomerAttachments.tsx` (upload form inside `<Can>`, table, Download button).
- `CustomerDetailsPage.tsx`: render both sections between contacts and timeline; `CustomerTimeline.tsx` `knownEvents` + `noteAdded`, `attachmentAdded`.
- `en.json` / `ar.json`: `customers.notes.*`, `customers.attachments.*`, `customers.timeline.events.noteAdded|attachmentAdded`.

---

## Edge Cases & Failure Modes

- **Exactly 10 MB** → accepted; **10 MB + 1 byte** → 400 `errors.file` (`UploadAttachmentRequestValidator`). Bodies above Kestrel's ~28.6 MB default → 413 from the server before the endpoint (not reachable in TestServer).
- **Disguised types** (`invoice.pdf.exe`, `.EXE`, no extension, `.js`) → 400; content type always from `AttachmentRules`, never the browser.
- **Path tricks in the name** (`..\..\x.pdf`, `C:\temp\x.pdf`) → only `x.pdf` kept as metadata; the storage key never uses the name; `LocalFileStorage` refuses keys with other characters or paths outside the root.
- **Missing file field / empty file** → 400 `errors.file`; **JSON body** instead of multipart → 415 (`BadHttpRequestException`).
- **Database save fails after the file is written** → file deleted (best effort), exception → 500.
- **File missing on disk** (removed by hand) → download 404 `AttachmentNotFound`.
- **Attachment of another customer** in the URL → 404 (repository filters by both ids).
- **Unknown / deleted customer** → 404 on every route.
- **Empty / whitespace / > 4000-character note** → 400 `errors.text`.
- **Arabic / Unicode file names** → kept; `Results.File` writes `filename*=UTF-8''…`.
- **Permissions** → add note / upload need `customers.manage`; list / download `customers.view` (401 / 403 rows).
- **Client** → size / type checked before the request (no wasted upload); server message shown under the file input on 400; Download uses the authorized API client (no public URL).

---

## Test Plan

1. Unit (new): `CustomerNoteTests`, `CustomerAttachmentTests`, `AttachmentRulesTests`, `UploadAttachmentValidatorTests`, `CustomerNoteServiceTests`, `CustomerAttachmentServiceTests` (fakes: `FakeFileStorage`, note / attachment repositories in `TimelineTestDoubles.cs` or the test class).
2. Integration (new): `CustomerNotesTests`, `CustomerAttachmentsTests`; (modified) `CustomersAuthorizationTests` (+10 rows, map 15), `CrmApiFactory` (temp root).
3. Guards unchanged: `PermissionPolicyTests` (new routes; `{}` JSON to the upload → 415, never 401/403), `SoftDeleteModelTests`, `LayerDependencyTests` (Application keeps no ASP.NET reference: `IFormFile` stays in Api), `LocalizedTextCatalogTests` (new `CustomerText` rows).
4. Client: `customers.test.ts` (+5), `client.test.ts` (+2), `CustomerNotesAttachments.test.tsx` (8), `CustomerDetailsPage.test.tsx` (mocks); guards `translations`, `no-hardcoded-text`, `theme`.

---

## Migration / Rollback

- `AddCustomerNotesAndAttachments`: tables `CustomerNotes`, `CustomerAttachments` (FK → `Customers` NO ACTION, indexes on `(CustomerId, CreatedAt)` / `(CustomerId, UploadedAt)`).
- Rollback: `dotnet ef database update AddCustomerInteractions …` drops both tables; stored files under the root stay and can be deleted by hand.

---

## Verification Steps

1. **Backend builds:** `server/`: `dotnet build` — 0 warnings, 0 errors.
2. **Backend tests:** `server/`: `dotnet test` — all green.
3. **Frontend:** `client/`: `npm test`, `npm run build`, `npm run lint` — green.
4. **Regression:** no new file under the repository from tests (`git status` clean apart from the story's files); `client/src/api/client.ts` remains the only `fetch` caller.

---

## Done Criteria

- [ ] Adding a note shows it with author and time (`AddNote_ShowsItWithAuthorAndTime`; UI `adds a note and shows it with author and time`) — AC 1.
- [ ] Allowed file ≤ 10 MB saved and downloadable (`Upload_AllowedFile_IsSaved_AndDownloadable`, `Upload_Exactly10MB_IsAccepted`; UI upload + download) — AC 2.
- [ ] File over 10 MB → 400 (`Upload_Over10MB_Returns400`; validator + UI check) — AC 3.
- [ ] Disallowed type → 400 (`Upload_DisallowedType_Returns400`; rules + UI check) — AC 4.
- [ ] Notes and uploads in the timeline; permissions enforced on the API; all texts in `CustomerText` and both i18n files.
- [ ] Build / tests / lint green; committed as `CRM-11: customer notes and attachments`.
