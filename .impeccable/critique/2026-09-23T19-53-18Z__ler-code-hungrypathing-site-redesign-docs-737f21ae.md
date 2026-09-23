---
target_identity: "file:C:\\Users\\Kyler\\code\\HungryPathing-site-redesign\\file:C:\\Users\\Kyler\\code\\HungryPathing-site-redesign\\docs"
timestamp: 2026-09-23T19-53-18Z
slug: ler-code-hungrypathing-site-redesign-docs-737f21ae
---
---
target: Hungry Pathing site
total_score: 28
max_score: 40
na_heuristics: 
p0_count: 0
p1_count: 5
---
# Critique: Hungry Pathing site (docs/)
Method: dual-agent (A design review, B site test + detector + browser + audit)
Tests: (1) PARTIAL - h1/lede state timing and closest storage, but "off duty nothing changes", penalty redirects, no pathfinding change and co-op are off the first screen; banner buffer mark drawn at ~30% for a 10% span; (2) PARTIAL - honest status repeated three times with a hand-written "0.3.1", the settings file never named on the overview, co-op and the Simulation settings line buried in a table cell; (3) PASS with gaps - confirm lines not picked out, no sample Day N line, download 1.5 screens down on phone.
Heuristics 28/40 (Good, low end). Cognitive load moderate (6+4 cards, flat hierarchy, internals on the overview).
Priority: [P1] before/after table unreadable on phones (th/td display:block); [P1] version history (switched-off, compat, faq#coop, results/cta/install callout); [P1] first screen missing trust facts; [P1] anchor jumps hide headings under a 134px phone header; [P1] light-mode focus ring 1.58:1; [P2] borrowed PWA identity and dead demo CSS, mod coral absent; [P2] overview long and flat, status repeated x4; [P2] confirm lines not picked out; [P2] og:image SVG; [P2] tap targets <44px; [P3] 404 bare, dark-only theme-color, inline styles, tables without captions, faint light borders.
Site test: 37/37 (stub DOM subset for local scripts; download/preview/tag rules; relative asset paths; well-formed HTML).
Perf: ~46 KB home, no fonts, CLS 0.
