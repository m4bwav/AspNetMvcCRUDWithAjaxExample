# AGENTS.md

Rules for any AI agent (Claude Code, Copilot, Cursor, Codex) working in this repository. `CLAUDE.md` and `.github/copilot-instructions.md` only point here.

## What this is

AspNetMvcCRUDWithAjaxExample: a small example website (a list of people with create, edit and details pages, and one ajax call that calculates a person's age). It was written in April 2014 on ASP.NET MVC 3, .NET Framework 4.0, Entity Framework 4.1 Model-First and SQL Server Compact 4.0, and was never published or deployed. It is being modernized with the package-modernize skill's repository variant, the first run on a website. The plan is ai-docs/plans/2026-09-30-modernization-and-v2-release.md once it exists; start with `ai-docs/HANDOFF.md`.

## Rules

- **The 2014 site is the contract.** Its behaviour is recorded from the running site under `tests/Golden/` (pages, form posts, validation failures, the ajax JSON, screenshots). The recording is made once, from the frozen commit (tag `v1.0.0`, commit df9fb35), and never regenerated or edited. When the new site differs, fix the new site, or the difference becomes a named exception that the maintainer ruled on in the plan.
- **Nothing is deployed or published without the maintainer.** No cloud credential or registry token is created or stored. A release workflow waits at an environment for the maintainer's approval.
- **Tests never touch the internet.** Page tests run the site in memory; the recording runs against a local server.
- **Research beats recall.** SDK, package, front-end library and action versions change; re-verify any version older than three months, and check every package name on its registry before installing it.
- **Document for handoff.** Anything learned, decided or built goes into `ai-docs/` before you finish; rewrite `ai-docs/HANDOFF.md` when work is left unfinished.
- **No AI attribution anywhere.** No Co-Authored-By trailers, no "generated with" lines in commits, pull requests or files.
- **Line endings.** New files are LF; count byte 13 with node before committing on Windows.

## Layout and traps

- `CRUDAjaxExampleWeb/` is the 2014 project as it was. It does not build on a machine with only the .NET SDK (`dotnet build` stops with MSB4278 on the Visual Studio 2010 web application targets; no .NET Framework 4.0 reference assemblies; ASP.NET MVC 3 and SQL Server Compact 4.0 were GAC installs).
- `CRUDAjaxExampleWeb/App_Data/DeveloperTest.sdf` holds four rows of keyboard test data. Opening it with SQL Server Compact rewrites the file: always work on a copy.
- `packages/` and `CRUDAjaxExampleWeb/obj/` are committed build leftovers from 2014.

## everlast (session knowledge, load on demand)

- `ai-docs/INDEX.md` lists what past sessions learned here (solutions with verified commands, decisions with reasons, plans). At the start of a task, scan it and open only the entries whose title or tags match; no line matches: `everlast.py search "<key terms>"` before concluding nothing was recorded. Read `ai-docs/HANDOFF.md` when continuing unfinished work (everlast-resume skill).
- Before acting on an entry marked `(recheck due)`, run `everlast.py recheck <entry>`, re-run its Verified-by command only when that is read-only or safe (a build, a test, a version query), then record `everlast.py verify <entry>` or `verify <entry> --failed "what broke"`; a fix that changed is superseded, never reused blindly.
- Before finishing a task that hit a dead end, verified a non-obvious command, made a design choice, or taught you something about the user, record it (everlast-capture skill, or `everlast.py note` / `handoff`); rewrite `HANDOFF.md` when work is left unfinished. Say "nothing to record" when that is true.
- Anything naming a person, an internal host or name, a credential, or an opinion about people goes to the private sidecar (`--private`), never here. Lessons about the user or this machine go to the user tier (`--user`).
- Rules go in this file, system layout in CODEMAP.md; the doc set holds only what could not be re-derived from the code in a minute.
- Link documents together with relative markdown links: every markdown folder is reachable from an index whose lines say when to read each file (`ai-docs/INDEX.md` is generated from frontmatter; give entries a one-line `summary`), and an entry links the entries it relates to on a typed `Related:` line (`supersedes`, `contradicts`, `builds on`, `see also`). The set then reads as a graph for people in Obsidian and for agents alike. No wikilinks in the repo.
