# Story 24 — Email: incoming email creates ticket (Story: CRM-24)

## Prerequisites

- Story 23 completed: [23-story-email-send-replies-CRM-23.md](23-story-email-send-replies-CRM-23.md) (`ChannelKind`, `TicketNumberTag`, `EmailChannelOptions`, `ChannelText`, `ChannelWorker`, MailKit package).
- CRM-9 section 6 ([../03-customer-management/09-story-customer-contacts-CRM-9.md](../03-customer-management/09-story-customer-contacts-CRM-9.md) ~line 2370) is **binding**: match the sender with `ICustomerService.LookupAsync(new CustomerLookupQuery(null, address))`; it throws `ValidationException` for an unparseable value (treat as unknown); several matches → take the first (ordered by name); 0 matches → `ICustomerService.CreateAsync`.
- CRM-10 section 5: `InteractionType.Message` + `messageReceived` (ActorId null — channel events have no user).
- **Phase 2 only:** CRM-13 (`Ticket.Create(..., TicketChannel.Email, createdById: null, ...)`, `ITicketRepository.AddAsync`, `Ticket.FormatNumber`) and CRM-15 (ticket messages) on `main`.
- One migration: **`AddReceivedMessages`**.

---

## Story Goal

Every email that reaches the support mailbox is kept and becomes (or continues) a ticket.

1. **[P1]** `EmailInboxPoller.PollAsync` reads unseen messages from the IMAP inbox (`IImapMailbox` wrapper over MailKit `ImapClient`), parses each with `EmailMessageParser` (MimeKit) into a channel-neutral **`InboundChannelMessage`** (`Channel`, `ExternalId` = Message-Id, `From` (lower-case address), `FromName`, `Subject`, `Body` (text part, else HTML stripped to text), `ReceivedAt` UTC), hands it to `IInboundMessageProcessor`, then marks it seen. The poller runs from `ChannelWorker` when IMAP is configured; not in `Testing`.
2. **[P1]** `InboundMessageProcessor.ProcessAsync` — a message whose `(Channel, ExternalId)` was already stored is **ignored** (AC 4; unique index as the race guard); the sender is matched to a customer (known → that customer, AC 1) or a **new customer** is created with the sender's name (or address) and email (AC 2); the subject's `[TKT-xxxxxx]` number is extracted (AC 3); the message is stored as a **`ReceivedMessage`** with `CustomerId` and `TicketNumber`.
3. **[P2]** The processor then creates a ticket (`Channel = Email`, subject = email subject without the tag, description = body, customer from step 2) — or, when `TicketNumber` names an existing ticket, appends the email as a customer message to it (AC 3) — sets `ReceivedMessage.TicketId`, and records `messageReceived` / `ticketCreated` on the timeline.

**Not in scope:** inbound attachments, quoted-reply stripping, spam filtering, multiple mailboxes.

---

## Context — Read These Files First

1. `.squad/plans/06-channels/00-overview.md`, [23-story-email-send-replies-CRM-23.md](23-story-email-send-replies-CRM-23.md).
2. `server/src/Crm.Application/Customers/ICustomerService.cs` (`CreateAsync`, `AddContactAsync`, `LookupAsync`), `ContactValues.cs` (`TryNormalizePhone`), `CustomerContracts.cs` (`CustomerRequest`, `CustomerLookupQuery`).
3. `server/src/Crm.Application/Common/Exceptions/ValidationException.cs`.
4. `server/tests/Crm.UnitTests/Customers/TimelineTestDoubles.cs` (fake style).

---

## Backend Tasks

### 1 — Unit tests first (Red) [P1]

`server/tests/Crm.UnitTests/Channels/`:
- `ReceivedMessageTests` — `Create_SetsEveryField_AndCutsLongBodies`, `Create_WithoutExternalId_Throws`.
- `InboundMessageProcessorTests` (fakes: `FakeCustomerService`, `FakeReceivedMessageRepository`) — `KnownSender_IsLinkedToThatCustomer` (AC 1), `UnknownSender_CreatesANewCustomer_WithNameAndEmail` (AC 2), `SubjectWithTicketTag_StoresTheTicketNumber` (AC 3), `SameMessageIdTwice_IsIgnored` (AC 4), `SeveralMatches_TakesTheFirst`, `UnparseableSender_IsStoredWithoutCustomer`.

### 2 — Domain + Application (Green) [P1]

- **Create** `server/src/Crm.Domain/Channels/ReceivedMessage.cs`: `Guid Id`, `ChannelKind Channel`, `string ExternalId` (max 300), `string From` (max 320), `string? FromName` (200), `string? Subject` (300), `string Body` (max 10 000, cut), `int? TicketNumber`, `Guid? CustomerId`, `DateTime ReceivedAt`, `DateTime CreatedAt`; `Create(...)`, `LinkCustomer(Guid)`.
- **Create** `server/src/Crm.Application/Channels/`: `InboundChannelMessage` record, `InboundResult(bool Duplicate, Guid? ReceivedMessageId, Guid? CustomerId, bool NewCustomer, int? TicketNumber)`, `IReceivedMessageRepository` (`ExistsAsync(channel, externalId)`, `Add`, `LastReceivedAtAsync(channel, from)` for CRM-25, `SaveChangesAsync` → returns false on a duplicate-key race), `IInboundMessageProcessor` + `InboundMessageProcessor` (email: lookup by email; WhatsApp: lookup by phone — CRM-26).

### 3 — Infrastructure (Green) [P1]

- **Create** `server/src/Crm.Infrastructure/Channels/Email/EmailMessageParser.cs` (`InboundChannelMessage Parse(MimeMessage, DateTime receivedAtUtc)`; missing Message-Id → `sha256(from|date|subject)` hex as external id), `IImapMailbox.cs` + `MailKitImapMailbox.cs` (`FetchUnseenAsync`, `MarkSeenAsync`), `EmailInboxPoller.cs` (`PollAsync(ct)` → count processed; a message that fails to process stays unseen and is logged).
- **Create** `Persistence/Configurations/ReceivedMessageConfiguration.cs` (table `ReceivedMessages`, **unique** `(Channel, ExternalId)`, index `(Channel, From, ReceivedAt)`, FK `CustomerId` → `Customers` `Restrict`, no navigation), `DbSet<ReceivedMessage> ReceivedMessages`, `Channels/ReceivedMessageRepository.cs` (`SaveChangesAsync` catches `DbUpdateException` of the unique index → false).
- DI: repository, `IImapMailbox`, `EmailInboxPoller`, processor. `ChannelWorker` also calls `EmailInboxPoller.PollAsync` when `EmailChannelOptions.IsImapConfigured`.
- **Migration:** `dotnet ef migrations add AddReceivedMessages --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations`.

### 4 — Infrastructure / integration tests [P1]

`server/tests/Crm.Api.IntegrationTests/Channels/`:
- `EmailMessageParserTests` — reads Message-Id, lower-case from, name, subject, text body; HTML-only body becomes text; missing Message-Id → stable hash.
- `EmailInboxPollerTests` (fake `IImapMailbox`, real processor + SQLite): known customer linked (AC 1), unknown sender → new customer (AC 2), tag → `TicketNumber` (AC 3), the same message polled twice → one `ReceivedMessage` (AC 4), processed messages marked seen.

### 5 — Ticket wiring [P2]

- After `git merge main` (with `CRM-15:`): `InboundMessageProcessor` gets the ticket services; `TicketNumber` of an existing ticket of the same customer → append a customer message (CRM-15 message entity, `Channel = Email`); otherwise create a ticket (`TicketChannel.Email`, `createdById` null). Record `messageReceived` (sourceId = message id). New migration `LinkReceivedMessagesToTickets` (`TicketId` nullable FK).
- Tests: `Email_FromKnownCustomer_CreatesATicketForThatCustomer` (AC 1), `Email_FromUnknownSender_CreatesCustomerAndTicket` (AC 2), `Email_WithTicketTag_IsAddedToTheExistingTicket` (AC 3), `SameEmailTwice_CreatesOneTicket` (AC 4).

---

## Frontend Tasks

No frontend changes ([P2] tickets created from email show up in the CRM-14 list with channel "Email").

---

## Edge Cases & Failure Modes

- **Duplicate Message-Id** (IMAP re-delivery, two workers) → `ExistsAsync` check, then unique index `(Channel, ExternalId)`; race → `SaveChangesAsync` false → treated as duplicate.
- **Missing Message-Id** → deterministic hash, so a re-poll is still a duplicate.
- **Sender address invalid for the customer validator** → stored with `CustomerId` null (nothing lost; an agent can link it later — Phase 2 creates no ticket without a customer).
- **Tag for a ticket that does not exist / belongs to another customer** → [P2] a new ticket is created.
- **IMAP not configured** → poller not called; startup fine.
- **IMAP failure** → exception logged by `ChannelWorker`, next run tries again; unprocessed mails stay unseen.
- **Very long body** → cut to 10 000 characters with "…".

---

## Test Plan

1. Unit: `ReceivedMessageTests`, `InboundMessageProcessorTests`.
2. Integration: `EmailMessageParserTests`, `EmailInboxPollerTests`.
3. [P2] the four ticket tests above.

**Deviations (Phase 1 as built):**
- Deviation: `IImapMailbox` has one method `ReadUnseenAsync(settings, max, handle, ct)` (one IMAP connection per poll; a message is flagged `\Seen` only when the handler returns true) instead of `FetchUnseenAsync` + `MarkSeenAsync`.
- Deviation: `EmailMessageParser.Parse` returns null for an email without sender address (the poller marks it seen and logs a warning); `ReceivedAt` is the poll time (UTC) from `TimeProvider`, not the sender-controlled `Date` header.
- Deviation: the WhatsApp branch of `InboundMessageProcessor` (phone lookup, new customer + WhatsApp contact) is written in this story because the class is shared; its tests come with CRM-26. Extra tests: `UnknownSender_WithoutName_IsNamedByTheAddress`, `ConcurrentDuplicate_DetectedOnSave_IsIgnored`, `Create_WithNonUtcTime_Throws`, parser `Parse_WithoutSender_ReturnsNull`, poller `Poll_WhenImapIsNotConfigured_DoesNothing`.
- Known limit: in a duplicate race, a customer created for the first copy stays (rare; no data loss).
- Results after Phase 1 of CRM-24: `dotnet test` 343 unit + 229 integration.

---

## Migration / Rollback

- `AddReceivedMessages`: table `ReceivedMessages` + FK to `Customers` (`NO ACTION`). Rollback to `AddOutboundMessages` drops it.

---

## Verification Steps

1. `server/`: `dotnet build` (0 warnings), `dotnet test`.
2. `client/`: `npm test`, `npm run build`, `npm run lint`.

---

## Done Criteria

- [x] [P1] Known sender matched (`KnownSender_IsLinkedToThatCustomer`); [P2] ticket linked — AC 1.
- [x] [P1] Unknown sender → new customer; [P2] + ticket — AC 2.
- [x] [P1] Tag extracted; [P2] appended to the existing ticket — AC 3.
- [x] [P1] Same Message-Id ignored (`SameMessageIdTwice_IsIgnored`, poller test) — AC 4.
- [x] All builds and tests green.

## Phase 2 as built

- `IChannelTicketService` / `ChannelTicketService` (Application/Tickets): `InboundMessageProcessor` stores the `ReceivedMessage`, then hands known / new customers to it. A `[TKT-n]` tag of an **open** ticket of the **same customer** appends an Inbound `TicketMessage` (`Ticket.RecordCustomerMessage`, never `FirstResponseAt`); otherwise a new `TicketChannel.Email` ticket (subject without the tag, description = body, `createdById` null, SLA due times via `ApplySla`, timeline `ticketCreated` + `messageReceived`). A tag for a closed / foreign / unknown ticket opens a new ticket.
- `ReceivedMessage.TicketId` (nullable FK) + `LinkToTicket`; migration `LinkReceivedMessagesToTickets`. `InboundResult` carries `TicketId` / `TicketCreated`. `InteractionEvents.MessageReceived` added.
- Shared `TicketNumbering.SaveNewAsync` (extracted from `TicketService`) so channel tickets use the same number lock.
- Deviation: the message is stored first and linked in a second save, so a duplicate delivery is dropped before any ticket exists; a crash between the two saves leaves a stored message without ticket.
- Tests: `ChannelTicketServiceTests`, `InboundMessageProcessorTests` (unit); `ChannelTicketWiringTests.Email_*`, `SameEmailTwice_CreatesOneTicket` (integration).
