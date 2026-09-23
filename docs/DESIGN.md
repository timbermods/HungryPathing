---
name: Hungry Pathing site
description: The works canteen at a timber mill; enamel signs, a chalk slate board and a timber frame on a works-yard ground.
colors:
  cobalt: "#1d4a86"
  cobalt-deep: "#143663"
  enamel: "#efe8d6"
  enamel-ink: "#1b2a3e"
  slate: "#23292d"
  chalk: "#ece9e1"
  chalk-muted: "#b8bcb8"
  coral: "#e8735c"
  water: "#7cc3e6"
  frame: "#5a4331"
  yard: "#e3ddd0"
  ink: "#1e2226"
  muted: "#4d5358"
  rule: "#c8bfab"
  band: "rgba(30, 34, 38, .05)"
  link-hover: "#133566"
  coral-ink: "#a8432b"
  water-ink: "#1f6690"
  yard-night: "#191b1d"
  ink-night: "#ebe6dc"
  muted-night: "#aeb0aa"
  rule-night: "#363a3d"
  band-night: "rgba(235, 230, 220, .04)"
  link-night: "#93b9ee"
  link-hover-night: "#bcd4f5"
  coral-ink-night: "#f29077"
  water-ink-night: "#8fcdeb"
typography:
  display:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "clamp(3rem, 7vw, 5.4rem)"
    fontWeight: 800
    lineHeight: 1
    letterSpacing: ".005em"
  headline:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "clamp(2.1rem, 4.2vw, 3.1rem)"
    fontWeight: 800
    lineHeight: 1
    letterSpacing: ".005em"
  pitch:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "clamp(1.6rem, 3vw, 2.2rem)"
    fontWeight: 800
    lineHeight: 1.05
    letterSpacing: ".01em"
  sign-title:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "1.7rem"
    fontWeight: 800
    lineHeight: 1
    letterSpacing: ".03em"
  title:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "1.55rem"
    fontWeight: 700
    lineHeight: 1.05
    letterSpacing: ".005em"
  tally-figure:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "1.9rem"
    fontWeight: 800
    lineHeight: 1
    fontFeature: "\"tnum\""
  label:
    fontFamily: "\"Big Shoulders Display\", \"Arial Narrow\", sans-serif"
    fontSize: "1.15rem"
    fontWeight: 700
    lineHeight: 1.2
    letterSpacing: ".03em"
  body:
    fontFamily: "system-ui, -apple-system, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif"
    fontSize: "1.0625rem"
    fontWeight: 400
    lineHeight: 1.62
  mono:
    fontFamily: "ui-monospace, \"Cascadia Mono\", \"SFMono-Regular\", Consolas, \"Liberation Mono\", monospace"
    fontSize: ".9em"
    fontWeight: 400
    lineHeight: 1.5
rounded:
  keyline: "2px"
  tag: "3px"
  plate: "4px"
  disc: "50%"
spacing:
  gutter: "clamp(16px, 4vw, 32px)"
  wrap: "1160px"
  section: "clamp(56px, 8vw, 100px)"
  frame: "10px"
  sign-gap: "22px"
  grid-gap: "24px"
  column-gap: "clamp(24px, 5vw, 56px)"
  row: "16px"
components:
  button-primary:
    backgroundColor: "{colors.cobalt}"
    textColor: "{colors.enamel}"
    typography: "{typography.label}"
    rounded: "{rounded.plate}"
    padding: "0 24px"
    height: "52px"
  button-primary-hover:
    backgroundColor: "{colors.cobalt-deep}"
    textColor: "#ffffff"
  button-secondary:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.plate}"
    padding: "0 24px"
    height: "52px"
  button-secondary-hover:
    backgroundColor: "{colors.band}"
  button-small:
    padding: "0 14px"
    height: "44px"
  header:
    backgroundColor: "{colors.cobalt}"
    textColor: "{colors.enamel}"
    height: "64px"
  nav-link:
    textColor: "#d6dff0"
    typography: "{typography.label}"
    padding: "0 12px"
    height: "44px"
  nav-link-current:
    textColor: "#ffffff"
  sign:
    textColor: "{colors.enamel-ink}"
    typography: "{typography.body}"
    padding: "4px 6px 8px"
  sign-after-hours:
    backgroundColor: "{colors.slate}"
    textColor: "{colors.chalk}"
    padding: "22px 24px"
  board:
    backgroundColor: "{colors.slate}"
    textColor: "{colors.chalk}"
    padding: "18px 18px 16px"
  tally:
    backgroundColor: "{colors.slate}"
    textColor: "{colors.chalk}"
    padding: "clamp(18px, 3vw, 30px)"
  step-disc:
    backgroundColor: "{colors.cobalt}"
    textColor: "{colors.enamel}"
    rounded: "{rounded.disc}"
    size: "36px"
  code-block:
    backgroundColor: "{colors.slate}"
    textColor: "{colors.chalk}"
    typography: "{typography.mono}"
    rounded: "{rounded.keyline}"
    padding: "14px 16px"
  callout:
    backgroundColor: "{colors.band}"
    textColor: "{colors.ink}"
    padding: "18px 20px"
  footer:
    backgroundColor: "{colors.slate}"
    textColor: "{colors.chalk-muted}"
    padding: "40px 0"
---

# Design System: Hungry Pathing site

## Overview

**Creative North Star: "The Works Canteen"**

The site is the canteen yard of a timber mill at the end of a shift. Three materials carry everything: cream vitreous enamel with cobalt borders and lettering for signs and buttons, blue-black slate with chalk for anything measured or timed (the shift board, the before/after tally, log lines, the footer), and a dark timber frame that holds the slate. They sit on a works-yard ground of wood chips: sawdust-grey by day, sooty timber-shed dark by night. Light and dark follow the system setting; there is no toggle.

The enamel and the slate are fixed materials: they look the same in both themes. Only the yard, the ink written directly on it, its rules and links change with the light. Times are always given as shift clock times or hours ("leaves 9:30", "3:00 left", "0.96 h"), and the one board holds every time in one gaze. Every sign has one job and one fixed layout.

Texture is produced raster, never CSS imitation: `slate.webp`, `yard-day.webp`, `yard-night.webp` (512px repeating tiles) and the 96px `enamel.png` nine-slice all come from `assets/textures/make_textures.py` (numpy and Pillow, fixed seed, no source images). Big Shoulders Display (700 and 800, OFL) is self-hosted in `assets/fonts/`.

**Key Characteristics:**
- Three fixed materials (enamel, slate, timber frame) on a theme-following yard ground
- Condensed industrial sign lettering over a plain system body face
- Chalk colours with jobs: coral for hunger, the buffer and the penalty; water blue for the walk and the board's control
- Rules and ruled rows instead of cards; the only panels are enamel signs and framed slate
- Every time is a clock time or an hour count

## Colors

A cobalt-and-cream enamel pair and a slate-and-chalk pair are fixed across themes; the yard, its ink and its rules swap between a sawdust day and a soot night.

### Primary
- **Sign Cobalt** (cobalt): the enamel's border and lettering. It fills the sticky header, primary buttons, step discs, check marks, sign headings, the open FAQ rule and the "Played" column rule. It is also the light-theme link and focus colour.
- **Deep Cobalt** (cobalt-deep): a button's outer edge and its hover fill. The light theme's link hover (link-hover) is a near twin.

### Secondary
- **Hunger Coral** (coral): the mod's own logo colour, in chalk. On slate it marks the three-hour buffer (at 16% fill with a dashed edge) and the penalty stroke. On the header it is the underline under the current page. On the yard it is only used through coral-ink (by day) and coral-ink-night, for the hero pitch line.
- **Water Blue** (water): the walk on the board (dashed), the leave drop line, the slider's accent, the output readout, links inside the tally, and the footer focus ring. Water-ink and water-ink-night are its yard-legible forms.

### Neutral
- **Vitreous Cream** (enamel): the sign plate, the text on cobalt, the selection text, and the header's bottom rule.
- **Enamel Ink** (enamel-ink): body text on a sign plate.
- **Blue-Black Slate** (slate, with `slate.webp`): the board, the tally, the "After the whistle" notice, code blocks, log lines to confirm, and the footer.
- **Chalk** (chalk) and **Worn Chalk** (chalk-muted): primary and secondary writing on slate. Chalk rules on slate are chalk at 14% to 50% alpha, usually dashed.
- **Timber Frame** (frame): the 10px frame around the board and tally, the 8px frame around the after-hours notice, and the footer's top border.
- **Sawdust Yard** (yard, with `yard-day.webp`) and **Soot Shed** (yard-night, with `yard-night.webp`): the page ground.
- **Yard Ink** (ink / ink-night), **Faded Ink** (muted / muted-night), **Yard Rule** (rule / rule-night): text, secondary text and hairlines on the yard. Heavy rules (the page head, section tops, table heads) use ink.
- **Band** (band / band-night): a faint wash for inline code, callouts and the secondary button's hover.
- Links on the yard at night are link-night with link-hover-night as the hover.

### Named Rules
**The Fixed Materials Rule.** Enamel, slate, chalk, coral, water and frame keep the same values in both themes. Only the yard tokens (yard, ink, muted, rule, band, link, coral-ink, water-ink) change with `prefers-color-scheme`.

**The Chalk Has Jobs Rule.** Coral means hunger or its penalty, water means the walk or the control. Neither is decoration, and neither is used as a raw fill on the yard. There, text uses coral-ink or water-ink.

## Typography

**Display Font:** Big Shoulders Display 800 and 700 (with "Arial Narrow", sans-serif)
**Body Font:** system-ui stack
**Label/Mono Font:** ui-monospace stack ("Cascadia Mono", Consolas) for code, config and log lines

**Character:** Big Shoulders is condensed, industrial sign lettering. It is used for every heading, the brand, nav links, buttons, sign titles, table heads, the board's labels and the tally's figures. The body is the reader's own system face, so long guide text reads plainly.

### Hierarchy
- **Display** (800, clamp(3rem, 7vw, 5.4rem), 1): the home h1. The guide page heads use clamp(2.8rem, 6vw, 4.2rem).
- **Headline** (800, clamp(2.1rem, 4.2vw, 3.1rem), 1): section h2 on the home page. In guide prose h2 steps down to clamp(1.9rem, 3.4vw, 2.5rem), and troubleshooting issues use clamp(1.6rem, 3vw, 2.1rem).
- **Pitch** (800, clamp(1.6rem, 3vw, 2.2rem), 1.05): the single coral-ink line under the h1.
- **Sign title** (800, 1.7rem, uppercase, .03em): enamel sign headings only.
- **Title** (700, 1.55rem, 1.05): h3.
- **Tally figure** (800, 1.9rem, tabular; 1.6rem on phones): the measured numbers.
- **Label** (700, 1.15rem, .03em): table heads, the board's control label, TOC heading (1.25rem), dt terms (1.15 to 1.35rem).
- **Body** (400, 1.0625rem, 1.62): all running text. The lead is 1.1rem at 56ch; guide prose is capped at 760px; section intros are 1.1rem in muted.
- **Mono** (.9em): inline code on the band wash, and code blocks on slate.

### Named Rules
**The Lettering Rule.** Big Shoulders appears only in its two shipped weights (700 and 800). Uppercase is reserved for enamel sign titles. Headings use sentence case.

**The Clock Rule.** Durations and moments are written as clock times or hour counts, in tabular figures wherever they are compared.

## Layout

A single 1160px wrap with a fluid gutter (clamp(16px, 4vw, 32px)). Home sections are separated by a 2px ink rule and padded clamp(56px, 8vw, 100px) vertically. Each section opens with a head capped at 760px. The hero is a two-column grid (.92fr / 1.08fr): text on the left, the slate board on the right. The signs are a 3-column grid (2 columns at 960px or below, 1 column at 720px or below) with 22px gaps. Notes, the status grid and the install steps are 2- or 3-column grids that collapse at 860px.

Guides (install, troubleshooting) use a 220px sticky table of contents beside a 760px prose column. The TOC goes static and 2-column at 860px, then 1-column at 720px. The FAQ uses a single prose column with expand and collapse tools. Content is organised by ruled rows: the facts list, the compatibility sheet (a 200px term column), troubleshooting issues (a 120px term column) and FAQ items.

At 720px or below: the header stops being sticky and its nav wraps below the brand. The board and tally bleed to the screen edges, with the frame kept only top and bottom (8px). The tally restacks each row as a heading plus two labelled figures. Scroll padding is 88px on desktop to clear the sticky header and 16px on phones.

## Elevation & Depth

Flat. Depth comes from material and framing, not light: slate sits in a timber frame, and enamel is a raster plate with its own printed border. The yard ground is a low-contrast texture. Nothing floats.

### Shadow Vocabulary
- **Enamel keyline** (`box-shadow: inset 0 0 0 2px var(--cobalt), inset 0 0 0 3px var(--enamel)`): the thin cream line inside a cobalt button or step disc, like the inner line of an enamel sign.
- **Slate seat** (`box-shadow: inset 0 0 0 1px rgba(0,0,0,.5), 0 1px 0 rgba(0,0,0,.25)`): the board only, seating the slate in its frame.

### Named Rules
**The No Offset Rule.** No drop shadows, offset shadows, glows or CSS bevels. The header is a flat cobalt strip with a 3px cream bottom rule.

## Shapes

Mostly square. Radii are 2px (code, focus ring), 3px (the header's GitHub tag), 4px (buttons) and full circles (step discs). Frames are thick and square: 10px timber around slate panels. Borders do the structural work: 2px ink rules for section tops and table heads, 1px rules between rows, and dashed chalk rules on slate. Icons are inline SVG or SVG masks (download arrow, check, dash, dotted open circle, chevron), and they take currentColor or a token colour.

## Components

### Buttons
Enamel plates with a keyline, lettered in Big Shoulders.
- **Shape:** gently squared (4px), 52px tall, 2px cobalt-deep border.
- **Primary:** cobalt fill, cream lettering, the enamel keyline inside. An optional 18px inline SVG icon sits before the text.
- **Hover:** cobalt-deep fill, white text, a 1px lift over .15s (cubic-bezier(.2, .8, .2, 1)).
- **Secondary:** transparent with an ink border and no keyline. On hover it gets the band wash.
- **Small:** 44px tall, 14px padding, 1.05rem.
- **Focus:** a 3px outline in the focus colour, offset 3px. On the cobalt header the outline is cream; in the footer it is water blue.

### Navigation
- **Header:** a sticky cobalt strip, 64px tall, with a 3px cream bottom rule. The brand is the mod's logo SVG plus the name in Big Shoulders 800 at 1.45rem.
- **Links:** Big Shoulders 700 at 1.15rem in pale cobalt-white, 44px targets. Hover turns them white. The current page is white with a 3px coral underline. GitHub is a small cream-outlined tag.
- **Footer:** slate with a 10px timber top border and a 2-column link list in chalk, with 44px targets.

### Enamel Sign
The five canteen rules. A nine-slice of `enamel.png` (`border: 24px solid transparent; border-image: url(enamel.png) 24 fill round`) gives the cream plate a cobalt border and chipped iron corners. The heading is an uppercase cobalt sign title; the text is enamel-ink at .98rem. Each sign is one rule with one heading and one paragraph.

### After the Whistle Notice
The sixth tile in the sign grid is deliberately not enamel. It is a chalk notice on slate in an 8px timber frame, with a chalk heading and worn-chalk text, because it describes what the mod does not touch.

### The Slate Board (signature)
One shift drawn in chalk in an inline SVG (640x372 viewBox), in a 10px timber frame on textured slate. The pieces:
- The three-hour buffer: a coral band at 16% with a dashed coral edge.
- Faint chalk gridlines and a worn-chalk axis, with the whistle marked at 16h.
- The mod's line in solid chalk (3.4px).
- The walk in dashed water blue.
- Each meal as a chalk arrow. How far the bar refills is not drawn.
- The game's own timing as a dotted line (stroke-dasharray 1 8), running into a thick coral penalty stroke.
- An event row under the axis for "leaves H:MM" and "penalty".

An SVG turbulence filter roughens the chalk strokes. A range control (15 to 180 minutes in 15-minute steps, water accent) recomputes the plan through `assets/shift.js`, and a polite live region restates it in words. The page ships with the 60-minute state already drawn, and the control stays hidden until the script runs. Marks jump; nothing animates. A figcaption labels it as an illustration.

### Chalk Tally
The measured before/after table on framed slate. The heads use the label style in worn chalk. The figures use the tally-figure style: worn chalk for "without the mod", chalk for "with the mod". Rows are divided by dashed chalk rules. On phones it bleeds to the screen edges and each row becomes a heading plus two figures, each labelled from `data-label`.

### Steps
A counter in a 36px cobalt disc with the enamel keyline, lettered in Big Shoulders. On the home page the steps form a 3-column grid under 2px ink rules, with the disc above each step. In guide prose they form a single column with the disc beside each step (52px indent).

### Lists, Sheets and Callouts
- **Check lists:** cobalt SVG ticks for "supported" and "played"; muted dashes for "not included"; muted dotted circles for "not yet".
- **Sheets:** ruled term and definition rows with display-font terms.
- **Callouts:** a 2px ink border (cobalt for notes) on the band wash.
- **Confirm lines:** log lines to check, set as mono chalk on a slate strip, with the explanation below in muted.

### FAQ Items
Native `details` elements separated by rules. Each summary is at least 56px tall, with an SVG chevron that rotates 180deg over .2s. An open item gets a 3px cobalt top rule.

## Do's and Don'ts

### Do:
- **Do** put anything measured or timed on slate in chalk, inside a timber frame (10px, or 8px on phones and on the notice).
- **Do** make every enamel sign from the `enamel.png` nine-slice. Give it one heading and one short paragraph.
- **Do** write times as clock times or hours, with tabular figures where they are compared.
- **Do** let light and dark follow `prefers-color-scheme` alone. Only the yard tokens change.
- **Do** produce new textures with `make_textures.py` and ship each with its provenance.
- **Do** keep touch targets at 44px or more, and the 3px offset focus ring.
- **Do** let the board work without script: draw its default state in the markup, and use script only to reveal and drive the control.

### Don't:
- **Don't** add offset or drop shadows, glows, radial washes or CSS bevels. Depth is frame and material.
- **Don't** use coral or water as decoration, or as raw text on the yard.
- **Don't** use pill chips, identical numbered cards or the teal-and-gold palette. Those belonged to the borrowed look this world replaced.
- **Don't** add a theme toggle.
- **Don't** use Timberborn game art or faked screenshots. The board is an illustration and says so.
- **Don't** load Big Shoulders weights other than 700 and 800, or set body text in it.
