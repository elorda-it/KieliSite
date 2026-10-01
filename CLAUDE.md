# CLAUDE.md — KieliSite

kieli.kz for «BASTAU LINE» ЖШС / Омар Бекмұрат: .NET 10 MVC + MySQL + Dapper, built on the
NiceGirlSite framework (MODEL → COMMON → DBHelper → KieliWeb). Public UI and admin UI are Kazakh;
talk to the user in Chinese. README.md (Chinese) has setup, deployment and the admin guide.

The public markup is the approved static prototype in `prototype/` — keep class names and
structure identical; content comes from the database, never hard-coded in views.

## Verify

```bash
dotnet build KieliSite.sln -nologo -v q
```

Must stay at 0 errors, 0 warnings. No unit tests: run the site and check pages.

```bash
cd src/KieliWeb && dotnet run --no-launch-profile -e ASPNETCORE_ENVIRONMENT=Development -e ASPNETCORE_URLS=http://127.0.0.1:51850
```

The desktop app's preview launcher cannot read ~/Documents (macOS privacy), so start the server from
the shell in the background and open the URL in the browser pane.

## Where things are

- `Setup/DatabaseBootstrap.cs` — every start: schema (`db/kieli_schema.sql`; new columns are added to
  existing tables; a database holding another program's same-named tables stops the start before anything changes), languages, admin menu (`Setup/AdminMenu.cs`), roles, first admin, content from
  `db/seed/*.json`, list row ids, draft translations. Only fills what is missing.
- `Setup/BlockRegistry*.cs` + `db/seed/blocks.json` — the 52 editable page blocks. Views read them
  with `Block("home.hero")["title"]`, `.Raw()` (html), `.Lines()`, `.List()`; see `Setup/BlockContent.cs`.
  `tools/blocks/generate.py` generated both files once from the prototype; after that edit by hand.
- `Setup/ContentStore.cs` — cached reads for the public site and the `window.BASTAU/KIELI/KAZTEST`
  JSON (same shapes as the prototype data files). Every admin save calls `ContentStore.Clear`.
- `Setup/Cms/` — admin engine: `Entities.cs` (one `EntityDef` per table: fields, list columns,
  Prepare/DeleteGuard), `FieldValues.cs` (clean posted JSON, entity ↔ JSON), `CmsController`.
  Admin controllers only map `X`, `X [post]`, `GetXList`, `SetXStatus` onto it.
- `Views/Console/Cms/` — shared list/edit views; `_Fields.cshtml` renders a `FieldDef[]`;
  `wwwroot/console/js/kieli-admin.js` reads the fields back into the hidden `dataJson` on submit.
- `Views/Themes/Kieli/` — public layout, partials, pages; `wwwroot/kieli/` — prototype CSS/JS/images
  (`kieli.js`, `kaztest.js` adapted from the prototype: real form submit, server-rendered sections).
- `Controllers/ConsultController.cs` — `POST /api/consult` (requests + files to `App_Data/consult/`).
- SEO/GEO: `Setup/StructuredData.cs` builds each page's schema.org JSON-LD (one `@graph`: BASTAU LINE as LocalBusiness,
  website, founder, the page and its Service/Article/TouristAttraction/LearningResource/FAQPage/breadcrumbs) from the same
  blocks and tables, in the page language; `HomeController.Render` passes it to the layout with the Open Graph values.
  `Controllers/SeoController.cs` serves `/robots.txt`, `/sitemap.xml` (hreflang + lastmod) and `/llms.txt`, `/llms-full.txt`
  (English summary for AI assistants). Unknown addresses under a language fall back to `Home/Missing` (404 page, noindex).
- Automatic news: `Setup/NewsCollector.cs` reads the RSS/Atom feeds of `newssource` (admin «Жаңалық көздері», seeds in
  `db/seed/newssources.json`) every hour (Hangfire `JobCollectNews`, production only; the admin list's «Қазір тексеру»
  runs it anywhere) and adds matching items as link-only news cards (`article.newsSourceId`). Keep it a headline +
  the feed's short summary + source + link: never copy full text or pictures, never get around robots.txt or bot
  protection (Kazinform's Cloudflare), and check a publisher's terms before adding it (Egemen: non-commercial only).
- Brand: the site name is kieli.kz (`site.brand`), the company stays «BASTAU LINE» ЖШС. Logo files are in `wwwroot/kieli/img/`
  (README «品牌与 Logo»); `BrandName()` colours the part after the last dot. `Media(url)` versions `/kieli/` files whose
  address comes from the database (static files are cached for a year).
- Places: `db/seed/kazakhstan.json` has 188 — the prototype's 18 (rewritten by hand) and 170 converted from the old kieli.kz
  database by `tools/kazakhstan/import_old_places.py` (capital titles recased from the text, bodies cleaned, photo credits
  kept as captions, deleted YouTube videos left out). `AddMissingPlaces` adds seed places a database lacks (by legacyId or
  slug; deleted rows stay deleted); old `/kz/attraction/view?id=` addresses 301 to the place in the same language.
- ҚАЗТЕСТ: `kaztestvariant` → `kaztestquestion` (listening questions point to `kaztestaudio.audioNo`, reading questions
  to `kaztestpassage.passageNo`). The bank is BASTAU LINE's own (written 2026-10, `db/seed/kaztest.json`); never put
  National Testing Center (testcenter.kz) questions, texts or audio back — they may not be used. A variant added to the
  seed later is added to existing databases on start (matched by title or first reading text, deleted ones stay deleted). Recording scripts
  for the voice work: `docs/kaztest-audio/`. Audio uploads go to `wwwroot/uploads/audio/` (`Content/UploadAudio`).
  The shipped audio (`wwwroot/kieli/audio/kaztest/*.m4a`) is synthetic — Piper TTS, kk_KZ-issai-high (ISSAI
  KazakhTTS2, CC BY 4.0) — made by `tools/kaztest-audio/make_audio.py`; keep each recording's credit, and bump the
  `?v=` of its link after regenerating (static files are cached for a year).

## Languages

Kazakh (`kz`) is the base; `ru`, `zh-cn`, `en`, `tr` are translations (`Setup/SiteLanguages.cs`; shown ones
= `language.frontendDisplay`). Every read goes through `ContentStore.X(cache, …, lang)`, which puts the
translation over the Kazakh values (untranslated → Kazakh) and localizes `/kz/…` links.
- Views: `Block(key)` is already in the page language; `U("form.nameLabel")` for interface texts
  (`ui.*` blocks, defaults `db/seed/ui.json`); `L("/kz/services")` for site links; `Lang`, `IsKazakh`.
- Scripts: `window.KIELI_UI` (+ `KIELI_UI_KAZTEST`) and `window.KIELI_LANG`; kieli.js `t("group.key", {n})`,
  kaztest.js `tr()/tx()`. No Kazakh literals in views or scripts — add a `ui.json` key instead (and its
  translations to `db/seed/i18n/*.json` under `blocks["ui.…"]`).
- Storage: `multilanguage` (blocks: one JSON per block+lang in column `dataJson`; content rows: one value
  per field, lists as JSON, service page sections in column `data`). List rows carry `_id` (never
  regenerate it; `kieli-admin.js` sends it back as `data-kf-rowid`), translations match rows by `_id`.
- `FieldDef.Translatable` = text kinds unless `NoTranslate`; `EntityDef.Translatable = false` for ҚАЗТЕСТ
  questions and passages (the test is Kazakh in every language).
- Draft translations: `db/seed/i18n/{lang}.json` (same shape as `--export-translations`), imported on
  start for texts without a translation. Tote (Arabic script) is a client-side view of Kazakh pages only.
- Changing a seeded default text (blocks.json, i18n, the site title): add `{key, field, lang, from}` to `db/seed/upgrades.json`
  so databases that still hold the old default get the new one on start (texts edited in the admin are left alone).

## Conventions

- Permissions: a menu item `/{controller}/{action}/list` in `navigation` carries view/create/edit/delete;
  `PermissionFilter` maps POST `GetXList`/`SetXStatus`/`X` onto it. New admin screens need a menu item
  in `AdminMenu.cs`; the bootstrap adds missing items to existing databases (full-access role, and the
  content editor role for content groups).
- Admin action names must not clash with `Controller` members (`Request`, `File`, `View`…).
- `@page` is a Razor directive: never name a Razor variable `page`.
- Tag-helper `model='…'` attributes use single quotes when the expression contains strings.
- Razor treats `x@y` as an e-mail address: build URLs with `@` after letters in code, not inline.
- JSON placed in `<script>` goes through `ContentStore.ScriptJson` (HTML-escaped).
- A `<script type="…">` in a view is written `<!script …>…</!script>`: the MVC script tag helper takes every script with a
  type (for import maps) and re-encodes the attribute (`application/ld+json` → `application/ld&#x2B;json`).
- Connections set `IgnoreCommandTransaction=true` (DBHelper) because the framework's admin code
  opens transactions without passing them to commands.
- Soft delete everywhere (`qStatus = 1`); unix-int times; lowerCamel columns ↔ PascalCase properties.

## Secrets

The repository (github.com/elorda-it/KieliSite) is public: nothing secret or personal may be committed — check
`git status` against `.gitignore` before every commit. `appsettings.json` is committed with `REPLACE_ME`. Real values live only in the gitignored
`appsettings.Development.json` / `appsettings.Production.json`. Never commit a real `Pwd=`, never
connect to the production `kieli_db` from a dev session, never print credentials.
`App_Data/` holds visitors' documents (personal data) — gitignored, never commit or publish it.
