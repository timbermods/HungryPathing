# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Timberborn players with a working colony, usually a large one (hundreds of beavers), who have watched builders and
haulers drop tools at a far site, work at half speed once hunger hits zero, then walk across the map to the district
center for bread while a stocked warehouse sits next door. Many play co-op through BeaverBuddies (the Stability Fork
or MultiColony) and need to know it won't desync their game. Comfortable enough with mods to unzip a folder, but
mostly not programmers. They arrive from the Timbermods org site, a friend or a forum post, want to know in one look
what changes and whether it's safe to add to an existing colony, install it, and later read one log line to confirm
it works. Returning players come back to update, tune the settings file, or report a problem.

## Product Purpose

The website for **Hungry Pathing** (https://github.com/timbermods/HungryPathing), a Timberborn mod that makes
working beavers plan food and water around their shift instead of waiting for the hunger penalty, and eat at the
closest stocked storage while on the job instead of crossing the map for a fancier meal.

What it does, in plain words. During working hours only, for employed adults:
1. **A three-hour buffer** on Hunger and Thirst, from the game's own `HoursWarningThreshold` field (the game uses it
   for bots but ships every beaver need at zero and never reads it for beavers).
2. **Just in time:** a beaver measures the walk to the closest stocked storage and leaves early enough to arrive with
   the buffer still in hand.
3. **Pre-fuel:** when food or water is within half an hour's walk and the beaver would not last the rest of the shift
   plus the buffer, it tops off now (breakfast at the start of a shift; a hauler passing a warehouse mid-shift).
4. **Builder job check:** a builder who has just reserved a site it would not last at lets the site go through the
   game's own "could not get there" path and tops off first.
5. **Closest storage while working:** the mod's trips, and the game's own penalty-state trips during working hours,
   go to the storage with the shortest walk; a higher-scoring food wins only within a quarter hour of extra walking.

Off duty the game's own behavior runs unchanged, so evening variety-seeking stays. Beavers don't eat more: daily
consumption is unchanged, only its timing moves. The game does the walking, reserving and eating.

Success, in order:
1. **Understand it:** the visitor sees the problem (why beavers walk so far in the base game) and the fix, and that
   it only touches working hours, changes no pathfinding and writes nothing to saves.
2. **Install it right:** the `HungryPathing-X.Y.Z.zip` under Assets (not *Source code*), extracted so that
   `Documents\Timberborn\Mods\HungryPathing\version-1.1\manifest.json` exists (not nested one level too deep),
   Harmony enabled, Hungry Pathing enabled; in co-op every player on the same version, game version and an identical
   `HungryPathing.cfg`.
3. **Use it:** confirm it loaded (`Hooks installed (5/5).` and `buffer 3h` in Player.log), read the daily summary
   line, tune the settings file if wanted.
4. **Report problems:** a GitHub issue with the mod and game versions, BeaverBuddies or not, other mods, and every
   `[HungryPathing]` line from Player.log, including any warning with the lines after it.

## Positioning

The base game makes a working beaver leave the job only once a need is already at zero, then scores each food group
by need points before looking at distance, so plain berries next to the site lose to bread at the district center
nearly every time. Hungry Pathing keeps the game's own behaviors and changes only *when* a working beaver goes and
*which* storage it picks, from measured walking times. It is not a pathfinding mod, not a ration-carrying mod and
adds no buildings. It is built for lockstep co-op (decisions read only the simulation, with fixed ordering and
tie-breaks), and with BeaverBuddies MultiColony each colony's own working hours are used. The numbers are measured,
not promised: a 355-beaver colony went from 15 beavers in a penalty and 117 within three hours of one to 0 and 0 at
the same hour of the shift. No other mod doing the same job is known to the maintainer; don't name or compare
against any.

## Operating Context

- **Current release: 0.3.1 (beta)**, published 2026-09-22 as GitHub's **Latest** (a regular release, not a
  pre-release; "beta" is the mod's label, not a GitHub pre-release). Asset `HungryPathing-0.3.1.zip`. The site
  names the current release through `docs/assets/release.js`; hand-written text keeps no version number that goes
  stale. Earlier releases: 0.3.0 and 0.2.2 (pre-releases, "preview"), 0.2.1, 0.2.0, 0.1.0 alpha.
- **Game:** Timberborn **1.1.2.4** (`MinimumGameVersion` in the manifest). The hooks were written against 1.1.2.4
  and, if a game update moves one, the mod installs none of them and says so in the log. Built and checked on
  Windows only.
- **Requires:** **Harmony** 2.4.1 or newer (Steam Workshop 3284904751). Nothing else.
- **Saves:** nothing is saved. It can be added to or removed from any colony at any time; no migration on update.
- **Co-op:** works with BeaverBuddies. Condition: every player installs the same mod version, runs the same game
  version and uses an identical `HungryPathing.cfg` (only `DailyReport` and `Diagnostics` may differ). The
  `Simulation settings:` line printed at startup is what players compare. BeaverBuddies itself only warns when mod
  versions differ. With **BeaverBuddies MultiColony**, each beaver's shift end comes from its own colony's working
  hours (asked through reflection, no dependency) and the storage index is per district, so colonies never eat from
  each other's storages. 0.3.x changes decisions compared with 0.2.1: co-op players update together.
- **Factions:** Folktails tested; Iron Teeth expected to work, untested. Tubeways and ziplines can hide the quickest
  storage outside the `CandidateLimit` nearest by straight line.
- **What players meet in game:** almost nothing. Hungry Pathing appears in the game's **mod manager**. There is **no
  in-game settings screen** (not Mod Settings): every setting is a `key = value` line in
  `version-1.1\HungryPathing.cfg`, read once at startup (`Enabled`, `Needs`, `WarningHours`, `JustInTime`,
  `JustInTimeLeadHours`, `PreFuel`, `PreFuelNearFoodHours`, `BuilderJobCheck`, `BuilderJobWorkHours`,
  `WorkTimeClosestFood`, `VarietyToleranceHours`, `RedirectCriticalTrips`, `CandidateLimit`, `RetryHours`,
  `DailyReport`, `Diagnostics`; CONFIGURATION.md explains each). The only UI text is one English **dialog** shown if
  the mod switches itself off after an error; it asks co-op players to have the host save and host that save again,
  with every player joining it. Everything else is Player.log lines.
- **Reporting:** GitHub issues at https://github.com/timbermods/HungryPathing/issues. Log:
  `%USERPROFILE%\AppData\LocalLow\Mechanistry\Timberborn\Player.log` (`Player-prev.log` for the previous session).

## Capabilities and Constraints

- **Stack and hosting:** plain static HTML/CSS with no build step, in `docs/` on `main`: `index.html` (Overview),
  `install.html`, `troubleshooting.html`, `faq.html`, `404.html`, and `docs/assets/` (`style.css`, `release.js`,
  `banner.svg`, `favicon.svg`). GitHub Pages serves `main:/docs` (legacy build) at
  https://timbermods.github.io/HungryPathing/, so merging to `main` publishes; there is no deploy script. System
  fonts only, dark theme by default with a `prefers-color-scheme: light` variant. The FAQ's Expand all / Collapse all
  and open-from-hash are an inline script in `faq.html`. `404.html` loads its stylesheet and links by absolute
  `/HungryPathing/` paths (it is served at any depth); keep it that way. One of the Timbermods sites; the footer links
  to https://timbermods.github.io/.
- **Contracts the site test enforces** (`node tests/test-site.mjs`, Node only, no packages, no network; run by
  `.github/workflows/tests.yml` on every pull request and every push to `main`, after the planner checks and even if
  they failed). It parses every `*.html` directly in `docs/` into a stub DOM, runs each page's local scripts against
  a fake GitHub API over seven release scenarios, and fails when:
  1. `index.html` or `install.html` is missing or has no download button. A **download button** is any `<a>` with
     `data-release-href="download"`, or any `<a class="btn">` whose text contains the word "download" (any case).
     A button that says Download is therefore always held to rules 2 and 5.
  2. As written, before any script runs, any download button's `href` is anything other than exactly
     `https://github.com/timbermods/HungryPathing/releases/latest`.
  3. A sample log line on any `docs/*.html` page or in `README.md` matches `[HungryPathing] <number> loading.` (for
     example `0.3.1` or `v0.3.1`). Sample lines show `<version>` (`&lt;version&gt;` in HTML).
  4. A label is shown with an empty value: any `[data-release]` element that is empty while its parent shows other
     text. Such labels (the install guide's `File:` line, `p.file-name`) must be hidden until the script runs, by a
     rule in a local stylesheet written exactly as `html:not([data-release-ready]) <selector> { display: none; }`,
     where `<selector>` is a simple compound selector (tag, `.class`, `[attr]`, `[attr="value"]`, comma-separated)
     or the element has `hidden`. A descendant or other complex selector in such a rule makes the test throw. After
     the script runs with a release, every such field must be filled.
  5. When GitHub answers, every download button on every page must point to the offered zip
     (`https://github.com/timbermods/HungryPathing/releases/download/<tag>/HungryPathing-<version>.zip`). The offer
     is GitHub's Latest (newest non-draft, non-pre-release), or the newest pre-release only when there is no Latest,
     never a draft, even when more than ten pre-releases are newer than the Latest.
  6. Every page with a download button must show the offered tag (`v0.2.1` style) or its zip name in visible text
     after the script runs, and **no page may show any other release's tag or zip name**. So hand-written text never
     contains a real tag like `v0.3.1` or a real zip name like `HungryPathing-0.3.1.zip`; plain version numbers
     without the `v` (as in "measured on 0.1.0") and `HungryPathing-X.Y.Z.zip` are safe.
  7. The word **"preview"** (whole word, any case) appears in the visible text of any page unless the offered release
     is a pre-release, and it must appear when it is. Today only the hero's `<span data-release-show="prerelease"
     hidden> preview</span>` says it. Never use "preview" anywhere else in visible copy, including the FAQ and 404.
  8. When GitHub refuses (rate limit) or there are no releases, download buttons must keep their written links and
     no page may name a release or say "preview".
  9. Any page's local scripts throw. Every `<script src>` that is not `http(s):`/`//` is loaded relative to the page
     and run in the stub DOM, so local script paths must be relative (an absolute `/HungryPathing/...` src fails),
     and the only DOM available is: `querySelector(All)` with the compound selectors above, `getElementById`,
     `getAttribute`/`setAttribute`/`hasAttribute`/`removeAttribute`, `hidden`, `textContent`, `className`,
     `children`, `appendChild`/`removeChild`, `createElement`/`createTextNode`, and `addEventListener`/
     `dispatchEvent` on `document` only (plus `fetch`, `localStorage`, `sessionStorage`, `CustomEvent`, `location`,
     timers). No `classList`, `style`, `closest`, `matches`, `innerHTML` or element listeners. Inline scripts are
     not run, which is why the FAQ script is inline; any new external script must stay inside this subset or not be
     loaded from `docs/`.
  10. Stylesheets: local `<link rel="stylesheet">` hrefs must resolve as relative to `docs/` or start with
     `/HungryPathing/`; anything else makes the test throw. Pages must stay well-formed hand-written HTML (the
     parser is a tokenizer with a stack).
  11. The release script tag: `index.html` and `install.html` (any page with a download button) load
     `assets/release.js` with `data-repo="timbermods/HungryPathing"` and `data-asset="^HungryPathing-[\d.]+\.zip$"`;
     a different repo or a pattern that misses `HungryPathing-<version>.zip` fails rules 5 and 6.
- **`docs/assets/release.js` is shared, byte-identical, across the Timbermods sites** (currently identical to
  MixedStorage's): replace it with the shared copy, never edit it. It fills `data-release="version|tag|asset-name|
  sha256"`, `data-release-href="download|notes"`, `data-release-show="prerelease|stable"` and
  `data-release-pinned`, sets `data-release-ready` on `<html>` and caches the answer for 30 minutes.
- **Terminology:** "Hungry Pathing" (two words) is the mod's name as in the mod manager; `HungryPathing` is the
  folder, zip, log tag and repo. Hunger and Thirst (capitalized as needs); "the buffer" (three hours,
  `HoursWarningThreshold`, `WarningHours`); "just in time" / the just-in-time rule; "pre-fuel" / "top off"; "builder
  job check"; "closest storage while working"; "penalty-state trips" (the critical redirect,
  `RedirectCriticalTrips`); "working hours", "off duty", "shift"; "storage" for warehouses and tanks generally;
  "switched off" for the circuit breaker (the log says `Switched off for the rest of this game`); "the daily summary
  line" (`Day N: trips started by rule: ...`); the `Simulation settings:` line; "path queries"; "the settings file"
  (`HungryPathing.cfg`), never "Mod Settings" or "options menu". Co-op mod: BeaverBuddies; BeaverBuddies MultiColony.
- **Honest status of what has been played:** 0.1.0 and 0.2.0 ran 36 in-game days in one single-district Folktails
  colony of 330 to 355 adults, nearly all as a BeaverBuddies host with a guest; three days replayed from the same
  save gave identical counters and co-op hashes. The before-and-after table comes from that colony's saves on
  0.1.0. One error was found there (the builder job check on the first construction day, 0.2.0) and fixed in 0.2.1.
  **Not played in a live game:** 0.2.1, 0.2.2, 0.3.0 and **0.3.1, the current release** (offline checks only); the
  builder rule's decisions on a construction-heavy day; a MultiColony game where a colony set its own working hours;
  the switch-off dialog; Iron Teeth; anything but Windows. Say this plainly and without alarm ("try it on a copy of
  your save first"). Describe the mod as it is now; version history belongs in CHANGELOG.md. Upgrade facts players
  need: close the game, extract over the old folder and choose Replace, keep a copy of an edited `HungryPathing.cfg`
  (the zip ships defaults), every co-op player updates to the same version together, nothing to migrate. The current
  pages still carry version-history phrases ("since 0.3.1", "since Hungry Pathing 0.2.2", "Versions up to 0.2.2 word
  the warning ...", "up to 0.2.2, only restarting the game did"); these break the standing rule and should go.
- **Sources of truth:** README.md, CONFIGURATION.md, DESIGN.md, TESTING.md and the release notes. Where the site and
  those disagree, flag it; don't guess.

## Brand Commitments

- **Voice:** a fellow player explaining a useful mod: plain, exact, a little dry, numbers where they exist. Never
  hype, never "smart AI beavers". The mod decides; the game does the walking.
- **No official Timberborn logos or key art.** The game's own item icons (food, water, goods) are allowed where used,
  credited as Timberborn's; the current site uses none. The site's own art is original SVG (the brand mark with a
  hunger bar and a walk arc; `banner.svg`).
- **License:** MIT, copyright Timbermods, for the mod, docs and site.
- **Unofficial community mod, not affiliated with or endorsed by Mechanistry.** Part of Timbermods. The footer says
  both.

## Evidence on Hand

- `docs/assets/banner.svg`: an original illustration (a beaver at a construction site, a short walk to a nearby
  warehouse, a long faded walk to the district center, a hunger bar with the three-hour buffer). It is a diagram,
  not a screenshot. `docs/assets/favicon.svg` and the inline brand mark in each page header.
- **Real measured data:** the before-and-after table (355-beaver colony, an hour before the end of the shift, from
  the saves: penalty 15 → 0, within 3 h 117 → 0, median working-hour trip 0.96 h → 0.72 h, late-shift trips
  49 → 21, walking past a closer storage 2 of 18 → 0 of 11), and in TESTING.md: the 33-day averages (49
  just-in-time, 133 pre-fuel, 15 redirects a day; about 8,300 path queries a day on 0.1.0, 7,500 on 0.2.0), the
  day 316–318 comparison of 0.1.0 and 0.2.0, and food-coverage figures (median adult 84 tiles from stocked food).
  Scripts that produced them: `tools/analysis/*.py`. Any chart must be drawn from these numbers exactly.
- Real log line formats (README, install and troubleshooting pages). The sample `Day N` line's numbers are
  illustrative, and are labeled so.
- **Does not exist and must not be faked:** any in-game screenshot or clip (of beavers eating, the colony, the mod
  manager entry, or the switch-off dialog, which has never been seen in a game); any before/after image; Iron Teeth
  or MultiColony-with-own-hours results; testimonials, player counts, download numbers, Workshop ratings or press.
  Leave marked slots for the maintainer's own shots if a page wants them.

## Product Principles

1. **Show the problem, then the measurement.** Explain why the base game sends beavers far (zero-before-leaving,
   score-before-distance) and back the fix with the real colony's numbers, never adjectives.
2. **Working hours only, the game does the rest.** Say clearly what is untouched: evenings, pathfinding, saves,
   consumption per day. That is what makes it safe to try.
3. **Install right, confirm in one line.** The folder layout, Harmony, and the `Hooks installed (5/5)` / `buffer 3h`
   check are impossible to miss.
4. **Same version, same settings file, every player.** Co-op safety is a condition, not a promise; the
   `Simulation settings:` line and the switch-off recovery (host saves and rehosts, everyone joins) stay prominent.
5. **Honest about what's been played.** One colony, 36 days, on 0.1.0 and 0.2.0; the current release not yet. Said
   plainly, without scaring anyone off, and never with a made-up claim.
