# Phase 20: Custom Toggle-Switch Control - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-10
**Phase:** 20-custom-toggle-switch-control
**Areas discussed:** Size & prominence, Label handling, On/off mapping & colors, Hover/press feedback

---

## Size & Prominence

| Option | Description | Selected |
|--------|-------------|----------|
| Full-width pill | Same 288px width as today's btnToggle, whole pill is the click target | |
| Compact switch + label row | ~50-60px switch inline with a "Rig Mode" text label, like a Settings toggle | ✓ |
| You decide | Claude picks proportions during planning | |

**User's choice:** Compact switch + label row.
**Notes:** Established the "Settings toggle row" mental model that shaped the rest of the discussion.

| Option | Description | Selected |
|--------|-------------|----------|
| Rounded-rect pill | Standard toggle-switch silhouette, fully rounded ends, circular sliding thumb | ✓ |
| Rectangular track, slight corner rounding | Blocky track closer to the app's existing flat-rectangle aesthetic | |

**User's choice:** Rounded-rect pill.

| Option | Description | Selected |
|--------|-------------|----------|
| Label left, switch right | Matches Settings-row convention (Windows Settings, iOS, GitHub Desktop) | ✓ |
| Switch left, label right | Interactive element leads | |
| Label above, switch centered below | Stacked vertically, more vertical space | |

**User's choice:** Label left, switch right.

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — whole row is clickable | Clicking anywhere in label+switch row toggles | ✓ |
| No — only the switch control itself | Only the track/thumb area responds to clicks | |

**User's choice:** Whole row is clickable.

---

## Label Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Static "Rig Mode" | Fixed label naming what the switch controls, like a Settings row | ✓ |
| Dynamic "Switch to Rig/Normal Mode" | Keeps today's verb-based phrasing, re-renders each toggle | |
| Dynamic current-state "Rig Mode" / "Normal Mode" | Names current state, updates on toggle | |

**User's choice:** Static "Rig Mode".

| Option | Description | Selected |
|--------|-------------|----------|
| Remove lblMode, switch label replaces it | Avoids showing mode twice on one small window | ✓ |
| Keep both | lblMode stays larger/more prominent above tiles, switch label secondary | |
| You decide | Claude picks during planning based on final layout | |

**User's choice:** Remove lblMode.
**Notes:** Raised a follow-up: the existing "Unknown" mode state (DISPLAY-11, mode file missing/corrupted) currently relies on lblMode ("Mode: Unknown") — removing it needed a replacement signal.

| Option | Description | Selected |
|--------|-------------|----------|
| Indeterminate switch position | Thumb centered/mid-track, neutral gray, distinct from both on/off | ✓ |
| Keep lblMode just for the Unknown case | Reintroduces slight redundancy but only in this rare/error state | |
| You decide | Claude designs the indeterminate visual during planning | |

**User's choice:** Indeterminate switch position.
**Notes:** Must remain unmistakably distinct per DISPLAY-11's "never guess" rule — captured as D-07.

---

## Hover/Press Feedback

| Option | Description | Selected |
|--------|-------------|----------|
| Copy existing hover/press pattern exactly | Reuse Phase 19's Identify/Settings owner-draw color-shift + focus ring | ✓ |
| You decide | Claude adapts the treatment for a switch shape during planning | |

**User's choice:** Copy existing pattern exactly.

---

## On/Off Mapping & Colors

| Option | Description | Selected |
|--------|-------------|----------|
| Rig = on | Thumb right/filled in Rig mode; Normal is neutral/off/left | ✓ |
| Normal = on | Inverted mapping | |

**User's choice:** Rig = on.
**Notes:** Normal is described everywhere else in the project as the baseline/default state, so it maps naturally to "off."

| Option | Description | Selected |
|--------|-------------|----------|
| Same AccentColor placeholder as tiles | Reuses `Color.FromArgb(0,90,158)` dark / `SystemColors.Highlight` light — same property that becomes live accent color in Phase 21 | ✓ |
| You decide | Claude picks a specific on/off color pair during planning | |

**User's choice:** Same AccentColor placeholder as tiles.

| Option | Description | Selected |
|--------|-------------|----------|
| Neutral gray/outline | Matches Phase 19 D-02 tile OFF convention | ✓ |
| You decide | Claude picks the exact off-state treatment during planning | |

**User's choice:** Neutral gray/outline.

| Option | Description | Selected |
|--------|-------------|----------|
| White/contrasting circle | Standard toggle-switch thumb convention (Windows, iOS, Android) | ✓ |
| You decide | Claude picks the thumb treatment during planning | |

**User's choice:** White/contrasting circle.

---

## Claude's Discretion

- Exact pixel dimensions of the compact switch (within ~50-60px) and row height/spacing.
- Exact indeterminate-state geometry (thumb centering, gray shade) for D-07.
- Exact hover/press color deltas adapted for the switch's track/thumb shape.
- Whether the row's vertical position shifts to fill the space `lblMode`'s removal frees up.

## Deferred Ideas

None — discussion stayed within phase scope. Toggle-switch slide animation was already out of scope per REQUIREMENTS.md before discussion started. Accent-color live-following (THEME-07) was explicitly deferred to Phase 21.
