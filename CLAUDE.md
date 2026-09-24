# CLAUDE.md

Hungry Pathing: a Timberborn mod (C#, Harmony, built against Timberborn 1.1.2.4's own assemblies) that times working
beavers' food and water trips to their shift and sends them to the closest stocked storage. Source in `source/`,
packaging in `packaging/`, tests in `tests/`, the project site in `docs/`. Changes land on `main` through a PR. CI
(`.github/workflows/tests.yml`, every PR and push to main) runs the two checks that need no game files:
- Planner checks: `dotnet run --project tests/HungryPathing.Tests.csproj -c Release` ("All planner checks passed.").
- Site checks: `node tests/test-site.mjs` (37/37; Node only, no packages, no network).
- Local build (needs the game installed): `.\build.ps1` makes `dist\HungryPathing-<version>.zip` (`-Test` adds the
  planner checks). Never pass `-Install`: it copies into the game's Mods folder.

## Standing rules

- Never launch or drive Timberborn, and never touch installed mods or saves. The maintainer (Kyler) playtests himself.
- Commit on a branch and open a PR. Kyler has said to merge PRs automatically: merge, then check the page live.
- Assume fresh games: no old-save compatibility notes. Version history goes in CHANGELOG.md, never on player pages.
- **There are two DESIGN.md files.** The root `DESIGN.md` is the MOD's design document (game code paths, the planner's
  reasoning); never touch it for site work. The SITE's design record is `docs/DESIGN.md`.

## Writing README and website text

Kyler, 2026-09-24: "simplicity and elegance is effective and desirable." Every change to the README, the website
text and the player docs follows these rules.

- **Write for a Timberborn player** who wants to download, install and use the mod. Developer detail goes in
  TESTING.md (building and checks), DESIGN.md (how the mod decides) or CHANGELOG.md; link to it rather than
  repeating it.
- **Short.** One idea per sentence, most under about 20 words. A paragraph or FAQ answer is one to three sentences,
  a troubleshooting answer a few numbered steps.
- **Lead with the action.** Menu paths as arrow chains; on-screen labels in bold, exactly as in game.
- **Say each thing once**, where a player would look for it; link to it elsewhere.
- **Plain words.** No internals (class names, ids, formats) unless the player needs them to act.
- **Cut** filler, repeated caveats, edge cases a player won't meet, and history ("since …", "no longer", older
  builds). Describe the mod as it is now.
- **Check every fact against the code** before writing it; changelogs lag.
- **Keep, briefly:** credits, the unofficial line, the status, and safety facts.
- **Reread as a new player before publishing.** Every step works as written, and nothing is said twice.

## Website

- **Where:** `docs/`: `index.html` (Overview), `install.html`, `troubleshooting.html`, `faq.html`, `404.html`;
  `docs/assets/`: `style.css`, `release.js`, `shift.js`, `banner.svg`, `favicon.svg`, `og.png`, `fonts/`, `textures/`.
  Live at https://timbermods.github.io/HungryPathing/.
- **Published:** GitHub Pages serves `main:/docs` (legacy build), so merging to main publishes; no deploy script. A
  build takes about a minute.
- **Latest releases update themselves:** when a release becomes GitHub's Latest, `.github/workflows/latest-release.yml`
  (the shared timbermods workflow) appends the standard footer to its notes, sets the site's
  `data-release="version|tag|asset-name"` fallback text and the README lines ending in `<!-- latest -->` to the new
  version, runs the site checks and commits to main. Pre-releases change nothing. Descriptions, status lists and FAQs
  stay manual (the checklist below). Dry run: Actions → Latest release → Run workflow.
- **Look:** "The Works Canteen": the canteen yard of a timber mill at the end of a shift. Enamel yard signs (cobalt on
  cream), a slate tally board in chalk with coral for hunger, timber frames, a wood-chip yard ground. The look is fixed:
  updates extend it and never restyle it.
- **Design records (read these before any site change).** The site is its own Impeccable project (root
  `.impeccable/config.json` is `{"projectRoots":["docs"]}`), so `impeccable context --target docs/index.html` loads
  root PRODUCT.md with `docs/DESIGN.md`.
  - `PRODUCT.md` (repo root): facts, voice, terminology, and every site-test contract (numbered 1–11).
  - `docs/DESIGN.md`: the visual system and its named rules, the source of truth for the look.
  - `docs/.impeccable/surfaces/docs-index-html.md` (direction contract), `docs/.impeccable/design.json` (tokens), and
    `.impeccable/critique/` at the repo root (the pre-redesign critique).

### Design rules (from docs/DESIGN.md; keep them)

- **Fixed Materials Rule**: enamel, slate, chalk, coral, water and frame look the same in both themes; only the yard
  tokens (yard, ink, muted, rule, band, link, coral-ink, water-ink) change.
- **Chalk Has Jobs Rule**: coral means hunger, the buffer or the penalty; water blue means the walk or the board's
  control. Never decoration, never raw text on the yard (use `--coral-ink` / `--water-ink` there).
- **Lettering Rule**: Big Shoulders only in 700 and 800; uppercase only for enamel sign titles; sentence case.
- **Clock Rule**: times are clock times or hour counts ("leaves 9:30", "0.96 h"), tabular figures where compared.
- **No Offset Rule**: no drop/offset shadows, glows, radial washes or CSS bevels; depth is frame and material. The only
  shadows are the enamel keyline (inset 2px cobalt + 3px cream) and the board's slate seat.
- Anything measured or timed sits on slate in chalk in a timber frame (10px; 8px on phones and on the notice). Every
  enamel sign is the `enamel.png` nine-slice with one heading and one short paragraph.
- Tokens are in `docs/assets/style.css`: `:root`, then `@media (prefers-color-scheme: dark) { :root { … } }`. Themes
  follow `prefers-color-scheme` only: no toggle and no storage key (a toggle is a don't).
  - Fixed: cobalt #1d4a86, cobalt-deep #143663, enamel #efe8d6, enamel-ink #1b2a3e, slate #23292d, chalk #ece9e1,
    chalk-muted #b8bcb8, coral #e8735c, water #7cc3e6, frame #5a4331; header nav text #d6dff0 (hover and current #fff).
  - Yard, light / dark: yard #e3ddd0 / #191b1d, ink #1e2226 / #ebe6dc, muted #4d5358 / #aeb0aa, rule #c8bfab /
    #363a3d, band rgba(30,34,38,.05) / rgba(235,230,220,.04), link = focus #1d4a86 / #93b9ee, link-hover #133566 /
    #bcd4f5, coral-ink #a8432b / #f29077, water-ink #1f6690 / #8fcdeb.
- Fonts: Big Shoulders Display 700/800 (headings, brand, nav, buttons, labels, tally figures), self-hosted in
  `docs/assets/fonts/` with `OFL-BigShoulders.txt`. Body: system-ui; code: ui-monospace. No other webfonts, no CDN.
- Textures: `slate.webp`, `yard-day.webp`, `yard-night.webp` (512px tiles) and `enamel.png` (96px nine-slice, slice
  24) come from `docs/assets/textures/make_textures.py` (numpy + Pillow, fixed seeds; run it in that folder). Change
  the script and re-run it; never edit the images. Every shipping raster carries provenance: with
  `IMP=$(ls -d ~/.claude/plugins/cache/impeccable/impeccable/*/skills/impeccable | tail -1)`, run
  `"$IMP/scripts/impeccable" embed-prompt <file> --prompt "Origin: …"` on each new or changed image, and check with
  `"$IMP/scripts/impeccable" embed-prompt --scan docs` (today "5 rasters, 0 missing"). `og.png` (1200x630) is a
  capture of the home hero (see the to-dos).
- Phones: no sideways scroll at 390px; tap targets ≥ 44px (TOC and footer links: see the to-dos).
- Motion: the signature is the slate board: drag the walk slider and the leave time moves. The marks jump; nothing
  animates. Otherwise only a 1px button lift (.15s, cubic-bezier(.2,.8,.2,1)) and the FAQ chevron turn (.2s), both
  off under `prefers-reduced-motion`.
- Don't: shadows, glows or bevels; coral or water as decoration; pill chips; identical numbered cards; the old
  teal-and-gold palette; a theme toggle; game art or faked screenshots; other Big Shoulders weights or body text in it.
- New components: build from these tokens and components, match the neighbours, and add them to `docs/DESIGN.md` and
  `docs/.impeccable/design.json`.
- `docs/assets/og.png` has no generator in the repo (a Playwright capture of the home hero at 1200x630): re-capture it
  whenever the hero changes, then `embed-prompt` it again.

### The slate board and the site test's stub DOM

- `docs/assets/shift.js` `plan(walk)` (START 13.5 h, BUFFER 3, SHIFT 16, TOP_OFF_WALK 0.5, penalty at 0.75 speed)
  redraws the SVG elements with ids `sh-*` and the live-region text. **Nothing generates the no-JS state.** The markup
  in `index.html` is `plan(60 min)` copied by hand, with the controls `hidden` until the script runs. After changing
  shift.js or the board, print the state (Git Bash, repo root; the argument is minutes) and paste every `d`, `x`, `y`
  and text into the matching `sh-*` element, including `<output id="sh-out">` and the two `board-read` lines:
  `node -e "const e={};global.document={addEventListener(){},getElementById:i=>e[i]||(e[i]={a:{},getAttribute:k=>k=='value'?process.argv[1]:null,removeAttribute(){},setAttribute(k,v){this.a[k]=v},set textContent(t){this.a.text=t}})};eval(require('fs').readFileSync('docs/assets/shift.js','utf8'));for(const i in e)console.log(i,JSON.stringify(e[i].a))" 60`
- `node tests/test-site.mjs` runs every local script in a stub DOM with no `classList`, `style`, `innerHTML`,
  `closest`, `matches`, element `.value` or element listeners; only `document.addEventListener` exists. So shift.js
  changes the SVG only via `setAttribute`/`textContent`, reveals the controls with `removeAttribute('hidden')`, hears
  the slider through one delegated `input` listener on `document`, and falls back to `getAttribute('value')`. The
  test fires no input events, so it only runs the default draw, but that text counts for the "preview" and
  release-name checks. Inline scripts (the FAQ's expand/collapse) are not run.

### Content rules

- Write every player-facing change by *Writing README and website text* above.
- Describe the mod as it is now: no "New in", "since 0.x" or "added in" on player pages.
- Played/not-played status matches the README ("**Beta.**"; "What it did in a real colony": one Folktails colony of
  330 to 355 adults, 36 in-game days, nearly all as a BeaverBuddies host; the builder rule is not yet observed on a
  construction day) and the newest CHANGELOG entry (the current release has not been played in a live game). The
  caution is "Try it on a copy of your save first." Never invent numbers, reviews or screenshots; tally figures come
  from README and TESTING.md exactly.
- Keep the footer: "unofficial community mod … maintained by Timbermods. Not affiliated with or endorsed by
  Mechanistry", and the link to https://timbermods.github.io/. MIT, copyright Timbermods.
- Terminology (PRODUCT.md): "Hungry Pathing" is the name; `HungryPathing` is the folder, zip and log tag. "The
  buffer", "just in time", "pre-fuel"/"top off", "builder job check", "closest storage while working", "penalty-state
  trips", "switched off", "the daily summary line", "the settings file" (`HungryPathing.cfg`), never "Mod Settings".
- Site-test contracts: download buttons are written as `href="…/releases/latest"` with `data-release-href="download"`.
  Hand-written text never has a real tag (`v0.3.1`) or zip name (`HungryPathing-0.3.1.zip`); sample log lines show
  `&lt;version&gt;`. The only "preview" is the hero's hidden `data-release-show="prerelease"` span. Hide-until-ready
  rules are exactly `html:not([data-release-ready]) <simple selector> { display: none; }`. Local script and style paths
  are relative, except `404.html`, which keeps absolute `/HungryPathing/…` paths.
- `docs/assets/release.js` is the shared timbermods copy, byte-identical to MixedStorage's and the Stability Fork's:
  replace it with the shared copy, never edit it.

### Update the website for a new release

When asked to "update the website for the latest release, consistent with the design" (write it by
*Writing README and website text* above):
1. Read `gh release view <tag> -R timbermods/HungryPathing`, README.md, CHANGELOG.md, CONFIGURATION.md, TESTING.md
   and `packaging/manifest.json`. List every player-facing change.
2. Update every place the site states a changed fact:
   - Version: none by hand. release.js fills `data-release="tag"` (index `.status-note .ver`) and `"asset-name"`
     (install `p.file-name`); no `data-release-pinned`. `grep -rn "0\.3\.1" docs` stays empty.
   - Status: index hero `.status-note`; index `#status` (intro, Played, Not yet); install `#verify` "Beta" callout and
     `#coop` "with this beta"; faq `#alpha`.
   - Game 1.1.2.4 and Harmony 2.4.1: index `#compat`, install `#requirements`.
   - Hooks "5/5" / "five hooks": index `#details`, install `#verify`, troubleshooting `#hooks` and `#log`.
   - Log lines: install `#verify` (`pre` and `dl.confirm`), troubleshooting `#log`.
   - Settings keys: install `#settings` table, faq `#one-rule` and `#change-buffer`, troubleshooting `#still-far`,
     `#morning`, `#builders`, `#performance`.
   - Rules and features: hero `.facts`, `.signs`, `#how`, `#details` `.notes`, faq "The basics".
   - Numbers: `#results` tally, `.aside-note` (8 path queries = `CandidateLimit`), troubleshooting `#performance`.
   - Co-op: index `#compat`, install `#coop`, faq `#coop`, troubleshooting `#coop` and `#switched-off`.
   - Meta: each page's `meta description`, index `og:description` and `og.png`; PRODUCT.md's Operating Context.
3. Use the existing components. A rule becomes a `<li class="sign">` before the slate `.after-hours` notice, which stays
   last (the grid is 3/2/1 columns: keep the count even or rethink it). A setting becomes a row in install
   `#settings`. A question becomes a `<details class="q" id>` in its `.faq-group`. A problem becomes an
   `<article class="issue" id>` (Likely cause / Fix) plus a TOC `<li>`. A compat fact becomes a `.sheet-rows` div.
   Status goes in `.checks` / `.checks.open`; a log line to check goes in `dl.confirm`.
4. Test: `node tests/test-site.mjs` (37/37) and `dotnet run --project tests/HungryPathing.Tests.csproj -c Release`.
5. Preview (Git Bash, repo root): `python -m http.server 8785 -d docs` in the background, open http://localhost:8785/,
   stop it after. `404.html` renders unstyled locally (absolute paths); check it live. Capture light, dark and a 390px
   phone. With the personal `impeccable-site-flow` skill:
   `python <skill>/scripts/capsite.py http://localhost:8785/ <out> "" install.html troubleshooting.html faq.html`.
   Otherwise use the Browser pane in both colour schemes at desktop and mobile sizes. Check the changed sections and
   that nothing scrolls sideways.
6. Optional: `"$IMP/scripts/impeccable" detect --json docs` (parse from the first `[`; non-zero exit when it finds
   anything). Known false positives, and `.impeccable/config.json` has no ignores: `side-tab` `border-top: 10px`
   (footer timber frame); `side-tab`/`border-accent-on-rounded` `border-bottom: 3px` (nav's coral current-page
   underline); `design-system-color` #fff/#d6dff0 (header nav) and the 404's rgb(0,0,0) (its absolute stylesheet is
   unreadable to the detector); `design-system-font-size` for sizes docs/DESIGN.md documents (guide heading clamps,
   .86–1.45rem, the board's 15px SVG labels).
7. If the look changed, update `docs/DESIGN.md` and `docs/.impeccable/design.json`, and the README if it repeats it.
8. Ship: branch → commit → push → `gh pr create`. After Kyler says merge: `gh pr merge <n> --merge` (Pages then
   publishes `main:/docs`), and verify:
   - `gh api repos/timbermods/HungryPathing/pages/builds/latest -q .status` is `built`;
   - `curl -s https://timbermods.github.io/HungryPathing/ | grep -c "<a changed string>"` finds the change.

### Full redesign

The whole Impeccable flow (init → critique → audit → direction → build → finish review → docs/DESIGN.md), records in
`docs/`; with the personal skill: "use the impeccable-site-flow skill to redesign this site".
