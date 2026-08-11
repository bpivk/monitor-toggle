---
status: resolved
trigger: "Light-mode button contrast: several buttons blend into the main-screen background because they're the same color as the background."
created: 2026-08-11
updated: 2026-08-11T21:00:00Z
---

# Debug Session: light-mode-buttons-blend-into

## Symptoms

**Expected behavior:** In light/white mode, all buttons (Identify, Settings, Discard Changes, Browse, Clear) should be visually distinguishable from the surrounding background, the way the Settings button already reads correctly.

**Actual behavior:** In light/white mode, several buttons blend into the background because they render the same color as the background:
- MainForm: the **Identify** button
- SettingsForm: the **Discard Changes**, **Browse**, and **Clear** buttons

The **Settings** button (MainForm) does NOT have this problem — it reads correctly against the background in light mode. User's framing: these buttons "need a black [outline/border]... like the same settings button does."

**Error messages:** None — this is a pure visual/contrast defect, not a crash or exception.

**Timeline:** Pre-existing — user confirmed this has "always been there," not a regression introduced by Phase 20 (toggle switch) or Phase 21 (accent color reading). Only noticed now during the Phase 21 rig-verification session on real Windows 11 hardware in light mode.

**Reproduction:**
1. Set Windows to light/white theme (Settings > Personalization > Colors > Choose your mode > Light).
2. Launch RigToggle — observe the **Identify** button on MainForm.
3. Open Settings — observe the **Discard Changes**, **Browse**, and **Clear** buttons.
4. Compare against the **Settings** button on MainForm, which reads correctly.

## Context

- Project: Rig Toggle (WinForms, .NET 10, Windows-only). This build environment is Linux — no Windows GUI available for visual confirmation of any fix; any candidate fix must be verified by the user on the real rig, or reasoned about from `ThemeApplier.cs`'s existing color-application code (the codebase's established theming pattern, per `ThemeButton`/`ThemeMonitorTile`/`ThemeToggleSwitch`).
- Likely code area: `src/RigToggle.App/ThemeApplier.cs` (per-control theming methods — likely `ThemeButton`, applied to `btnIdentify`, and separately to `btnDiscard`/`btnBrowse`/`btnClear` or similarly named SettingsForm controls) and/or `MainForm.cs`/`SettingsForm.Designer.cs` for how border/flat-style is configured on these specific buttons vs. the working `btnSettings`.
- Not part of Phase 21's scope (accent color) and not fixed by Phase 22's SettingsForm layout pass (spacing/grouping, not color/contrast) per the roadmap — this is its own defect.

## Current Focus

reasoning_checkpoint_round7:
  hypothesis: "The top/left-thicker-than-bottom/right border asymmetry (reported after round 6's double-border fix) is caused by GDI+'s AntiAlias FillPath rasterizer's known top-left-vs-bottom-right coverage bias for rectangle-like paths under the default PixelOffsetMode, not by any code-level geometry asymmetry (already proven symmetric-by-construction in the 2026-08-11T20:30:00Z Evidence entry) or any second border-drawing code path (already ruled out by exhaustive grep of every btnIdentify. reference in the same entry). Setting PixelOffsetMode.Half for the two border FillPath calls shifts GDI+'s pixel-center sampling convention and will make all four edges read as equal thickness."
  confirming_evidence:
    - "2026-08-11T20:30:00Z Evidence: outer/inner rect geometry re-derived algebraically and confirmed mathematically symmetric — equal borderInset margin on all four sides by construction, ruling out a logic-level asymmetry."
    - "2026-08-11T20:30:00Z Evidence: exhaustive grep of every `btnIdentify.` reference in MainForm.cs and MainForm.Designer.cs found no other code path (beyond the round 4-6 fill-based border and the already-ruled-out conditional focus ring) that could draw or configure a second, asymmetric border element."
    - "PixelOffsetMode.Half (or HighQuality, which implies it) is Microsoft's own documented remedy for exactly this failure mode — asymmetric anti-aliased rectangle-fill coverage under the classic Win32 top/left-inclusive, bottom/right-exclusive rectangle convention — making this a standard, well-precedented fix rather than a speculative one."
  falsification_test: "If the fresh rig close-up screenshot still shows the top/left edges reading thicker than bottom/right after PixelOffsetMode.Half is applied to the border fills, this hypothesis is wrong — the asymmetry is not a PixelOffsetMode/rasterization-convention artifact, and a genuinely new (fourth, after native-border/width/PixelOffsetMode) mechanism would need to be considered, since geometry and a second border source are both already ruled out."
  fix_rationale: "Addresses the actual rasterization mechanism (GDI+'s pixel-center sampling convention during AntiAlias FillPath coverage computation) directly, rather than papering over the symptom with a manually asymmetric inset correction (which would be a magic-number workaround liable to break again at a different DPI scale or button size, per the round-6 evidence entry's own reasoning). Minimal and scoped: only the two border FillPath calls run under PixelOffsetMode.Half, restored to Default in a finally block immediately after — does not touch round 6's BorderSize=0/!IsDark changes (confirmed correct, do not revisit), ThemeApplier.cs, the text render, or the focus ring."
  blind_spots: "Cannot visually confirm on this Linux environment — still reasoning from GDI+'s documented PixelOffsetMode behavior, not a fresh screenshot showing this exact fix applied. This is the 5th consecutive round requiring rig re-verification for Identify's border rendering specifically. If PixelOffsetMode.Half does not fully resolve the asymmetry, HighQuality (a stronger variant) or a manual asymmetric-inset compensation are both plausible next steps but neither has been tried or approved yet."

reasoning_checkpoint_round6:
  hypothesis: "btnIdentify is a stock System.Windows.Forms.Button (not a UserControl with UserPaint suppressing base rendering), so WinForms' base OnPaint draws the native FlatStyle.Flat border (a SQUARE ring, driven by FlatAppearance.BorderSize/BorderColor, currently BorderSize=1/BorderColor=ControlDark in light mode via ThemeApplier.ThemeButton) BEFORE the Paint event (BtnIdentify_Paint) fires. BtnIdentify_Paint's hand-drawn rounded fill/border is therefore composited ON TOP OF, not INSTEAD OF, that native square border -- and because the hand-drawn shape is rounded while the native one is square, the rounded corners don't fully cover the native border's square corners, exposing two distinct, differently-shaped borders simultaneously (the double-line/mismatched-corner artifact the user described in 6.png). Setting FlatAppearance.BorderSize=0 for btnIdentify specifically (after ThemeApplier.ThemeButton runs) will suppress the native square border render entirely while leaving BorderColor and the hand-drawn code untouched, and decoupling the hand-drawn border's draw condition from the now-forced-zero BorderSize (using IsDark instead) preserves the light-mode-only presence behavior."
  confirming_evidence:
    - "2026-08-11T19:30:00Z Evidence: btnIdentify has a Paint event handler attached but is not a custom owner-drawn control class -- for a stock Button, base.OnPaint (which includes ButtonFlatAdapter's native FlatAppearance border render when BorderSize>0) runs before the Paint event is raised to subscribers, confirmed by cross-referencing WinForms' documented Paint event order and the fact that btnSettings (identical ThemeButton call, identical BorderSize=1 in light mode) never showed this symptom -- because BtnSettings_Paint's fill is a same-shape, same-bounds square FillRectangle that fully masks a square native border, whereas BtnIdentify_Paint's fill/border is deliberately rounded and does not cover the native border's square corner pixels."
    - "User's own detailed description of 6.png ('double lines... upper left portion clearly has a rounded corner as well as a rectangular one... half of one border is overlapping with a normal button border') is a direct, literal description of two differently-shaped borders occupying overlapping-but-not-identical geometry -- exactly what a rounded hand-drawn border on top of an unsuppressed square native border would produce, and not explainable by any single-border width/AA/geometry theory (rounds 1-5, all falsified for this same reason)."
    - "Rounds 1-5 all only ever modified the hand-drawn border's rendering technique (stroke vs fill, width, AntiAlias) and never touched FlatAppearance.BorderSize's effect on the STOCK Button's own native paint path -- this is a genuinely untested code path, not a re-tuning of an already-tried mechanism."
  falsification_test: "If the fresh rig screenshot still shows double lines / mismatched corner shapes after FlatAppearance.BorderSize=0 is applied for btnIdentify (post-ThemeButton) and the hand-drawn border condition is decoupled to !IsDark, this hypothesis is wrong -- the native-border theory would be falsified and a genuinely new (sixth) mechanism would need to be considered, since every single-border-technique theory has now been exhausted."
  fix_rationale: "This targets the actual mechanism identified from direct evidence (native Button base-paint border render, independent of and prior to the Paint event) rather than tuning any parameter of the hand-drawn border technique again (which rounds 1-5 exhaustively did without success for this exact reason -- they were all correctly fixing artifacts of ONE border while a second, entirely different border rendered underneath/around it unexamined). Minimal and scoped: one line in ApplyDashboardTheming (BorderSize=0 for btnIdentify only, after the shared ThemeButton call -- does not change ThemeButton's behavior for any other control including btnSettings and the native SettingsForm buttons), one condition change in BtnIdentify_Paint (decouples presence-of-hand-drawn-border from the now-always-zero BorderSize, using the equivalent IsDark check instead so light-mode-only behavior is preserved byte-for-byte)."
  blind_spots: "Cannot visually confirm on this Linux environment -- still reasoning from source code (WinForms' documented Paint event ordering, ButtonFlatAdapter's known behavior) and the user's verbal description of 6.png, not a fresh screenshot showing this exact fix applied. This is the fourth consecutive round requiring rig re-verification for Identify alone; also unverified whether the fix affects Identify's hover/pressed states, which have not been specifically screenshotted in any round of this session -- proactively requesting that check this round."

reasoning_checkpoint:
  hypothesis: "In light mode, ThemeApplier.ThemeButton fills every flat button with SystemColors.Control and sets FlatAppearance.BorderSize=0 (no border) for BOTH light and dark mode (the BorderSize=0 was only needed to dodge dotnet/winforms#13897, a DARK-mode-only FlatAppearance border bug). Meanwhile MainForm/SettingsForm use standard Mica backdrop (DWMSBT_MAINWINDOW) which in light mode renders as a near-white/very-light-gray tone visually indistinguishable from SystemColors.Control. With no border and fill≈background, a flat button's only differentiator is its drawn content. btnSettings survives because BtnSettings_Paint draws a dense, structured gear-icon glyph (MonitorIconGeometry.DrawGearIcon, using btnSettings.ForeColor) that reads as a distinct shape regardless of the surrounding fill/background match. btnIdentify/btnDiscard/btnBrowse/btnClear only render plain single-weight text in that same ForeColor, which is not enough visual mass to register as a bounded button shape against a matching background — hence 'blends into background.' btnSaveSettings is spared only because it is SettingsForm.AcceptButton (IsDefault=true), which is a coincidental default-button visual, not evidence against this hypothesis."
  confirming_evidence:
    - "ThemeApplier.ThemeButton (ThemeApplier.cs:129-140): FlatStyle.Flat, BackColor=SystemColors.Control in light mode, FlatAppearance.BorderSize=0 unconditionally (both light AND dark) — doc comment (lines 114-123) explains BorderSize=0 exists specifically to dodge dotnet/winforms#13897, a bug the comment itself describes as only manifesting 'once dark mode is active.'"
    - "DwmTitleBar.ApplyRoundedCornersAndMica (RigToggle.Windows/DwmTitleBar.cs:38, DWMSBT_MAINWINDOW=2) — standard Mica backdrop is applied to the window regardless of theme; in light mode this Windows 11 backdrop renders near-white, visually close to SystemColors.Control's face color."
    - "MainForm.cs BtnSettings_Paint (line 1235-1260) fills with the SAME ManualButtonFill/BackColor as BtnIdentify_Paint, then draws MonitorIconGeometry.DrawGearIcon with btnSettings.ForeColor (same ForeColor source/color as btnIdentify's text) — proves the fill/ForeColor values are identical between the working and broken MainForm buttons; the only difference is icon glyph vs. plain text content."
    - "SettingsForm.cs: grep for '_Paint' handlers on btnBrowse/btnClearAppPath/btnDiscardChanges/btnSaveSettings returns nothing — these are plain (non-owner-drawn) WinForms Buttons, so a FlatAppearance.BorderSize>0 set by ThemeButton WOULD render natively (not overwritten by a custom Paint handler), unlike MainForm's btnIdentify/btnSettings which self-paint FillPath/FillRectangle over their full ClientRectangle in BtnIdentify_Paint/BtnSettings_Paint and would erase a native border."
  falsification_test: "If SystemColors.Control were NOT visually close to the light-mode Mica backdrop, plain-text buttons would still show a clear rectangular color patch even without a border, and users would not describe them as blending in. The user's own report ('same color as the background', 'need a black outline... like Settings does') directly matches this hypothesis rather than contradicting it."
  fix_rationale: "Restore FlatAppearance.BorderSize/BorderColor for LIGHT mode only (leave dark mode at BorderSize=0, preserving the #13897 workaround untouched) in ThemeApplier.ThemeButton. This fixes SettingsForm's Browse/ClearAppPath/DiscardChanges directly (no custom paint to erase it). For MainForm's btnIdentify, which self-paints over the native border, add an explicit border stroke inside BtnIdentify_Paint (reading FlatAppearance.BorderColor/BorderSize the same way ManualButtonFill already reads FlatAppearance's other theme colors — one place, one source of truth) so the rounded-rect fill gets an outline in light mode. This targets the actual mechanism (missing shape boundary) rather than the symptom, and is a minimal, additive change with no dark-mode behavior change."
  blind_spots: "Cannot visually confirm color values on this Linux dev environment — no Windows GUI available. SystemColors.Control's exact RGB and the real-world Mica light tone are read from documentation/reasoning, not measured pixels. The chosen border color (SystemColors.ControlDark) is a reasoned choice for contrast, not rig-verified. User must visually confirm on the real Windows 11 rig before this session is archived as resolved."

reasoning_checkpoint_round5:
  hypothesis: "Widening Identify's owner-drawn border inset from 1px to 2px (keeping round 4's fill-based technique unchanged -- two concentric AntiAlias FillPath calls, no Pen) will make the border read as visibly solid/opaque, because GDI+'s AntiAlias coverage-blend region is roughly ~1px wide regardless of stroke-vs-fill technique (round 4's rig result, 2026-08-11T18:30:00Z), so at 1px total width the blend band can consume up to 100% of the border's visible area, while at 2px the same ~1px blend band consumes at most half, leaving the remainder solidly opaque."
  confirming_evidence:
    - "Round 4 rig result (2026-08-11T18:30:00Z Evidence): fill-based border at 1px inset reproduced the identical pressed-down look as the stroke-based rounds 1-2, despite eliminating the stroke mechanism entirely -- proves width, not rendering technique, is the active variable."
    - "Round 3's pixel sampling (2026-08-11T15:00:00Z Evidence) measured the AA blend band at exactly 1-2 pixel rows/columns wide on a 1px-nominal border -- directly supports the ~1px blend-radius estimate this round's math is based on."
    - "This session's own round-4 reasoning_checkpoint confirmed ordinary AntiAlias FillPath on rounded rects renders cleanly elsewhere in this codebase (MonitorTile tile fill, ToggleSwitch track fill) with no reported blend/corner artifacts -- the fill technique itself is not in question, only whether a given margin width is wide enough to have a visibly-solid interior once the AA edges on both the outer and inner path are accounted for."
  falsification_test: "If the fresh rig screenshot still shows the border as an indistinct/blended-into-fill band at 2px (not a visibly wider, partially-solid margin), the AA-blend-ratio hypothesis is wrong and width is not the controlling variable -- the next candidate would be pixel-snapping the fill coordinates to integer device pixels (not yet tried) rather than further width increases (already flagged as the pre-approved next escalation path if 2px fails)."
  fix_rationale: "Introduce a separate, explicit constant (not FlatAppearance.BorderSize, which stays at 1 and is shared with the native-rendered SettingsForm buttons via ThemeApplier) scoped only to BtnIdentify_Paint's owner-drawn inset math. This is the minimal change that tests the width hypothesis without touching ThemeApplier.cs (confirmed-correct native buttons untouched) or the fill-based technique itself (never falsified, per round 4's own falsified-hypothesis entry which blamed width, not method)."
  blind_spots: "Cannot visually confirm on this Linux environment. 2px is a specific untested magnitude -- the ~1px AA blend-radius estimate is inferred from round 3's pixel measurements on a 1px border, not independently re-measured at 2px; the actual blend radius could be larger or smaller in practice. If 2px is still insufficient the user has pre-approved trying 3px next, but that has not been done yet -- only 2px is implemented and verified in this round."

hypothesis: CONFIRMED (round 4 mechanism, round 5 width tuning) — see reasoning_checkpoint_round4/round5 above; fill-based border technique unchanged, only the inset width increases from 1px to 2px, scoped to BtnIdentify_Paint only
test: source-reasoning + build/test only (no Windows GUI in this environment) — awaiting round-5 rig verification
expecting: Identify's border reads visibly solid/opaque (not blended into the fill) on straight edges AND smoothly rounded at corners simultaneously, with no pressed/sunken look and no stair-step artifacts; border may read visually thicker than the native SettingsForm buttons' 1px borders (accepted tradeoff, user pre-approved)

reasoning_checkpoint_round2:
  hypothesis: |
    Two independent, unrelated mechanisms explain the two round-1 regressions:
    (A) Save Settings double border: WinForms' ButtonFlatAdapter draws a native
    "default button" indicator ring (rect inflated by -1,-1, confirmed against the
    dotnetframework.org mirror of ButtonFlatAdapter.cs source) around any Control.IsDefault
    button, independent of FlatAppearance.BorderSize -- this fires because
    SettingsForm.cs:105 sets `this.AcceptButton = btnSaveSettings`, which calls
    Button.NotifyDefault(true). Our round-1 fix added a SECOND explicit FlatAppearance
    border on top of that always-present native ring, producing two concentric rings only
    on the AcceptButton (Discard Changes, not AcceptButton, only gets the one ring and is
    correct).
    (B) Identify "pressed down": BtnIdentify_Paint's border stroke width is computed as
    `bounds.Height * (1/32)` (e.g. ~1.25-1.5px for typical button heights), NOT the
    literal `btnIdentify.FlatAppearance.BorderSize` (1px, the same value the native
    Discard Changes/Browse/Clear borders render with). Combined with
    `SmoothingMode.AntiAlias` and no half-pixel path alignment, a non-integer-width pen
    straddles pixel rows/columns and GDI+ renders it as a soft ~50%-opacity blurred band
    rather than a crisp hard-edged line. A soft gray gradient band around a light fill is
    a classic depth/shading cue (subtle inner-shadow look) -- exactly what reads as
    "pressed down" -- versus the natively-rendered borders, which are crisp non-anti-aliased
    1px hairlines and read as flat outlines. (Re-examined the fill-then-stroke-inset
    geometry itself: worked through the offset math and confirmed the stroke's outer edge
    is mathematically flush with the fill path's true-bounds edge at every corner -- the
    geometry/draw-order is NOT the defect; the pixel-alignment/anti-aliasing mismatch is.)
  confirming_evidence:
    - "dotnetframework.org mirror of ButtonFlatAdapter.cs: 'when Control.IsDefault is true, the rectangle r is inflated by -1, -1' as part of the native flat-button paint path -- confirms a native default-button ring exists independent of FlatAppearance and is unconditional on BorderSize."
    - "Debug Evidence 2026-08-11T00:00:04Z (this session, round 1): btnSaveSettings already read as visually distinct from Browse/Clear/Discard Changes BEFORE any border fix was applied -- back when FlatAppearance.BorderSize was unconditionally 0 for every button in every theme. This proves the native default-button ring renders regardless of BorderSize, so suppressing our explicit border for the AcceptButton loses no visual definition."
    - "MainForm.cs:1187 (current code): `float borderPenWidth = Math.Max(1f, bounds.Height * (1f / 32f));` never reads `btnIdentify.FlatAppearance.BorderSize` at all, despite the method's own comment (line 1184) claiming it reads 'the same values ThemeButton stored on the button' -- confirmed by direct read that this is only true for BorderColor, not width. Non-owner-drawn Discard Changes/Browse/Clear render their border at exactly BorderSize=1 device pixel via native (non-anti-aliased) WinForms painting."
    - "Independently re-derived the border-stroke offset geometry (borderRect inset by borderPenWidth/2, corner radius reduced by the same amount) algebraically: outward Minkowski offset of a rounded-rect path by half the pen width reconstructs exactly the original full-bounds rounded rect at the original corner radius -- the stroke's outer edge is provably flush with the fill's edge, ruling out the round-1 'inset ring' geometry theory as the actual mechanism."
  falsification_test: |
    (A) If btnSaveSettings still shows any visible ring/edge after BorderSize is
    suppressed to 0 for AcceptButton-in-light-mode, that ring must be the native
    default-button indicator alone (single ring) -- if it instead disappears entirely,
    this hypothesis is wrong and the native ring theory is falsified.
    (B) If pinning borderPenWidth to the literal FlatAppearance.BorderSize (1, an
    integer) with a half-pixel-aligned inset still reads as "pressed" on the real rig,
    the anti-aliasing/soft-blur theory is falsified and the round-1 fill-then-stroke
    draw-order theory (or a third mechanism) needs to be revisited.
  fix_rationale: |
    (A) Suppress ThemeApplier.ThemeButton's explicit light-mode border only when the
    button is currently its form's AcceptButton (`ReferenceEquals(button.FindForm()?.AcceptButton, button)`)
    -- fixes the root mechanism (double ring from two independent border sources) at the
    one shared call site, rather than patching SettingsForm.cs alone; this also
    preemptively fixes the identical latent bug on MonitorConfirmDialog's btnContinue
    (also FlatStyle.Flat + AcceptButton, themed via the same ThemeButton call, not yet
    reported only because the user's rig session didn't exercise that dialog in light
    mode this round) without adding a second special case.
    (B) Change BtnIdentify_Paint to read `btnIdentify.FlatAppearance.BorderSize` (the
    literal int ThemeButton set, same source of truth already used for BorderColor)
    instead of a height-derived float, and inset by exactly half that integer width so a
    1px anti-aliased pen lands crisply on a single pixel row instead of straddling two --
    removes the width mismatch and the anti-aliasing softness without changing the
    already-correct fill, hover/press colors, or focus-ring code.
  blind_spots: |
    Still cannot visually render WinForms on this Linux environment. The anti-aliasing/
    half-pixel-offset theory for (B) is standard GDI+ convention (well documented) but
    unverified pixel-for-pixel on the real Mica-backdrop rig; if wrong, the true
    mechanism may be something not yet considered (e.g. DPI scaling interaction, or
    Windows' own default-button-adjacent focus cue). Requires user re-verification on
    real hardware with fresh screenshots before this session can be archived.

reasoning_checkpoint_round3:
  hypothesis: |
    BtnIdentify_Paint's border stroke renders as an asymmetrically-blended (top/left
    2px-soft, bottom/right 1px-lighter) line because SmoothingMode.AntiAlias is active for
    its DrawPath call, whereas the sibling SettingsForm buttons' native ButtonFlatAdapter
    border is drawn via non-anti-aliased GDI (always fully opaque, single hard pixel row).
    This asymmetric coverage pattern IS, pixel-for-pixel, the classic Windows "sunken/pressed"
    3D bevel convention (dark top-left, light bottom-right) -- causing the user's "looks like
    it's pressed down" perception as a literal, measurable rendering artifact rather than a
    vague impression.
  confirming_evidence:
    - "Direct RGB pixel sampling of 2.png (Discard Changes, native border): entire border is
      pure, uniform (160,160,160), zero blended pixels -- proves 160 is the true rendered
      SystemColors.ControlDark value on this rig and that native rendering is fully opaque."
    - "Direct RGB pixel sampling of 1.png (Identify, self-painted border): top edge 2px
      (190,190,190)+(180,180,180); left edge 2px (185,185,185)+(190,190,190); bottom edge 1px
      (200,200,200); right edge 1px (200,200,200) -- every measured pixel is a blend between
      160 (true border) and 240 (fill/background, confirmed identical), none reach pure 160."
    - "The asymmetry is directional and consistent (top+left darker/wider vs bottom+right
      lighter/narrower on the SAME shape with the SAME nominal border color/width on all four
      sides) -- not a uniform blur, which rules out a simple 'pen too thin' explanation and
      instead matches exactly the visual convention Windows uses for sunken/pressed controls."
    - "BtnIdentify_Paint (MainForm.cs:1167) sets e.Graphics.SmoothingMode = AntiAlias for the
      whole method, including the border DrawPath call (line 1207) -- confirmed by direct
      code read this is the only rendering difference between Identify's border path and the
      sibling buttons' native (non-anti-aliased) GDI border rendering, given round 2 already
      proved the pen width/geometry are otherwise identical/flush."
  falsification_test: |
    If the border pixels remain blended/non-uniform (not matching the native buttons' pure
    160,160,160) after SmoothingMode is set to None specifically for the border DrawPath call,
    this hypothesis is wrong and the true mechanism is something else (e.g. an OS-level DPI
    bitmap-stretch affecting the whole window, which would need to be ruled out by checking
    whether the native borders in the SAME screenshot also show any blending -- they do not,
    which already argues against a global stretch explanation).
  fix_rationale: |
    Set e.Graphics.SmoothingMode = SmoothingMode.None immediately before the border DrawPath
    call, then restore SmoothingMode = SmoothingMode.AntiAlias immediately after (before the
    text render and the conditional focus-ring DrawPath, both of which are unaffected/still
    want smoothing -- TextRenderer.DrawText is GDI-based and ignores Graphics.SmoothingMode
    entirely; the focus ring is a temporary overlay, not reported broken, left unchanged).
    This forces GDI+'s aliased (non-coverage-blended) rasterizer for the border stroke only,
    producing a fully-opaque single-pixel-row/column line at the exact border color -- the
    same rendering characteristic already confirmed (via pixel sampling) for the native
    buttons' borders. This targets the confirmed mechanism (AntiAlias coverage blending
    specifically on the border stroke) directly, changing nothing about the fill's smooth
    rounded corners or the border's color/width/geometry (all already correct per round 2).
  blind_spots: |
    Disabling AntiAlias for the border stroke will make the border's rounded corners
    (currently a smooth 4px radius, reduced to ~3.5px for the inset border path) render
    aliased/stair-stepped instead of smooth, since the SAME DrawPath call strokes both the
    straight edges and the corner arcs -- cannot selectively alias only the straight
    segments and antialias only the corners with GDI+ within a single DrawPath call. At a
    4px corner radius this is likely a very minor/subtle visual change (a small number of
    corner pixels), but it is a real, unverified tradeoff on the actual rig. Still cannot
    render WinForms in this Linux environment -- this fix is deduced from directly-measured
    pixel evidence rather than rig-observed, so it requires human-verify confirmation before
    the session can be archived, same as rounds 1 and 2.

next_action: |
  ROUND 3. Save Settings is CONFIRMED FIXED by the user — do not touch that code path again
  (SettingsForm.cs AcceptButton-suppression logic in ThemeApplier.ThemeButton is done).

  Identify is STILL broken — confirmed via direct pixel comparison of two user screenshots
  (1.png round-1 vs 4.png round-2, both active/focused MainForm, same framing): visually
  identical, and the numeric math proves round 2's pen-width change was a no-op at
  IdentifyHeightPx=32 (both formulas evaluate to 1.0 at 100% DPI). The width/anti-aliasing
  hypothesis from round 2 is now falsified (see Eliminated). The "should look like Settings"
  framing is also retired as a target — Settings has zero border at rest; its apparent
  distinctness in every screenshot is an artifact of incidentally holding default keyboard
  focus (DrawButtonFocusRing), not a border to emulate.

  Fresh, un-tried angles for round 3:
  - The four confirmed-CORRECT SettingsForm buttons (Discard Changes/Browse/Clear/Save
    Settings) all render their border via WinForms' NATIVE FlatStyle.Flat pipeline
    (ButtonFlatAdapter, GDI, pixel-snapped, non-anti-aliased). Identify's border is entirely
    hand-drawn GDI+ (BtnIdentify_Paint, e.Graphics.DrawPath with SmoothingMode.AntiAlias set)
    on a ROUNDED-RECT path (BuildRoundedRect, cornerRadius=4px — this rounding is INTENTIONAL,
    a deliberate prior design decision per the comment at MainForm.cs:1121-1126 unifying
    Identify's shape with the tile/toggle-switch visual language; do not remove the rounding).
    The defect is most likely in HOW the GDI+ stroke renders relative to the native GDI
    stroke — anti-aliasing softness/color-blending against the fill underneath, even at
    identical nominal width/color — not WHAT width or color value is used (both already
    proven identical to the working buttons in round 2's investigation, still true after
    the no-op finding).
  - Consider: does the fill underneath (ManualButtonFill, filled via FillPath over the FULL
    rounded bounds, drawn BEFORE the border stroke) show through partially under the
    anti-aliased border pen, softening its edge into the fill and creating a "shaded"/beveled
    look distinct from a native hard-edged border? Try pixel-snapping the border path's
    coordinates to integer device pixels before stroking, or temporarily disabling
    SmoothingMode.AntiAlias for just the border DrawPath call (re-enabling it after, if the
    text/fill still need it), to see whether a hard-edged stroke resolves the "pressed" look
    while keeping the rounded corners the fill already has.
  - Also worth double-checking _identifyHovered/_identifyPressed aren't somehow stuck/leaking
    into the idle-state screenshots (ManualButtonFill would return MouseDownBackColor if
    _identifyPressed were incorrectly true at rest) — ruled out by code reading so far but not
    yet explicitly proven false at runtime.

  This environment has no Windows GUI — any round-3 fix must again go through a human-verify
  checkpoint. Consider asking the user for one more data point BEFORE writing more code if the
  next hypothesis isn't strongly evidenced: e.g. a maximally zoomed-in crop of just the Identify
  button border/corner area, since GDI+ anti-aliasing artifacts are exactly the kind of subtle
  pixel-level detail a full-window screenshot may not resolve clearly enough to diagnose from
  text description alone.

  ROUND 3 RESULT: did not need to ask for a new screenshot -- read raw pixel RGB values directly
  out of the EXISTING 1.png/2.png files with PIL (image files are readable in this Linux
  environment even though WinForms itself cannot run). Found hard, measured evidence: Identify's
  border blends asymmetrically toward the fill (top/left ~185-190 over 2px, bottom/right ~200
  over 1px) while the native sibling buttons' border is a pure, fully-opaque 160 with zero
  blending. That top-left-darker/bottom-right-lighter asymmetry is the literal Windows
  sunken/pressed 3D bevel convention. Root cause: e.Graphics.SmoothingMode = AntiAlias applied to
  the border DrawPath call causes GDI+'s coverage-based rasterizer to blend the stroke instead of
  rendering it as a hard opaque line. Fix applied: SmoothingMode.None set immediately around only
  the border DrawPath call in BtnIdentify_Paint, restored to AntiAlias immediately after (fill and
  focus ring unaffected). See reasoning_checkpoint_round3 above and Resolution below.
  Build clean, 82/82 tests pass. AWAITING RIG VERIFICATION.

  ROUND 3 RIG RESULT: partial success, new artifact. Straight-edge blending is fixed (border
  reads solid/opaque now, confirmed by user), but disabling AntiAlias for the whole border
  DrawPath call (which includes BuildRoundedRect's 4px-radius corner arcs) also disabled
  smoothing on those curves — user-provided close-up screenshot 5.png shows stair-step/notch
  artifacts at the corners ("upper left corner has some kind of rounding, right has a few
  pixels and bottom left as well"). SmoothingMode.None was too blunt: it can't give crisp
  straight edges AND smooth curved corners in a single Pen/DrawPath call. See Eliminated for
  the falsified hypothesis and the 2026-08-11T17:00:00Z Evidence entry for the full analysis,
  including a check against this codebase's own ToggleSwitch.cs/MonitorTile.cs outline
  precedents (neither of which strokes a border on top of an already-fully-filled same-family
  shape the way BtnIdentify_Paint does — this composition has no prior precedent to copy).

  ROUND 4 DIRECTION: switch from a STROKE-based border (Pen + DrawPath, whichever
  SmoothingMode) to a FILL-based border — two ordinary anti-aliased FillPath calls, no Pen
  involved at all:
    1. Fill a rounded rect at the FULL bounds (or very slightly larger) with
       btnIdentify.FlatAppearance.BorderColor — this becomes the visible border, since it will
       be almost entirely covered by step 2.
    2. Fill a SMALLER, inset-by-BorderSize rounded rect on top with the actual button fill
       color (ManualButtonFill(...)), using a correspondingly reduced corner radius so the
       inner and outer rounded-rects stay concentric.
    3. Leave SmoothingMode.AntiAlias ON throughout (do not toggle it) — both fills are ordinary
       anti-aliased FillPath calls, the same technique MonitorTile's tile fill and ToggleSwitch's
       track fill already use cleanly elsewhere in this codebase, so corners should render
       smoothly without reintroducing the original blended-line softness (there is no thin
       stroke anywhere in this approach for GDI+ to soften).
  This is a genuinely different rendering technique from rounds 1-3, not a parameter tweak on
  the same DrawPath call — implement it, build, test, and get one more rig screenshot (ideally
  another close-up crop like 5.png, since 1.png/4.png's full-window shots were not sharp enough
  to catch the round-3 corner artifact) before declaring this resolved. This environment has no
  Windows GUI — end at another human-verify checkpoint as before.

  ROUND 4 RESULT: implemented the fill-based border in BtnIdentify_Paint (MainForm.cs) exactly
  per the direction above. Removed the Pen/DrawPath/SmoothingMode-toggling border block from
  round 2/3 entirely. New logic: when btnIdentify.FlatAppearance.BorderSize > 0, first FillPath
  a rounded rect at the FULL bounds (cornerRadius = bounds.Height * 4/32) with
  FlatAppearance.BorderColor, then FillPath a second, smaller rounded rect inset by exactly
  BorderSize on all four sides (innerCornerRadius = cornerRadius - BorderSize, clamped to >= 0)
  with ManualButtonFill's hover/press-aware fill color on top — the inset fill covers all but a
  uniform BorderSize-wide margin of the outer fill, which is the visible border. When
  BorderSize == 0 (dark mode), falls back to the original single-FillPath-at-full-bounds path
  (byte-for-byte the old dark-mode behavior, unchanged). SmoothingMode stays AntiAlias for the
  entire method, never toggled — no Pen, no DrawPath, anywhere in the border logic now. Text
  render and the conditional accent focus ring (both already AntiAlias / GDI-based) are
  untouched. `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` — 0 warnings, 0
  errors. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass (no WinForms
  rendering coverage; cannot catch visual regressions). NOT YET rig-verified — this environment
  has no Windows GUI. AWAITING RIG VERIFICATION with a fresh close-up screenshot of just the
  Identify button (like 5.png), ideally showing all four corners clearly, before this session
  can be archived as resolved.

  ROUND 4 RIG RESULT: failed, reverted to original defect. User confirmed round 4's on-rig
  appearance is genuinely identical to round 1/2's pressed-down look (not a stale-screenshot
  mixup — explicitly double-checked). See Eliminated and the 2026-08-11T18:30:00Z Evidence
  entry for the full analysis: at only 1px total border width, GDI+'s AntiAlias blend region
  (~1px wide) consumes most/all of the visible border regardless of whether it's rendered via
  stroke (rounds 1-3) or fill (round 4) — the defect was never stroke-vs-fill, it's WIDTH
  relative to AA blend radius. User was offered the choice to stop here (original invisible-
  button bug and Save Settings' double border are both independently confirmed fixed already)
  or try one more targeted change; chose to continue.

  ROUND 5 DIRECTION (user-approved 2026-08-11T18:35:00Z): widen the border from 1px to 2px.
  Keep round 4's fill-based technique (two concentric AntiAlias'd FillPath calls, no Pen) —
  that part of the technique was never falsified, only the 1px width was. Concretely: do not
  read `btnIdentify.FlatAppearance.BorderSize` directly as the inset amount anymore for this
  owner-drawn control (leave FlatAppearance.BorderSize itself at 1 in ThemeApplier — that
  value is also consumed by the OTHER, confirmed-correct native buttons via the standard
  WinForms pipeline, and must not change for them); instead use a separate, explicit 2px inset
  constant for BtnIdentify_Paint's own owner-drawn rendering specifically, since Identify is
  the only hand-drawn consumer of this styling and is not bound to reuse the same literal
  BorderSize value the native controls use. This is a visual-weight change scoped to Identify
  only — do not widen the border on any native/non-owner-drawn button. Build, test, and end at
  another human-verify checkpoint with a close-up screenshot request, same as before. If 2px
  still doesn't resolve it, the next escalation (not yet approved, ask first) would be 3px or
  reconsidering pixel-snapping the fill coordinates to exact integer device pixels rather than
  further width increases.

  ROUND 5 RESULT: implemented exactly per the direction above. Added a new constant
  `IdentifyOwnerDrawnBorderInsetPx = 2f` in MainForm.cs, scoped as a private const alongside
  the other Identify layout constants (IdentifyWidthPx/IdentifyHeightPx). BtnIdentify_Paint's
  border block still gates on `btnIdentify.FlatAppearance.BorderSize > 0` (i.e. still only
  draws a border in light mode, exactly as before — presence is unchanged, only the geometry
  changed) but now uses `IdentifyOwnerDrawnBorderInsetPx` instead of the literal `borderSize`
  variable for the inner-rect inset and the inner corner-radius reduction. ThemeApplier.cs
  is untouched — FlatAppearance.BorderSize stays at 1 for all buttons (native SettingsForm
  buttons unaffected). `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` — 0
  warnings, 0 errors. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass.
  NOT YET rig-verified. AWAITING ROUND 5 RIG VERIFICATION — requesting a close-up screenshot
  of just the Identify button (all four corners visible), same framing as 5.png, since prior
  rounds' full-window screenshots were not sharp enough to catch corner-level artifacts.

  ROUND 5 RIG RESULT: failed, but for a completely NEW reason — user's detailed follow-up
  description ("double lines... rounded corner as well as a rectangular one... half of one
  border overlapping with a normal button border") revealed a root cause none of rounds 1-5
  ever considered: TWO separate borders rendering simultaneously (a native square one this
  session never suppressed, plus the hand-drawn rounded one every round targeted). See the
  2026-08-11T19:30:00Z Evidence entry for the full analysis and Eliminated for round 5's
  falsified hypothesis.

  ROUND 6 DIRECTION: this is a genuinely different bug class from rounds 1-5, not another
  geometry/AA tweak. Two coordinated changes, both in src/RigToggle.App/MainForm.cs:
    1. In ApplyDashboardTheming (around line 1049), immediately after the existing
       `ThemeApplier.ThemeButton(btnIdentify, IsDark);` call, add
       `btnIdentify.FlatAppearance.BorderSize = 0;` — this suppresses the NATIVE square
       border WinForms renders for any Button with FlatStyle.Flat + BorderSize>0, which has
       been rendering underneath/around Identify's hand-drawn border since round 1 without
       anyone realizing it (FlatAppearance.BorderColor is untouched by this line — still
       available for the hand-drawn code below to read).
    2. In BtnIdentify_Paint, the hand-drawn border block currently gates on
       `if (btnIdentify.FlatAppearance.BorderSize > 0)` — after step 1, BorderSize will
       ALWAYS be 0, so this condition must change to something that still correctly reflects
       "are we in light mode" without depending on the now-forced-zero property. Use
       `if (!IsDark)` instead (MainForm's own IsDark property, already accessible from this
       instance method) — same light-mode-only behavior, decoupled from the native property.
    Do NOT modify ThemeApplier.cs or btnSettings — both are confirmed unaffected/correct
    (Settings' plain square FillRectangle fully masks any native border by sharing its exact
    shape/bounds, which is why Settings was never symptomatic despite receiving the identical
    BorderSize=1 assignment Identify does). Do NOT modify SettingsForm's buttons either — all
    four are natively-rendered (no owner Paint handler) and are exactly where the native
    FlatAppearance border is SUPPOSED to be the only border; this bug is specific to Identify
    being both owner-drawn AND themed via the generic native-button theming path.
    Build, test, and end at another human-verify checkpoint. Request a close-up screenshot of
    just the Identify button, same as before — this is the 4th consecutive round needing rig
    verification for this one button, so also proactively ask the user to check Identify at
    hover and pressed states too (not just at rest) if convenient, since the double-border
    mechanism could plausibly also affect those visual states and neither of them has been
    specifically screenshotted yet in this whole session.

  ROUND 6 RESULT: implemented exactly per the direction above, both changes in
  src/RigToggle.App/MainForm.cs. (1) ApplyDashboardTheming: added
  `btnIdentify.FlatAppearance.BorderSize = 0;` immediately after the existing
  `ThemeApplier.ThemeButton(btnIdentify, IsDark);` call, force-suppressing the native square
  FlatAppearance border render for this control specifically. FlatAppearance.BorderColor is
  untouched (still set by ThemeButton, still read by BtnIdentify_Paint below). btnSettings'
  ThemeButton call and every other ThemeApplier.cs logic is untouched. (2) BtnIdentify_Paint:
  changed the hand-drawn border block's gate from `if (btnIdentify.FlatAppearance.BorderSize >
  0)` (now permanently false after change 1) to `if (!IsDark)` (MainForm's own IsDark
  property) — same light-mode-only presence behavior, decoupled from the now-forced-zero
  native property. The fill-based rendering technique inside the block (round 4) and the 2px
  inset constant (round 5) are both unchanged — only the outer gating condition changed.
  ThemeApplier.cs was NOT touched this round (still has round 2's uncommitted AcceptButton
  fix, confirmed correct, left alone). `dotnet build src/RigToggle.App/RigToggle.App.csproj -c
  Debug` — 0 warnings, 0 errors. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` —
  82/82 pass. NOT YET rig-verified. AWAITING ROUND 6 RIG VERIFICATION — requesting a close-up
  screenshot of just the Identify button (all four corners visible, same framing as 5.png/
  6.png) at rest, AND a check of Identify's hover and pressed visual states (neither
  screenshotted yet this session), since the native-border-suppression mechanism could
  plausibly affect those states too.

  ROUND 6 RIG RESULT: major progress, one remaining subtlety. User confirms the double-line/
  mismatched-corner defect is GONE ("looks better now") — round 6's native-border suppression
  is correct and should be kept as-is. New, more subtle report: top and left border edges look
  thicker than bottom and right. Focus ring ruled out (button was not focused in the test).
  No other border-drawing code found for btnIdentify anywhere in MainForm.cs/Designer.cs. The
  fill geometry itself (outer/inner rounded rects) is mathematically symmetric — equal
  borderInset margin on all four sides by construction. Leading hypothesis: GDI+'s own
  AntiAlias FillPath rasterization has a known top-left-vs-bottom-right coverage bias under
  the default PixelOffsetMode, which would explain a systematic (not code-logic) thickness
  bias without any second border source. See the 2026-08-11T20:30:00Z Evidence entry for the
  full reasoning.

  ROUND 7 DIRECTION: in BtnIdentify_Paint, set `e.Graphics.PixelOffsetMode =
  PixelOffsetMode.Half;` (System.Drawing.Drawing2D namespace, already imported for
  SmoothingMode) before the border fills — the standard, documented GDI+ remedy for
  asymmetric anti-aliased rectangle-fill bias. Test scoping it to just the border-drawing
  block first (set before the outer/inner FillPath calls, restore to Default after, mirroring
  how SmoothingMode is scoped elsewhere in this method) to avoid unintended side effects on
  the text rendering (TextRenderer.DrawText, which is GDI-based and likely unaffected by
  Graphics.PixelOffsetMode regardless, but keep the change minimal and scoped rather than
  applying it for the whole method on a first attempt). Do NOT revert round 6's
  BorderSize=0/!IsDark changes — those are confirmed correct. Build, test, end at another
  human-verify checkpoint with a close-up screenshot request, focusing specifically on
  whether all four edges now read as equal thickness.

  ROUND 7 RESULT: implemented exactly per the direction above, in
  src/RigToggle.App/MainForm.cs, BtnIdentify_Paint. Inside the existing `if (!IsDark)` block
  (round 6, untouched otherwise): set `e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;`
  immediately before the outer/inner border FillPath calls, wrapped those two FillPath calls
  in a try/finally, and restore `e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;` in the
  finally block (mirrors how SmoothingMode is scoped elsewhere in this method, and guarantees
  restoration even if a FillPath call throws). Scope is exactly the two border fills — text
  render (TextRenderer.DrawText, GDI-based, unaffected by this property) and the conditional
  focus-ring DrawPath both remain at PixelOffsetMode.Default, untouched. Round 6's
  BorderSize=0 (ApplyDashboardTheming) and `!IsDark` gate were NOT modified. ThemeApplier.cs
  was NOT touched. `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` — 0
  warnings, 0 errors. `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass.
  NOT YET rig-verified — this environment has no Windows GUI. AWAITING ROUND 7 RIG
  VERIFICATION — requesting a fresh close-up screenshot of just the Identify button (all four
  edges/corners clearly visible, same framing as prior close-up crops 5.png/6.png), asking
  specifically whether all four border edges now read as equal thickness.

## Evidence

- timestamp: 2026-08-11T00:00:00Z
  checked: ThemeApplier.cs ThemeButton method (lines 125-146)
  found: FlatStyle.Flat + FlatAppearance.BorderSize=0 applied unconditionally for both dark and light mode; BackColor in light mode is SystemColors.Control. Doc comment states BorderSize=0 exists specifically to avoid dotnet/winforms#13897, a bug described as only manifesting "once dark mode is active."
  implication: light mode never gets a border, even though the bug this avoids is dark-mode-only — light mode buttons rely entirely on fill/ForeColor contrast against the background, with no border fallback.

- timestamp: 2026-08-11T00:00:01Z
  checked: RigToggle.Windows/DwmTitleBar.cs ApplyRoundedCornersAndMica
  found: DWMSBT_MAINWINDOW (standard Mica) backdrop applied to the window regardless of theme.
  implication: in light mode, standard Mica renders near-white/very-light-gray — visually close to SystemColors.Control, the exact color ThemeButton fills flat buttons with. Button fill and window background nearly match, with no border to separate them.

- timestamp: 2026-08-11T00:00:02Z
  checked: MainForm.cs BtnIdentify_Paint (1163-1199) vs BtnSettings_Paint (1235-1260)
  found: both use the same ManualButtonFill/BackColor and the same ForeColor source; BtnSettings_Paint additionally draws MonitorIconGeometry.DrawGearIcon (a dense multi-stroke glyph) while BtnIdentify_Paint only draws plain centered text via TextRenderer.DrawText.
  implication: the working (Settings) and broken (Identify) buttons share identical fill/border/ForeColor values — the only functional difference is glyph vs. text content. The gear icon's shape provides enough visual mass to read as a bounded button even with fill≈background; plain text does not. This directly explains why Settings "reads correctly" and Identify blends, without needing any color difference between them.

- timestamp: 2026-08-11T00:00:03Z
  checked: SettingsForm.cs / SettingsForm.Designer.cs for "_Paint" event handlers on btnBrowse/btnClearAppPath/btnDiscardChanges/btnSaveSettings
  found: no matches — none of these buttons are owner-drawn/self-painted.
  implication: unlike MainForm's btnIdentify/btnSettings (which self-paint over their full ClientRectangle and would erase a native FlatAppearance border), SettingsForm's buttons render natively via FlatStyle.Flat — a FlatAppearance.BorderSize>0 set in ThemeButton will actually render and immediately fix Browse/ClearAppPath/DiscardChanges without any SettingsForm-specific code change.

- timestamp: 2026-08-11T00:00:04Z
  checked: SettingsForm.cs constructor (lines 105-106)
  found: this.AcceptButton = btnSaveSettings — btnSaveSettings is the form's default button (IsDefault=true), the only one of the four SettingsForm buttons themed identically that the user did NOT report as blending.
  implication: consistent with the hypothesis — btnSaveSettings' default-button visual treatment gives it incidental extra definition Browse/ClearAppPath/DiscardChanges lack, not a color difference. Does not require special-casing in the fix; adding a light-mode border to all four via ThemeButton is neutral-to-positive for btnSaveSettings.

- timestamp: 2026-08-11T12:00:00Z
  checked: User rig verification on real Windows 11 hardware, light mode, first fix attempt (BorderSize=dark?0:1 + BorderColor=ControlDark uniformly)
  found: |
    Partial success. Browse, Clear, and Discard Changes now look correct (confirmed by user).
    Two NEW visual defects introduced by the fix itself, evidenced by two screenshots (1.png, 2.png)
    the user attached (now at repo root, /home/bpivk/moza/1.png and 2.png):

    1.png (MainForm): Identify button now has a border, but the user describes it as
    "looks like it's pressed down" — i.e. reads as a sunken/inset button rather than a normal
    flat outlined one. Rig Mode toggle switch and the Settings gear button (which has its own
    pre-existing accent-color focus-ring square around it, unrelated/not reported as broken)
    are in the same screenshot for comparison.

    2.png (SettingsForm): Save Settings and Discard Changes side by side. Discard Changes has
    a single clean border (correct, matches Browse/Clear). Save Settings visibly has a DOUBLE
    border — an outer and inner ring, distinctly different from Discard Changes' single border.
  implication: |
    Save Settings double-border directly falsifies this session's earlier "neutral-to-positive"
    prediction for btnSaveSettings (see Evidence 2026-08-11T00:00:04Z and Resolution.fix below).
    SettingsForm.cs:105 sets `this.AcceptButton = btnSaveSettings` — WinForms natively draws a
    distinguishing visual treatment around a form's AcceptButton/default button (typically an
    extra ~1px accent-colored outline) REGARDLESS of FlatAppearance.BorderSize. The fix added a
    SECOND explicit FlatAppearance border on top of that pre-existing native default-button
    indicator, producing two concentric rings. Discard Changes (not AcceptButton) only gets the
    new single border and looks correct — direct A/B confirmation within the same screenshot.

    Identify's "pressed down" look is NOT explained by the AcceptButton mechanism (Identify is
    not a form's AcceptButton). Candidate mechanism: BtnIdentify_Paint fills the FULL bounds
    solid first (fillPath = BuildRoundedRect(bounds, cornerRadius) at true edges), THEN draws a
    SEPARATE inset border stroke afterward (borderRect inset by borderPenWidth/2, its own
    slightly-smaller cornerRadius) — fill-then-stroke-inset, not stroke-then-fill on the same
    path. This is the OPPOSITE order/geometry from this codebase's own established
    "stroke-then-fill GDI+ compositing" convention (Phase 13 precedent, reused Phase 19/20 per
    prior PATTERNS.md/PITFALLS.md references) and differs structurally from MonitorTile.cs's own
    focus-ring precedent (MonitorTile.cs:260-267, BuildRoundedRect+DrawPath on an inset rect) in
    one key way: MonitorTile's inset ring is a CONDITIONAL overlay (only when Focused, drawn on
    top of an already-complete tile as a temporary indicator), not a PERMANENT structural border
    outlining the whole control's boundary. Using the same "thin inset ring on top of a solid
    fill" technique for a permanent border, rather than for a temporary focus indicator, may be
    what reads as a sunken bevel — worth comparing against how a normal (non-owner-drawn)
    FlatStyle.Flat button's native border renders (flush with the true edge, not inset) since
    that's what Discard Changes/Browse/Clear now correctly show and what Identify should match
    visually, being also a to-edge (non-inset) rendering rather than a ring drawn inward from a
    fully-painted fill.
  screenshots: ["/home/bpivk/moza/1.png", "/home/bpivk/moza/2.png"]

- timestamp: 2026-08-11T13:00:00Z
  checked: |
    Applied round-2 fixes: (A) ThemeApplier.cs ThemeButton now suppresses the explicit
    light-mode FlatAppearance border when `ReferenceEquals(button.FindForm()?.AcceptButton, button)`
    is true, leaving BorderSize=0 for the form's current default button (native ring only)
    while every non-default button in light mode is unaffected. (B) MainForm.cs
    BtnIdentify_Paint now reads `btnIdentify.FlatAppearance.BorderSize` (literal int, same
    value ThemeButton sets) as the border pen width instead of a height-derived float, with
    a half-pixel inset so a 1px anti-aliased pen renders as a crisp single-pixel-row line
    instead of straddling two pixel rows. Re-derived the round-1 "inset ring" geometry
    theory algebraically and confirmed it was NOT the actual defect (the stroke's outer edge
    is provably flush with the fill's true-bounds edge at every corner) -- eliminated in
    favor of the width-mismatch/anti-aliasing-softness mechanism instead.
    `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` -- 0 warnings, 0 errors.
    `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` -- 82/82 pass.
  found: Both fixes compile cleanly and the full existing test suite (no WinForms rendering coverage) passes unchanged.
  implication: |
    Self-verification is exhausted for this environment (no Windows GUI available to render
    WinForms). Root-cause mechanisms for both round-1 regressions are reasoned through and
    documented (native default-button ring stacking for (A), anti-aliased non-integer pen
    width for (B)), each grounded in direct source reads (ButtonFlatAdapter.cs behavior,
    this session's own prior evidence, and the literal MainForm.cs/ThemeApplier.cs code) --
    but neither can be pixel-confirmed without the user re-testing on the real Windows 11
    rig with fresh screenshots.

- timestamp: 2026-08-11T14:00:00Z
  checked: |
    Round-2 fix's real-world effect on real hardware, via two new user screenshots
    (3.png — Identify in an INACTIVE/dimmed background MainForm behind the modal Settings
    dialog, not usable for comparison; 4.png — Identify in an ACTIVE, focused MainForm, same
    framing as the original 1.png). Compared 4.png directly against 1.png (round-1 build).
    Also checked the exact numeric math of round 2's pen-width "fix", and read BtnSettings_Paint
    in full (MainForm.cs:1269-1295) for the first time end-to-end rather than assuming its
    border treatment from Evidence 2026-08-11T00:00:02Z's earlier partial read.
  found: |
    (a) btnIdentify.Size is set via `Scaled(IdentifyHeightPx)` with `IdentifyHeightPx = 32`
    (MainForm.cs:79-80, 991). At default 100% DPI scale, Scaled(32) = 32. The round-1 "buggy"
    pen width was `bounds.Height * (1f/32f)` = 32 * 1/32 = **1.0** — numerically IDENTICAL to
    round 2's literal `FlatAppearance.BorderSize` value of **1**. The round-2 fix changed the
    rect-inset math by an imperceptible amount (borderPenWidth/2 in both cases evaluates to the
    same 0.5) but did NOT change the rendered pen width at all at this button's actual size.
    This is a mathematical coincidence specific to IdentifyHeightPx=32, not something that would
    hold at other DPI scales, but at 100% (the scale in every screenshot so far) round 2 was a
    no-op.
    (b) User confirms: 4.png (active MainForm, round-2 build) looks the same as 1.png
    (active MainForm, round-1 build) — visually verified by direct side-by-side comparison,
    consistent with (a)'s math. Round 2 did not change Identify's appearance in any way; the
    "pressed down" look is unchanged from round 1.
    (c) BtnSettings_Paint (MainForm.cs:1269-1295), read in full: draws NO border whatsoever.
    It fills a plain (non-rounded) Rectangle with ManualButtonFill, draws the gear glyph via
    MonitorIconGeometry.DrawGearIcon, and ONLY draws a border-like element
    (DrawButtonFocusRing, an ACCENT-colored square, not SystemColors.ControlDark) when
    `btnSettings.Focused` is true. Every screenshot taken so far (1.png, 4.png) shows Settings
    with its blue focus-ring square visible — meaning Settings has simply retained initial
    keyboard focus (default tab order) in every screenshot session, not that it has a
    permanent border. The user's original framing ("Identify needs an outline like Settings
    does") was based on an incidental focused-state screenshot, not Settings' actual resting
    (unfocused) appearance, which has ZERO border — just the gear glyph.
  implication: |
    Two separate conclusions:
    1. The round-2 hypothesis (anti-aliased pen-width blur causing a "pressed" look) is
       FALSIFIED at IdentifyHeightPx=32 — the pen width literally did not change between
       round 1 and round 2, yet the user still describes the same "pressed down" look in
       both, meaning whatever visual defect exists must be present in BOTH renders equally,
       and is NOT explained by the width-vs-integer-pixel reasoning from round 2.
    2. Comparing Identify to Settings was never an apples-to-apples comparison to begin with —
       Settings has no border at rest; its visual distinctness in every observed screenshot
       comes from (a) the gear icon glyph (dense visual mass, per the ORIGINAL round-1
       hypothesis, still valid) plus (b) an incidental focus ring that happens to always be
       visible because Settings has kept default focus in every test session so far. The
       actual remaining defect is specific to Identify's owner-drawn border rendering itself
       reading as "pressed/sunken" to the user, independent of any comparison to Settings —
       needs to be diagnosed on its own terms (e.g. anti-aliasing artifacts inherent to
       GDI+ DrawPath on a rounded-rect path vs. WinForms' native pixel-snapped hard-edged
       FlatStyle.Flat border rendering used by the SIBLING SettingsForm buttons that ARE
       confirmed correct, even though those buttons share the identical BorderColor/BorderSize
       values — the difference must be in HOW the stroke is rasterized, not what color/width
       it's set to).

- timestamp: 2026-08-11T15:00:00Z
  checked: |
    ROUND 3. Read raw pixel RGB values directly from the user's own screenshots
    (1.png = Identify, MainForm; 2.png = Discard Changes/Save Settings, SettingsForm)
    using PIL in this Linux environment (image files ARE readable here even though
    WinForms itself cannot run) -- this was not done in rounds 1-2, which reasoned
    about rendering mechanisms from source code only, never measured actual rendered
    pixel colors.
    Native button border (Discard Changes, 2.png, row y=69 full width): pure, hard-edged,
    exactly (160,160,160) for its entire length, transitioning directly 240->160->240 with
    ZERO intermediate/blended pixel values -- confirms native ButtonFlatAdapter rendering is
    fully opaque, single-pixel-row, non-anti-aliased, and 160 is the true rendered
    SystemColors.ControlDark value on this rig.
    Identify's border (1.png, BorderColor is the SAME SystemColors.ControlDark, BorderSize
    is the SAME literal 1): measured at all four edges around the button:
      - top edge: TWO pixel rows, values (190,190,190) and (180,180,180)
      - left edge: TWO pixel columns, values (185,185,185) and (190,190,190)
      - bottom edge: ONE pixel row, value (200,200,200)
      - right edge: ONE pixel column, value (200,200,200)
    Every measured Identify border pixel is a blend between the true border color (160) and
    the fill/background color (240) -- none reach pure 160. Fill and window background both
    measured at (240,240,240) (identical, confirming the original round-1 fill≈background
    finding is still true and irrelevant to this remaining defect).
  found: |
    The border renders ASYMMETRICALLY: top and left edges are each 2 px wide and average
    darker/closer to the true border color (~185-190), while bottom and right edges are each
    1 px wide and lighter/more washed out (~200, closer to the 240 background). This
    top-left-darker / bottom-right-lighter partial-coverage pattern is the exact classic
    Windows 3D "sunken/inset" bevel convention (a raised/embossed control conventionally
    shows light top-left + dark bottom-right; a pressed/sunken control shows the reverse --
    dark top-left + light bottom-right, which is precisely what was measured here).
  implication: |
    This is not merely "soft/blurry" (round 2's already-falsified framing) -- it is a
    directionally CONSISTENT shading asymmetry that reads as a sunken bevel, which directly
    and precisely explains "looks like it's pressed down" as a literal, measurable rendering
    artifact, not a subjective impression. Root mechanism: e.Graphics.SmoothingMode =
    AntiAlias is active for BtnIdentify_Paint's border DrawPath call (MainForm.cs:1167,1207).
    GDI+'s anti-aliased rasterizer computes fractional per-pixel coverage for the stroked
    rounded-rect path; even though the border rect is deliberately offset to (0.5, 0.5) for
    theoretical crisp-hairline alignment (MainForm.cs:1202-1204), the rasterizer is still
    spreading the 1px-wide stroke's coverage unevenly across 2 pixel rows/columns on two
    sides and 1 (lower-coverage) pixel on the other two sides -- concretely NOT what the
    native, non-anti-aliased ButtonFlatAdapter pipeline does for the sibling buttons (a single
    fully-opaque pixel row/column at the exact border color, confirmed above). The
    inconsistent asymmetry (2px+2px vs 1px+1px, not a uniform 4-side blur) rules out "pen
    width is simply non-integer" (round 2, already falsified) and instead implicates the
    AntiAlias rasterizer's coverage computation itself as applied to this specific
    Fill-then-Stroke rounded-rect GraphicsPath, not the pen geometry math (already proven
    flush in round 2). This directly matches this session's own round-3 next_action angle:
    "disabling SmoothingMode.AntiAlias for just the border DrawPath call... to see whether a
    hard-edged stroke resolves the pressed look."
  screenshots: ["/home/bpivk/moza/1.png (re-examined with pixel sampling)", "/home/bpivk/moza/2.png (re-examined with pixel sampling)"]

- timestamp: 2026-08-11T17:00:00Z
  checked: |
    Round-3 fix's real-world effect on real hardware, via user-provided zoomed-in screenshot
    5.png (a close crop of just the Identify button at rest, much higher effective resolution
    than 1.png/4.png's full-window shots). Also compared against this codebase's own existing
    "outline over a shape" precedent, ToggleSwitch.cs's Off-state track outline (lines 408-429)
    and MonitorTile.cs's focus ring (lines 260-267), read in full for the first time this round.
  found: |
    User reports (and 5.png shows): the blending/pressed-look IS resolved (border reads solid
    now), but disabling AntiAlias for the border DrawPath call introduced NEW corner artifacts —
    the user's own words: "the border has added pixels... upper left corner has some kind of
    rounding [artifact]. Right has a few pixels and bottom left as well." This is the textbook
    symptom of rasterizing a curved GraphicsPath (BuildRoundedRect's arc segments at the 4px
    corner radius) with SmoothingMode.None — GDI+'s non-antialiased rasterizer stair-steps/
    notches curves instead of smoothing them, and can expose small path-seam artifacts at
    arc-to-line transition points that AntiAlias was previously hiding.

    Codebase precedent check: ToggleSwitch's Off-state outline (the closest existing analog —
    a single stroked rounded-rect outline, not a filled tile) calls `g.DrawPath(outlinePen,
    trackPath)` directly on the SAME path used for any fill, with NO SmoothingMode toggling and
    NO separate inset border rect — it either strokes-only (no fill drawn first, so the pen has
    nothing but the surrounding BackColor to blend against) or, in On/Indeterminate state,
    fills only (no separate stroke at all). Neither ToggleSwitch nor MonitorTile's focus ring
    ever draws a stroke ON TOP OF an already-fully-opaque same-color-family fill the way
    BtnIdentify_Paint does (fillPath covers full bounds first, THEN a separately-built inset
    borderPath is stroked over it) — this fill-then-stroke-over-fill composition has no
    precedent elsewhere in this codebase to copy from, which may be why it's proven fragile
    across two attempts (soft/blended with AntiAlias on, notched/stray-pixeled with it off).
  implication: |
    SmoothingMode.None was too blunt an instrument — it fixed the straight-edge blending
    (confirmed by round 3's pixel measurements) but broke the rounded-corner rendering, which
    is a deliberate, established design choice for this control (do not remove the rounding).
    A pure stroke-based approach (Pen + DrawPath), with AntiAlias either fully on or fully off,
    cannot satisfy both "crisp opaque straight edges" and "smooth smooth curved corners" at
    once for this single DrawPath call — GDI+'s known thin-line AA softness (a widely-
    documented limitation, not specific to this codebase) affects 1px strokes even when
    precisely half-pixel-aligned, and disabling AA fixes the line-softness but reintroduces
    raw stair-stepping on any arc segment of the same path.
    Next direction: replace the stroke-based border entirely with a FILL-based border — draw
    a slightly-larger rounded rect filled with BorderColor FIRST (this becomes the visible
    border ring, and the same AA-filled-shape rendering already proven clean elsewhere in this
    codebase — MonitorTile's tile fill, ToggleSwitch's track fill — applies here too, since it's
    an ordinary anti-aliased FillPath, not a thin stroke), THEN fill a smaller inset rounded
    rect with the actual button fill color ON TOP, leaving only a uniform border-color margin
    visible. This avoids GDI+'s thin-stroke AA softness entirely (no Pen/DrawPath involved for
    the border at all) while keeping AntiAlias on throughout (so corners stay smooth, matching
    every other rounded shape in this app). Needs re-verification with a fresh screenshot once
    implemented, ideally another close-up crop like 5.png since regular full-window screenshots
    (1.png/4.png) were not sharp enough to catch the corner-notch artifact this round.

reasoning_checkpoint_round4:
  hypothesis: |
    Replacing the stroke-based border (Pen + DrawPath, either SmoothingMode) with a
    FILL-based border (two ordinary AntiAlias FillPath calls -- outer rounded rect filled
    with BorderColor, inner inset rounded rect filled with the button fill color on top,
    no Pen involved anywhere) will render Identify's border as a crisp, uniform,
    fully-opaque margin with smooth rounded corners simultaneously, because it structurally
    cannot exhibit either round-3-class defect: there is no thin stroke for GDI+'s
    coverage-based AA rasterizer to blend/soften (round 3's defect), and AntiAlias stays on
    throughout so the curved corner arcs in both BuildRoundedRect paths render smoothed, not
    aliased/stair-stepped (round 3-fix's regression).
  confirming_evidence:
    - "This codebase's own MonitorTile tile fill and ToggleSwitch track fill already use
      plain AntiAlias FillPath on rounded-rect paths cleanly, with no reported blending or
      corner artifacts anywhere in this session's evidence -- establishing that ordinary
      anti-aliased fills of rounded shapes are NOT the problematic case in this codebase;
      only thin AntiAlias STROKES (Pen+DrawPath) were ever implicated (round 3 evidence,
      2026-08-11T15:00:00Z / 17:00:00Z)."
    - "Round 3's own falsification narrowed the defect specifically to the Pen/DrawPath
      stroke rasterization path, not to rounded-rect geometry or AntiAlias in general --
      removing the stroke removes the exact mechanism identified as the root cause, rather
      than adjusting a parameter within that same mechanism (which rounds 2 and 3 both did
      and both only partially fixed the problem)."
    - "The inset math is a direct reuse of the same inset-by-BorderSize/reduced-corner-radius
      relationship already validated algebraically in round 2 (Minkowski-offset concentricity
      check) -- just applied to two solid fills instead of a stroke, so the outer and inner
      rounded rects are provably concentric with a uniform BorderSize-wide margin between
      them at every corner, not just the straight edges."
  falsification_test: |
    If the fresh rig screenshot shows ANY non-uniform margin width around the border (e.g.
    thicker at corners than edges, or vice versa), a soft/blended edge on the outer or inner
    fill boundary, or any stair-step/notch artifact at the corners, this hypothesis is wrong
    and a fourth, not-yet-considered mechanism is in play (candidates to consider next: GDI+
    fill-path anti-aliasing behaves differently when two adjacent filled paths overlap/abut
    versus a single isolated fill, or a DPI-scaling-specific rounding issue in the inset
    math that only manifests on the real rig's DPI setting).
  fix_rationale: |
    Same as ROUND 4 DIRECTION recorded in next_action -- targets the actual mechanism
    (stroke-based rasterization) identified as the root cause of the round-3 "traded one
    artifact for another" result, by removing that mechanism entirely rather than tuning it
    further. Minimal in scope: only the border-drawing block inside BtnIdentify_Paint
    changed; ThemeApplier.cs (AcceptButton suppression, confirmed fixed) and BtnSettings_Paint
    (never reported broken) are untouched.
  blind_spots: |
    Still cannot render WinForms in this Linux environment -- this fix is deduced from
    reasoning about GDI+'s fill-vs-stroke rasterization behavior and this codebase's own
    working precedents (MonitorTile/ToggleSwitch fills), not observed on real hardware.
    Whether two adjacent/abutting AntiAlias-filled rounded-rect paths (rather than a single
    isolated fill, which is all the existing precedents use) produce any seam/blend artifact
    at the boundary between them is unverified -- plausible in theory (GDI+ fill AA typically
    only affects a shape's own outer edge, not creating gaps against an adjacent fill) but
    not something this session can confirm without a rig screenshot. Requires human-verify
    confirmation before this session can be archived, same as rounds 1-3.

- timestamp: 2026-08-11T18:00:00Z
  checked: |
    Round-3 fix's real-world corner-artifact regression (see 2026-08-11T17:00:00Z Evidence
    and its falsified Eliminated entry). Implemented ROUND 4 DIRECTION: replaced the entire
    stroke-based border block (Pen + DrawPath + SmoothingMode toggling) in BtnIdentify_Paint
    (MainForm.cs) with a fill-based border — FillPath a full-bounds rounded rect with
    FlatAppearance.BorderColor first, then FillPath a smaller rounded rect inset by exactly
    BorderSize on top with ManualButtonFill's fill color, using a corner radius reduced by
    BorderSize to stay concentric with the outer rect. SmoothingMode left at AntiAlias for
    the entire method, never toggled. Dark mode (BorderSize=0) falls through to the original
    single-fill path, unchanged.
    `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` — 0 warnings, 0 errors.
    `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass.
  found: Fix compiles cleanly, full existing test suite (no WinForms rendering coverage) passes unchanged.
  implication: |
    Self-verification is exhausted again for this environment (no Windows GUI). The fix
    structurally removes the exact mechanism round 3 identified as the root cause (a thin
    Pen/DrawPath stroke subject to GDI+'s AA-coverage-blending-vs-aliased-stair-stepping
    tradeoff) rather than tuning a parameter within that mechanism, which is what rounds 2
    and 3 both did and both only partially fixed. Cannot be pixel-confirmed without the user
    re-testing on the real Windows 11 rig with a fresh close-up screenshot.

- timestamp: 2026-08-11T18:30:00Z
  checked: Round-4 fill-based border's real-world effect. User re-tested on the rig and reported "we're back at the start with the depressed look," pointing at the existing 4.png (round-2 screenshot) as still visually representative — explicitly confirmed (via follow-up question) that this is not a stale/misattached file, round 4's actual on-rig appearance genuinely looks the same as round 1/2's original pressed-down look, not merely similar.
  found: |
    Round 4 (two concentric AA'd FillPath calls, outer BorderColor + inner fill color, inset by
    the literal borderSize=1px) reproduced the same "pressed" look as rounds 1-2, essentially
    undoing round 3's partial progress (round 3 at least visibly changed the edges, even though
    it broke the corners). Re-examined the math: with only a 1px margin between the outer and
    inner fill, GDI+'s AntiAlias coverage-based rasterizer blends the INNER fill's edge against
    whatever is already painted underneath (the outer BorderColor fill) across a sub-pixel
    coverage band — at 1px total width, that blend band can consume most or all of the visible
    border, functionally reproducing the same "soft ring blending into the fill" appearance as
    the original stroke-based rounds, just via a different code path. The border color chosen
    (SystemColors.ControlDark) is unchanged and still matches the confirmed-correct native
    buttons exactly, so color is not implicated — width/AA-blend-ratio is the remaining
    unexplored lever.
  implication: |
    A 1px border, regardless of stroke-vs-fill technique, is too thin relative to GDI+'s
    anti-aliasing blend radius (roughly ~1px of soft transition on each edge) to ever read as
    fully opaque/crisp when rendered by hand-drawn GDI+ with AntiAlias on — the blend region is
    a large fraction (up to 100%) of the border's own width at 1px. Native GDI-rendered
    FlatStyle.Flat borders don't have this problem because native Win32 border rendering isn't
    anti-aliased the way GDI+'s path-fill/path-stroke rasterizer is. Round 3 already showed
    that disabling AA fixes the width problem but breaks the (intentionally rounded) corners on
    the same call. The untried lever: widen the border to 2px. This doesn't eliminate AA
    blending at the edges, but roughly halves the blended-region's share of the total border
    width (a ~1px soft transition against a 2px border leaves close to half of it visibly solid,
    vs. nearly the entire 1px border being blend-affected) — user approved trying this
    (2026-08-11T18:35:00Z) after being told this is a visual-weight change, not just a bugfix,
    and after being offered the alternative of stopping here and leaving Identify's border
    softness as a known minor cosmetic issue (the original invisible-button bug and Save
    Settings' double border are both independently confirmed fixed already, regardless of how
    this specific detail resolves).

- timestamp: 2026-08-11T19:00:00Z
  checked: |
    ROUND 5. Implemented the user-approved 2px-inset direction: added
    `IdentifyOwnerDrawnBorderInsetPx = 2f` as a new private const in MainForm.cs, scoped
    next to the other Identify layout constants. BtnIdentify_Paint's border block (round 4's
    fill-based technique, unchanged) now uses this constant for the inner rect's inset and
    the inner corner-radius reduction, instead of the literal `btnIdentify.FlatAppearance.
    BorderSize` value (which stays 1 and still gates whether a border is drawn at all --
    `borderSize > 0` presence check is untouched). ThemeApplier.cs was NOT touched --
    FlatAppearance.BorderSize remains 1 for every button, so the confirmed-correct native
    SettingsForm buttons (Discard Changes/Browse/Clear/Save Settings), which read BorderSize
    directly through the standard WinForms FlatStyle.Flat pipeline, are unaffected by this
    change.
    `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` -- 0 warnings, 0 errors.
    `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` -- 82/82 pass.
  found: Fix compiles cleanly, full existing test suite (no WinForms rendering coverage) passes unchanged.
  implication: |
    Self-verification is exhausted again for this environment (no Windows GUI). The change is
    minimal and additive: only the inset magnitude used inside BtnIdentify_Paint's already-
    fill-based (round 4, never falsified as a technique) border logic changes, from 1px to
    2px, via a new named constant kept deliberately separate from FlatAppearance.BorderSize so
    the native buttons' shared theming value is untouched. Cannot be pixel-confirmed without
    the user re-testing on the real Windows 11 rig with a fresh close-up screenshot of just
    the Identify button (all four corners visible, same framing as 5.png).

- timestamp: 2026-08-11T19:30:00Z
  checked: |
    Round-5's real-world effect via user-provided close-up screenshot 6.png, plus a specific,
    highly-detailed follow-up description after the user rejected an initial pass/fail read of
    that image: "the upper and left portions of it have double lines. Also upper left portion
    clearly has a rounded corner as well as a rectangular one. Like half of one border is
    overlapping with a normal button border." This description does not match anything rounds
    1-5 hypothesized (all of which assumed exactly ONE border was being rendered, with defects
    in its width/color/AA-crispness) -- it describes TWO DISTINCT, DIFFERENTLY-SHAPED borders
    simultaneously visible (one rounded, one rectangular). Re-read MainForm.cs:1049
    (`ThemeApplier.ThemeButton(btnIdentify, IsDark)`, in ApplyDashboardTheming, the phase 19/20
    two-call-site funnel every theming update goes through) with this specific framing in mind.
  found: |
    ThemeApplier.ThemeButton(btnIdentify, IsDark) is called unconditionally for btnIdentify,
    the SAME generic method used for every native (non-owner-drawn) themed button. Since round
    1, this method sets `btnIdentify.FlatAppearance.BorderSize = dark ? 0 : 1` and
    `FlatAppearance.BorderColor = SystemColors.ControlDark` on the underlying stock
    `System.Windows.Forms.Button` control btnIdentify actually is. btnIdentify is NOT a custom
    UserControl subclass with UserPaint styling suppressing base rendering -- it's a plain
    `Button` with a `Paint` event handler attached (BtnIdentify_Paint). For a stock WinForms
    Button, `OnPaint` calls the base FlatStyle-adapter rendering (which DOES include a NATIVE,
    SQUARE FlatAppearance border when BorderSize>0) before raising the `Paint` event to
    subscribers -- meaning BtnIdentify_Paint's hand-drawn content (including its own,
    intentionally ROUNDED custom border, present since round 2) is drawn ON TOP OF, not INSTEAD
    OF, the native square border. Every round 1-5 assumption that "FillPath covering the full
    ClientRectangle erases any native border" was never actually verified and is likely wrong
    for BorderSize specifically -- it may have been true-enough for the ORIGINAL BorderSize=0
    state (nothing to erase), but became false the moment round 1 started setting BorderSize=1
    on this same, still-natively-rendering control.

    Cross-check against btnSettings (also owner-drawn, also themed via the identical
    ThemeApplier.ThemeButton(btnSettings, IsDark) call at MainForm.cs:1050, so it ALSO gets
    FlatAppearance.BorderSize=1 in light mode): BtnSettings_Paint fills a plain, non-rounded
    `FillRectangle` over the FULL ClientRectangle -- the exact same shape and bounds a native
    square border would occupy. A same-shape, same-bounds opaque fill drawn after the native
    border completely obscures it (zero exposed native-border pixels), which is exactly why
    Settings has never shown any double-border symptom despite receiving the identical
    FlatAppearance.BorderSize=1 assignment as Identify. Identify's fill, by design (D-01/D-02
    of the Phase 20 UI-SPEC, matching the tile/toggle-switch rounded visual language), is a
    ROUNDED rect that does NOT cover the same square bounds a native border occupies --
    specifically at the corners, where the round arc pulls inward, the native square border's
    corner geometry is left exposed, and along the straight edges the two borders (native
    square, hand-drawn rounded, at a 2px inset since round 5) sit at different pixel distances
    from the true edge, producing two near-parallel visible lines.
  implication: |
    This is the actual root cause of every round-1-through-5 failure, not a refinement target
    within the same design -- Identify was NEVER supposed to receive a nonzero NATIVE
    FlatAppearance.BorderSize at all, because it manages its own border entirely by hand. Every
    prior round was correctly diagnosing and partially fixing artifacts of the HAND-DRAWN
    border (blending, width, AA), while a completely separate NATIVE border was simultaneously
    rendering underneath/around it the entire time, invisible-in-isolation-analysis but fully
    explaining "pressed down" (a native square border peeking out from under/beside a slightly
    differently-positioned rounded one reads as a layered, uneven, recessed shape) and now, at
    2px width, visible enough as literal double lines and mismatched corner shapes for the user
    to describe precisely.
    Fix direction (round 6): stop btnIdentify from ever getting a nonzero NATIVE
    FlatAppearance.BorderSize, while preserving its hand-drawn border. Concretely: (1) in
    MainForm.cs ApplyDashboardTheming, immediately after the existing
    `ThemeApplier.ThemeButton(btnIdentify, IsDark);` call (line 1049), add
    `btnIdentify.FlatAppearance.BorderSize = 0;` to force-suppress the native border render
    (FlatAppearance.BorderColor is untouched by this -- still available for the hand-drawn
    code to read); (2) in BtnIdentify_Paint, the hand-drawn border's draw condition currently
    reads `if (btnIdentify.FlatAppearance.BorderSize > 0)` -- this will now ALWAYS be false
    once (1) is applied, so the condition must change to something decoupled from
    FlatAppearance.BorderSize entirely, e.g. `if (!IsDark)` (MainForm's own IsDark property,
    already accessible from this instance method) -- preserving the exact same
    light-mode-only border behavior without depending on the now-forced-to-zero native
    property. Do NOT touch btnSettings or ThemeApplier.ThemeButton's general logic -- both are
    confirmed correct/unaffected by this bug (the shape-masking argument above explains why
    Settings was never symptomatic despite sharing the same BorderSize=1 assignment).

- timestamp: 2026-08-11T20:00:00Z
  checked: |
    ROUND 6. Implemented the round-6 direction (native-border suppression) exactly as
    specified: src/RigToggle.App/MainForm.cs ApplyDashboardTheming now sets
    `btnIdentify.FlatAppearance.BorderSize = 0;` immediately after
    `ThemeApplier.ThemeButton(btnIdentify, IsDark);`; BtnIdentify_Paint's border-block gate
    changed from `if (btnIdentify.FlatAppearance.BorderSize > 0)` to `if (!IsDark)`. No other
    lines changed — round 4's fill-based technique and round 5's 2px inset constant
    (IdentifyOwnerDrawnBorderInsetPx) are both preserved untouched inside the block.
    ThemeApplier.cs and btnSettings/SettingsForm code paths were not touched.
    `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` — 0 warnings, 0 errors.
    `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass.
  found: Fix compiles cleanly, full existing test suite (no WinForms rendering coverage) passes unchanged.
  implication: |
    Self-verification is exhausted again for this environment (no Windows GUI). This round
    targets a structurally different mechanism than rounds 1-5 (native Button base-paint
    border suppression, not a hand-drawn-border rendering-technique tweak) — per the
    round-6 reasoning checkpoint, if this still shows double lines on the rig, every
    single-border-technique theory tried so far will be exhausted and a genuinely new
    mechanism will need to be considered. Cannot be pixel-confirmed without the user
    re-testing on the real Windows 11 rig with a fresh close-up screenshot of Identify at
    rest, hover, and pressed states.

- timestamp: 2026-08-11T20:30:00Z
  checked: |
    Round 6's real-world effect. User confirms it "looks better now" (double-line/mismatched-
    corner defect from round 5 is resolved — round 6's native-border suppression worked) but
    reports a NEW, more subtle asymmetry: the top and left border edges look thicker than the
    bottom and right edges. Asked the user to check whether Identify had keyboard focus in the
    test screenshot, since a separate accent-colored focus ring (MainForm.cs:1284-1293,
    conditional on btnIdentify.Focused) draws a second overlapping ring only in that state —
    user confirmed it was NOT focused (or not sure, but not visibly focused), ruling out the
    focus ring as the explanation. Also grepped every `btnIdentify.` reference in MainForm.cs
    (Enabled toggles, Size/Location layout, the FlatAppearance.BorderSize=0 line, the paint
    block itself, and all Mouse/Enter/Leave handlers which only call Invalidate()) and
    MainForm.Designer.cs (FlatStyle=Flat, event wiring, no FlatAppearance sub-properties set at
    design time) — found no other code path that draws or configures any border-like element
    on btnIdentify beyond the fill-based border (round 4-6) and the conditional focus ring
    (ruled out above).
  found: |
    The fill-based border's geometry itself is mathematically symmetric: outer rect spans
    bounds.X to bounds.X+Width (and Y to Y+Height); inner rect is
    (bounds.X+borderInset, bounds.Y+borderInset) sized (Width-2*borderInset,
    Height-2*borderInset) — by construction this places the inner rect's right edge at
    bounds.X+Width-borderInset and bottom edge at bounds.Y+Height-borderInset, i.e. an equal
    borderInset margin on all four sides, not an asymmetric one. With no other code-level
    border source found and the geometry itself symmetric, the remaining candidate is GDI+'s
    own rasterization behavior: `Graphics.FillPath` under `SmoothingMode.AntiAlias` with
    default `PixelOffsetMode` (`Default`, effectively `None`) is documented to exhibit a
    consistent top-left-vs-bottom-right coverage bias for rectangle-like paths — an artifact
    of how GDI+ samples pixel coverage from the classic Win32 "top/left inclusive,
    bottom/right exclusive" rectangle convention combined with anti-aliased edge sampling. This
    would explain a systematic (not random) 1-pixel-ish thickness bias toward the top/left on
    BOTH the outer and inner fills simultaneously, without needing any second border-drawing
    code path — a pure rasterization artifact, not a logic bug.
  implication: |
    Round 7 direction: set `e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;` (or
    `HighQuality`, which implies `Half`) before the border fills in BtnIdentify_Paint — this is
    the standard, well-documented GDI+ remedy for exactly this class of "one edge reads
    thicker" asymmetric anti-aliased-fill bias, since it shifts the pixel-center sampling
    convention GDI+ uses when rasterizing shape coverage. Scope narrowly to the border-drawing
    block (or the whole method, if that proves cleaner and doesn't visibly affect the text/
    focus-ring rendering) rather than a geometric workaround (e.g. manually asymmetric insets
    compensating in the opposite direction), since a PixelOffsetMode fix addresses the actual
    rasterization mechanism rather than papering over it with a magic-number correction that
    could break again at a different DPI scale or button size.

- timestamp: 2026-08-11T21:00:00Z
  checked: |
    ROUND 7. Implemented the round-7 direction (PixelOffsetMode.Half scoped to the border
    fills) exactly as specified: src/RigToggle.App/MainForm.cs BtnIdentify_Paint, inside the
    existing `if (!IsDark)` block, now sets `e.Graphics.PixelOffsetMode =
    PixelOffsetMode.Half;` immediately before the outer/inner border FillPath calls, wraps
    those two calls in try/finally, and restores `e.Graphics.PixelOffsetMode =
    PixelOffsetMode.Default;` in the finally block. No other lines in the method changed —
    round 6's BorderSize=0 (ApplyDashboardTheming) / `!IsDark` gate and round 4/5's fill-based
    technique with the 2px inset constant (IdentifyOwnerDrawnBorderInsetPx) are all preserved
    untouched. ThemeApplier.cs and btnSettings/SettingsForm code paths not touched.
    `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` — 0 warnings, 0 errors.
    `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass.
  found: Fix compiles cleanly, full existing test suite (no WinForms rendering coverage) passes unchanged.
  implication: |
    Self-verification is exhausted again for this environment (no Windows GUI). This round
    targets a pure GDI+ rasterization-convention mechanism (PixelOffsetMode's effect on
    AntiAlias FillPath coverage sampling), scoped as narrowly as possible (only the two
    border fills, restored immediately after) so it cannot regress the text render or focus
    ring, which remain at PixelOffsetMode.Default. Cannot be pixel-confirmed without the user
    re-testing on the real Windows 11 rig with a fresh close-up screenshot of just the
    Identify button showing all four edges.

## Eliminated

- hypothesis: "Adding FlatAppearance.BorderSize=1/BorderColor=ControlDark uniformly in ThemeApplier.ThemeButton (light mode only) is neutral-to-positive for every affected button including btnSaveSettings, requiring no special-casing."
  falsified_by: "2026-08-11T12:00:00Z rig evidence — btnSaveSettings (the form's AcceptButton) renders a double border on real hardware, confirmed via user screenshot 2.png. The AcceptButton's pre-existing native default-button visual treatment stacks with the new explicit FlatAppearance border instead of being neutral."

- hypothesis: "Identify's 'pressed down' look is caused by BtnIdentify_Paint's border geometry/draw-order itself — filling the full bounds first, then stroking a separately-inset ring afterward, is structurally an 'inset ring on top of a solid fill' that reads as sunken, distinct from a native flush flat border."
  falsified_by: "Re-derived the border-stroke offset geometry algebraically (round 2, this session): outward Minkowski offset of a rounded-rect path by half the pen width reconstructs exactly the original full-bounds rounded rect at the original corner radius, at every corner — the stroke's outer edge is provably flush with the fill path's true-bounds edge, not inset relative to it. The geometry/draw-order is not the defect. Replaced by the width-mismatch/anti-aliasing-softness hypothesis (see reasoning_checkpoint_round2), which is grounded in a concrete code discrepancy: the border pen width was a height-derived float (~1.25-1.5px) rather than the literal FlatAppearance.BorderSize (1px, matching the native buttons), and a non-integer anti-aliased pen straddles pixel rows producing a soft blurred band."

- hypothesis: "Identify's 'pressed down' look is caused by a non-integer-width anti-aliased pen (height-derived ~1.25-1.5px) straddling pixel rows and blurring, vs. the literal 1px FlatAppearance.BorderSize the native SettingsForm buttons render at."
  falsified_by: "2026-08-11T14:00:00Z rig evidence — IdentifyHeightPx=32 makes the 'buggy' height-derived formula (32 * 1/32) mathematically equal to the literal BorderSize of 1 (both = 1.0 exactly) at the 100% DPI scale used in every test screenshot. The round-2 fix changed nothing about the actual rendered pen width. User-provided screenshot 4.png (active MainForm, round-2 build) is visually identical to 1.png (active MainForm, round-1 build) — confirms the fix was a no-op and the 'pressed down' look is unchanged, not explained by this hypothesis. Also found BtnSettings_Paint draws no border at all (only a conditional accent-colored focus ring when Focused) — so 'looks like Settings' was never a same-mechanism comparison to validate against in the first place; Settings' apparent distinctness in test screenshots comes from incidentally retaining default keyboard focus, not from any border."

- hypothesis: "Disabling SmoothingMode (AntiAlias -> None) for just the border DrawPath call, restoring AntiAlias immediately after, resolves the pressed/sunken look while leaving the rounded corners intact."
  falsified_by: "2026-08-11T17:00:00Z rig evidence — user-provided close-up screenshot 5.png shows the straight-edge blending IS fixed (border reads solid/opaque now), but the rounded corners now show stair-step/notch artifacts ('upper left corner has some kind of rounding [artifact], right has a few pixels, bottom left as well') — GDI+'s non-antialiased rasterizer does not smooth curved GraphicsPath segments (BuildRoundedRect's arcs), so disabling AA fixed the line but broke the corners it shares the same DrawPath call with. Partial success only — traded one artifact for another. Replaced by the fill-based-border hypothesis (see reasoning_checkpoint entry, 2026-08-11T17:00:00Z Evidence) which avoids Pen/DrawPath (and therefore this whole class of AA-vs-crispness tradeoff) entirely."

- hypothesis: "Replacing the stroke-based border (Pen + DrawPath) with a fill-based border (two concentric AA'd FillPath calls) avoids GDI+'s thin-stroke AA softness entirely, since fills don't have the same known thin-line aliasing problem strokes do."
  falsified_by: "2026-08-11T18:30:00Z rig evidence — user reports round 4 looks identical to round 1/2's original pressed-down appearance, confirmed as a genuine on-rig observation (not a stale screenshot). The fill-based technique still has an anti-aliased edge (between the outer and inner fill) that blends across the same ~1px width the border occupies, reproducing the same softness via a different code path. The defect is not stroke-vs-fill; it's border WIDTH relative to GDI+'s AA blend radius at 1px. See Evidence entry above for full analysis and the round-5 direction (widen to 2px)."

- hypothesis: "Widening the fill-based border's inset from 1px to 2px (keeping the same technique) resolves the pressed look by reducing the AA-blend-region's share of the total border width."
  falsified_by: "2026-08-11T19:30:00Z rig evidence — user's detailed description ('double lines... upper left portion clearly has a rounded corner as well as a rectangular one... half of one border is overlapping with a normal button border') reveals the defect was never about a single border's width/crispness at all. There are TWO separate borders rendering simultaneously: a NATIVE square one (from FlatAppearance.BorderSize=1, set on btnIdentify by the same ThemeApplier.ThemeButton call used for every other themed button, never suppressed for this owner-drawn control across rounds 1-5) and the HAND-DRAWN rounded one every round 1-5 fix targeted. Widening the hand-drawn border's inset made the gap between the two borders larger and thus MORE visible as distinct double lines, not less — this hypothesis addressed the wrong border. See the 2026-08-11T19:30:00Z Evidence entry for the full root-cause analysis and round-6 fix direction (suppress the native border for btnIdentify specifically, decouple the hand-drawn border's draw condition from FlatAppearance.BorderSize)."

## Resolution

root_cause: "Seven compounding issues across seven rounds: (1) ThemeApplier.ThemeButton set FlatAppearance.BorderSize=0 unconditionally in light mode (only needed in dark mode to dodge dotnet/winforms#13897), so light-mode flat buttons had no border and relied on fill≈background-indistinguishable content alone. (2) The AcceptButton (Save Settings) additionally gets a native WinForms default-button ring (ButtonFlatAdapter, independent of FlatAppearance) which doubled up with the round-1 fix's explicit border. (3) Identify's border, unlike the sibling SettingsForm buttons' natively-rendered (GDI, non-anti-aliased) borders, was drawn via GDI+ Pen+DrawPath — a thin stroke on a rounded-rect path is structurally unable to be both crisply opaque on straight edges AND smoothly curved at corners within a single DrawPath call: AntiAlias-on blends the stroke's opacity asymmetrically (pixel-sampling proved a sunken/pressed 3D bevel shading pattern), while AntiAlias-off fixes the edges but stair-steps/notches the corner arcs (confirmed via rig screenshot 5.png). (4) Switched to a fill-based border (two concentric AntiAlias FillPath calls, no Pen) to eliminate the stroke mechanism entirely — but at only 1px total width, GDI+'s AntiAlias blend region (~1px) consumed nearly the entire visible border regardless of stroke-vs-fill technique, so the fill-based version still looked pressed/sunken (confirmed on-rig, round 4). (5) Widened the owner-drawn border's inset from the literal FlatAppearance.BorderSize (1px) to a separate, explicit 2px constant (IdentifyOwnerDrawnBorderInsetPx) scoped only to Identify's own paint handler — but this made a previously-hidden SIXTH issue visible instead of fixing anything: (6) btnIdentify is a stock Button (not owner-paint-suppressed), so WinForms' base OnPaint renders a NATIVE SQUARE FlatAppearance border (driven by the same BorderSize/BorderColor ThemeButton sets on every themed button) before the Paint event fires — this native square border was rendering underneath/around the hand-drawn rounded border across every round 1-5 attempt, invisible at 1px inset but exposed as literal double lines and mismatched rounded/square corners once round 5 widened the hand-drawn border's inset to 2px (rig screenshot 6.png). btnSettings never showed this because its fill is a same-shape, same-bounds square FillRectangle that fully masks a square native border. Round 6 fix: force `btnIdentify.FlatAppearance.BorderSize = 0` in ApplyDashboardTheming (after ThemeButton runs) to suppress the native border entirely for this control, and decouple BtnIdentify_Paint's hand-drawn-border presence check from the now-always-zero BorderSize by using `!IsDark` instead. FlatAppearance.BorderSize/BorderColor in ThemeApplier.cs itself stay unaffected for every other button, since the native SettingsForm buttons consume that value directly and are already confirmed correct. (7) With the double-border defect resolved, a seventh, subtler issue surfaced: the top/left border edges read visibly thicker than bottom/right, even though the round 4-6 fill geometry is mathematically symmetric (equal borderInset margin on all sides, re-derived algebraically) and no second border-drawing code path exists (confirmed by exhaustive grep of every btnIdentify. reference). This matches GDI+'s documented top-left-vs-bottom-right AntiAlias FillPath coverage bias for rectangle-like paths under the default PixelOffsetMode — a pure rasterization-convention artifact, not a logic bug. Round 7 fix: set `e.Graphics.PixelOffsetMode = PixelOffsetMode.Half` scoped to just the two border FillPath calls (restored to Default immediately after in a finally block), shifting GDI+'s pixel-center sampling convention to the standard remedy for this exact bias class."
fix: |
  1. src/RigToggle.App/ThemeApplier.cs, ThemeButton(): FlatAppearance.BorderSize is now
     `(dark || isFormDefaultButton) ? 0 : 1` (was unconditionally 0), BorderColor set to
     SystemColors.ControlDark. Dark mode is untouched. The AcceptButton (form default button)
     is excluded from the explicit border because it already gets a native default-button
     ring from WinForms regardless of FlatAppearance — this fixes SettingsForm's
     btnBrowse/btnClearAppPath/btnDiscardChanges/btnSaveSettings in light mode.
  2. src/RigToggle.App/MainForm.cs, BtnIdentify_Paint(): added an explicit border stroke
     (reading btnIdentify.FlatAppearance.BorderSize/BorderColor, the values ThemeButton set)
     around the rounded-rect fill path when BorderSize > 0, using the literal integer
     BorderSize (not a height-derived float) with a half-pixel inset for crisp alignment.
     Necessary because this handler's own FillPath call covers the full ClientRectangle every
     repaint, erasing any native border.
  3. src/RigToggle.App/MainForm.cs, BtnIdentify_Paint() (round 3, SUPERSEDED by round 4 below):
     wrapped the border DrawPath call with `e.Graphics.SmoothingMode = SmoothingMode.None`
     immediately before and restored to `SmoothingMode.AntiAlias` immediately after. Fixed the
     asymmetric blended/sunken-looking stroke but introduced corner stair-step/notch artifacts
     (rig screenshot 5.png) — this step's code no longer exists in the current fix, replaced
     by step 4.
  4. src/RigToggle.App/MainForm.cs, BtnIdentify_Paint() (round 4, current): replaced the
     entire stroke-based border block (Pen + DrawPath + SmoothingMode toggling from steps
     2-3) with a fill-based border. When FlatAppearance.BorderSize > 0: FillPath a rounded
     rect at the full ClientRectangle bounds with FlatAppearance.BorderColor first, then
     FillPath a second rounded rect inset by exactly BorderSize on all sides (corner radius
     reduced by BorderSize, clamped to >= 0) with ManualButtonFill's hover/press-aware color
     on top — the inset fill covers all but a uniform BorderSize-wide margin, which is the
     visible border. When BorderSize == 0 (dark mode): unchanged single full-bounds fill,
     byte-for-byte the original dark-mode behavior. SmoothingMode.AntiAlias stays on for the
     whole method, never toggled — no Pen or DrawPath used for the border at all, removing
     the entire class of thin-stroke AA tradeoff that caused rounds 2-3's issues. Text render
     and the conditional focus ring are unaffected. BtnSettings_Paint was deliberately left
     unchanged throughout all four rounds — never reported broken.
  5. src/RigToggle.App/MainForm.cs (round 5, current): round 4's fill-based technique is
     unchanged (same two-FillPath structure, same BorderColor/fill-color sourcing, same
     `borderSize > 0` presence gate reading FlatAppearance.BorderSize). Added a new private
     const `IdentifyOwnerDrawnBorderInsetPx = 2f` near the other Identify layout constants,
     and changed only the inset/corner-radius-reduction math inside the fill-based border
     block to use this constant instead of the literal `borderSize` (1px) variable. This
     doubles the visible margin width so roughly half of it stays outside GDI+'s ~1px
     AntiAlias blend region instead of nearly the entire 1px margin being blend-affected.
     ThemeApplier.cs is untouched — FlatAppearance.BorderSize remains 1 for all buttons,
     so the native SettingsForm buttons (Discard Changes/Browse/Clear/Save Settings, already
     confirmed correct) are unaffected.
  6. src/RigToggle.App/MainForm.cs (round 6, current): (a) ApplyDashboardTheming — added
     `btnIdentify.FlatAppearance.BorderSize = 0;` immediately after
     `ThemeApplier.ThemeButton(btnIdentify, IsDark);`, forcing off the NATIVE square
     FlatAppearance border WinForms' base Button.OnPaint renders before the Paint event
     fires (this was rendering underneath/around the hand-drawn rounded border since round
     1, unnoticed until round 5's wider inset exposed it as double lines). BorderColor is
     untouched — still read by BtnIdentify_Paint's fill-based border below. (b)
     BtnIdentify_Paint — changed the hand-drawn border block's gate from
     `if (btnIdentify.FlatAppearance.BorderSize > 0)` (now permanently false) to
     `if (!IsDark)`, preserving the exact same light-mode-only presence behavior via a
     property decoupled from the now-forced-zero native value. Rounds 4/5's fill-based
     technique and 2px inset constant inside the block are unchanged. ThemeApplier.cs and
     btnSettings/SettingsForm code paths untouched this round.
  7. src/RigToggle.App/MainForm.cs (round 7, current): inside the existing `if (!IsDark)`
     block (round 6, otherwise unchanged), set `e.Graphics.PixelOffsetMode =
     PixelOffsetMode.Half;` immediately before the outer/inner border FillPath calls,
     wrapped those two calls in a try/finally, and restore
     `e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;` in the finally block. Fixes a
     GDI+ AntiAlias FillPath rasterization bias (top-left edges reading thicker than
     bottom-right) by shifting the pixel-center sampling convention, scoped to only the two
     border fills — TextRenderer.DrawText and the conditional focus-ring DrawPath remain at
     PixelOffsetMode.Default, untouched. Rounds 4-6's technique, geometry, and gating are all
     otherwise unchanged.
verification: |
  Round 1 self-verified (build + tests passed) but rig-verification (2026-08-11T12:00:00Z)
  found two regressions: Save Settings double border, Identify "pressed down" look.
  Round 2 fixed (A) Save Settings double border by suppressing the explicit border on the
  AcceptButton — CONFIRMED FIXED by the user, do not revisit. Round 2's fix for (B) Identify's
  "pressed down" look (pen-width formula) was mathematically a no-op at IdentifyHeightPx=32 and
  did not change the rendering (confirmed via pixel-identical before/after screenshots).
  Round 3: pixel-sampled the EXISTING rig screenshots (1.png, 2.png) directly with PIL in this
  Linux environment to measure actual rendered RGB values rather than reasoning about GDI+
  behavior abstractly. Found and fixed the real mechanism (AntiAlias coverage-blending on the
  border stroke specifically, producing an asymmetric sunken-bevel-like shading pattern).
  - `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` succeeds, 0 warnings/errors.
  - `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass (no WinForms UI
    coverage in this suite; does not exercise the changed paint/theme code).
  - Confirmed no impact to dark mode, to the fill/color values (unchanged from round 2), to
    the focus ring (still AntiAlias, unaffected), or to the three CONFIRMED-correct
    SettingsForm buttons (native rendering path, untouched by this round's change).
  Round 3 rig-verified: straight-edge blending fixed, but new corner stair-step/notch
  artifacts appeared (rig screenshot 5.png) — SmoothingMode.None was too blunt for a single
  Pen/DrawPath call that strokes both straight edges and curved corner arcs. Falsified and
  replaced.
  Round 4: replaced the stroke-based border entirely with a fill-based border (two ordinary
  AntiAlias FillPath calls, no Pen/DrawPath anywhere in the border logic) — structurally
  removes the mechanism identified as the root cause of rounds 2-3's issues rather than
  tuning a parameter within it.
  - `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` succeeds, 0 warnings/errors.
  - `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass (no WinForms UI
    coverage in this suite; does not exercise the changed paint/theme code).
  - Confirmed no impact to dark mode (falls through to the original unchanged single-fill
    path), to the fill/color source values (ManualButtonFill unchanged), to the focus ring
    (still AntiAlias, unaffected), or to the three CONFIRMED-correct SettingsForm buttons
    (native rendering path, untouched by any round's change to MainForm.cs).
  Round 4 rig-verified: NOT resolved — user confirmed round 4's on-rig appearance looks
  identical to round 1/2's original pressed-down look. Root cause narrowed to border WIDTH
  (1px) relative to GDI+'s AntiAlias blend radius (~1px), not stroke-vs-fill technique.
  Round 5: widened the owner-drawn border inset from 1px to 2px via a new, explicitly-scoped
  constant (IdentifyOwnerDrawnBorderInsetPx), keeping round 4's fill-based technique
  unchanged (never falsified as a method — only its 1px width was falsified).
  - `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` succeeds, 0 warnings/errors.
  - `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass (no WinForms UI
    coverage in this suite; does not exercise the changed paint code).
  - Confirmed no impact to dark mode (unaffected — this constant is only read inside the
    `borderSize > 0` branch, which dark mode never enters), to ThemeApplier.cs/FlatAppearance.
    BorderSize (unchanged, still 1, still consumed natively by the confirmed-correct
    SettingsForm buttons), to the focus ring (still AntiAlias, unaffected), or to
    BtnSettings_Paint (untouched across all five rounds).
  Round 5 rig-verified: NOT resolved, but for a NEW reason — user's detailed description of
  6.png revealed a previously-unconsidered mechanism (native square FlatAppearance border
  rendering alongside the hand-drawn rounded one, invisible at 1px inset, exposed as literal
  double lines at 2px inset). The width-tuning hypothesis itself was not re-falsified on its
  own terms; it simply targeted the wrong border.
  Round 6: force-suppresses the native FlatAppearance border for btnIdentify specifically
  (BorderSize=0, set after ThemeApplier.ThemeButton) and decouples the hand-drawn border's
  presence check from the now-always-zero BorderSize (uses !IsDark instead). This is a
  structurally different mechanism than rounds 1-5, which only ever tuned the hand-drawn
  border's own rendering technique.
  - `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` succeeds, 0 warnings/errors.
  - `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass (no WinForms UI
    coverage in this suite; does not exercise the changed paint/theme code).
  - Confirmed no impact to dark mode (BtnIdentify_Paint's `!IsDark` check is false in dark
    mode, identical presence behavior to the old `BorderSize > 0` check which was also always
    false in dark mode), to ThemeApplier.cs (untouched this round, still has round 2's
    AcceptButton fix), to btnSettings (its own ThemeButton call and paint handler untouched),
    or to the SettingsForm native buttons (native rendering path, never touched by any round's
    change to MainForm.cs).
  Round 6 rig-verified: CONFIRMED FIXED by the user — the double-line/mismatched-corner
  defect is gone ("looks better now"). Do not revert or modify round 6's BorderSize=0/
  !IsDark changes. New, subtler report surfaced: top/left border edges look thicker than
  bottom/right.
  Round 7: scoped `e.Graphics.PixelOffsetMode = PixelOffsetMode.Half` to just the two border
  FillPath calls (restored to Default in a finally block immediately after) — targets GDI+'s
  documented top-left-vs-bottom-right AntiAlias FillPath coverage bias directly, after
  confirming (2026-08-11T20:30:00Z Evidence) that the fill geometry itself is symmetric and
  no second border-drawing code path exists.
  - `dotnet build src/RigToggle.App/RigToggle.App.csproj -c Debug` succeeds, 0 warnings/errors.
  - `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` — 82/82 pass (no WinForms UI
    coverage in this suite; does not exercise the changed paint code).
  - Confirmed no impact to dark mode (PixelOffsetMode.Half is only set inside the `!IsDark`
    branch), to ThemeApplier.cs (untouched this round), to the text render or focus ring
    (both remain at PixelOffsetMode.Default, outside the try/finally scope), or to round 6's
    BorderSize=0/!IsDark changes (unmodified).
  Round 7 rig-verified: CONFIRMED FIXED by the user — "Yes. I can confirm it looks good in
  both modes." (2026-08-11T21:00:00Z). All four Identify border edges now read as equal
  thickness in light mode, and dark mode (unaffected by any of the seven rounds' changes,
  which are all scoped inside `!IsDark`/`dark ? ... : ...` branches) remains correct. Combined
  with round 2's separately-confirmed Save Settings fix and round 6's separately-confirmed
  double-border fix, every symptom reported across this session's seven rounds is now
  resolved and rig-verified on real Windows 11 hardware. Session closed.
files_changed:
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.App/MainForm.cs
