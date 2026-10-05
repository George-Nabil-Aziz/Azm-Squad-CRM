# Customer Support CRM

Customer support CRM for AZM Squad: customers, tickets, SLA, Email/WhatsApp channels, knowledge base, customer portal, reports. Arabic + English.

**Stack:** ASP.NET Core (.NET 10) + EF Core + SQL Server · React + Vite + TypeScript + shadcn/ui · xUnit + Vitest

## Team setup

### Requirements

- .NET SDK 10
- Node.js 20+
- [Claude Code](https://claude.com/claude-code)
- squad-kit: `npm install -g squad-kit`

### 1. Connect Notion (user stories)

User stories live in the Notion database **CRM User Stories**. Claude reads them through the Notion MCP connector, which is linked to each person's own account (it is not in `.mcp.json`).

1. Go to claude.ai → **Settings → Connectors → Notion → Connect**.
2. Give it access to the **Customer Support CRM** page (ask George to share it with you).
3. Restart Claude Code and check with `/mcp` that Notion is connected.

### 2. Project tools (automatic)

Opening the project in Claude Code loads these from the repo. Approve them when asked the first time:

| Tool | Where |
|---|---|
| squad-kit commands `/squad-plan`, `/squad-new-story` | `.claude/commands/` |
| Vercel React best practices skill | `.claude/skills/` |
| .NET skills (dotnet, aspnetcore, data, test) | `.claude/settings.json` |
| shadcn MCP server | `.mcp.json` |

## Workflow

Every change follows the squad-kit flow (details in [CLAUDE.md](CLAUDE.md)):

```
Notion story → squad new-story (intake.md) → /squad-plan → tests first (TDD) → implement → Notion status Done
```
