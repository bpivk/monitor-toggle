---
phase: quick-260829-fnt
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - src/RigToggle.Core/UpdateOrchestrator.cs
  - src/RigToggle.Tests/UpdateOrchestratorTests.cs
  - src/RigToggle.App/AboutForm.cs
  - src/RigToggle.App/AboutForm.Designer.cs
  - src/RigToggle.App/ThemeApplier.cs
  - src/RigToggle.App/MainForm.Designer.cs
  - src/RigToggle.App/MainForm.cs
autonomous: false
requirements: [UPDATE-06]

estimate:
  tokens: 55000
  raw_tokens: 55000
  tasks: 3
  confidence: low

must_haves:
  truths:
    - "MainForm carries a menu bar whose Help menu contains a single About item (UPDATE-06: a third, more discoverable manual-check entry point)"
    - "Help > About opens a modal dialog naming the app and its running version in full Major.Minor.Patch form (e.g. 2.2.1, never 2.2 and never 2.2.1.0)"
    - "The About dialog's Check for Updates button runs the exact same check the tray item and the Settings button already run, via MainForm.PerformManualUpdateCheck (UPDATE-06)"
    - "The tray Check for Updates item and the Settings dialog Check for Updates button both still work exactly as before — additive only"
    - "The menu bar and the About dialog render legibly in both light and dark themes, following the app's existing ThemeApplier pattern"
    - "The tile row, the Identify/toggle action row and the Settings gear are not overlapped by the new menu bar, on both the visible and the --tray startup path"
  artifacts:
    - "src/RigToggle.App/AboutForm.cs — themed modal About dialog"
    - "src/RigToggle.App/AboutForm.Designer.cs — its layout, following UpdatePromptDialog.Designer.cs's convention"
    - "src/RigToggle.App/ThemeApplier.cs — new ThemeMenuStrip(MenuStrip, bool) targeted recolor helper"
    - "src/RigToggle.App/MainForm.Designer.cs — menuStrip / helpMenuItem / helpAboutMenuItem, MainMenuStrip assignment"
    - "src/RigToggle.App/MainForm.cs — HelpAboutMenuItem_Click, ShowAboutDialog, ThemeMenuStrip call, LayoutDashboard menu offset"
    - "src/RigToggle.Core/UpdateOrchestrator.cs — public FormatDisplayVersion(Version) + RunningVersionText"
    - "src/RigToggle.Tests/UpdateOrchestratorTests.cs — FormatDisplayVersion cases"
  key_links:
    - "UpdateOrchestrator.FormatDisplayVersion is the ONE running-version-text implementation — CheckAsync's up-to-date balloon and the About dialog's version label must both read from it, or the two displays drift the way the recent Major.Minor truncation bug already made them drift once"
    - "ApplyDashboardTheming is the single shared theming helper both MainForm call sites (OnThemeChanged, InitializeTrayState) invoke — the ThemeMenuStrip call goes THERE, never into either call site directly (19-RESEARCH.md Pitfall 1, the drift bug this codebase shipped twice)"
    - "LayoutDashboard's stripTop must absorb the docked MenuStrip's height — every other coordinate in that method cascades from stripTop, and WinForms does NOT shift absolutely-positioned siblings out from under a docked control"
    - "AboutForm's Check for Updates button must invoke MainForm.PerformManualUpdateCheck, never a re-implementation, so it inherits the _updateCheckInProgress Interlocked reentrancy guard that already protects the tray and Settings entry points"
---

<objective>
Add a traditional `MenuStrip` to `MainForm` with a `Help` menu containing a single `About`
item, opening a small themed modal `AboutForm` that shows the app name, the running version,
a `Check for Updates` button wired to the existing `MainForm.PerformManualUpdateCheck()`, and
a `Close` button.

Purpose: `Check for Updates` (UPDATE-06) is currently reachable only from the tray context
menu and from inside the Settings dialog — neither is discoverable from the main window. A
Help > About entry is the conventional place users look, and it doubles as the app's version
readout, which today only appears transiently in an update balloon.

Output: A new `AboutForm` (+ Designer), a new `ThemeApplier.ThemeMenuStrip` helper, the
`MainForm` menu bar and its wiring, and a single shared running-version formatter in
`UpdateOrchestrator` so the About dialog and the update balloon can never disagree.

Strictly additive. The tray `Check for Updates` item and the Settings dialog
`Check for Updates` button are NOT removed, moved, renamed or otherwise altered.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md

@src/RigToggle.Core/UpdateOrchestrator.cs
@src/RigToggle.App/UpdatePromptDialog.cs
@src/RigToggle.App/UpdatePromptDialog.Designer.cs
@src/RigToggle.App/ThemeApplier.cs
@src/RigToggle.App/MainForm.Designer.cs

Large files — do NOT read whole, use targeted Read offsets / Grep:

- `src/RigToggle.App/MainForm.cs` (2595 lines). Relevant anchors, verified at plan time:
  - `_updateOrchestrator` field + `_updateCheckInProgress` guard — lines 45-60
  - `IsDark` / `AccentColor` properties — lines 273-281
  - `InitializeTrayState()` (the `--tray`-safe timing point) — lines 367-393
  - `OnThemeChanged` — lines 242-268
  - `LayoutDashboard()` — lines 1378-1469 (`stripTop` is line 1418)
  - `ApplyDashboardTheming()` — lines 1490-1522
  - `TrayCheckUpdatesMenuItem_Click` — line 2067 (put the new Help>About handler beside it)
  - `ShowUpdatePromptDialog` — lines 2270-2282 (the owner convention to copy)
  - `PerformManualUpdateCheckAsync` / `PerformManualUpdateCheck` — lines 2327-2405
- `src/RigToggle.App/SettingsForm.cs` (74 KB). Only anchor needed: `BtnCheckForUpdates_Click`
  at line 1220 — read it to confirm the existing pattern, and change NOTHING in this file.
- `src/RigToggle.App/Program.cs` — composition root, lines 290-352. The running version is
  resolved at line 299 and injected into `UpdateOrchestrator` at line 300; the
  `SettingsFormFactory` at line 350 already threads `mainForm.PerformManualUpdateCheck`
  through. No change is needed in this file.
- `src/RigToggle.Tests/UpdateOrchestratorTests.cs` (533 lines) — read the top ~45 lines for
  the fixture conventions (`RunningVersion` constant, xunit style) before adding tests.

Environment facts confirmed at plan time:
- `dotnet` 10.0.302 is available and the whole solution — including the `net10.0-windows`
  WinForms `RigToggle.App` project — COMPILES on this Linux host (`dotnet build RigToggle.sln`
  succeeds). It cannot be RUN or visually inspected here; that is Task 3's job.
- Baseline `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj` = 223 passed, exit 0.
  Run the test project directly; `dotnet test RigToggle.sln` aborts trying to launch the
  non-test WinForms assembly.
- `RigToggle.Tests` references `RigToggle.Core` ONLY. Nothing in `RigToggle.App` is unit
  testable from there — which is why the version formatter belongs in Core.
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: One shared running-version display formatter in Core</name>
  <files>src/RigToggle.Core/UpdateOrchestrator.cs, src/RigToggle.Tests/UpdateOrchestratorTests.cs</files>
  <read_first>
    src/RigToggle.Core/UpdateOrchestrator.cs lines 44-60 (fields/ctor) and 149-155 (the
    existing inline `runningVersionText` local and its explanatory comment).
    src/RigToggle.Tests/UpdateOrchestratorTests.cs lines 1-45 (fixture conventions).
  </read_first>
  <behavior>
    - `FormatDisplayVersion(new Version(2, 2, 1))` returns "2.2.1"
    - `FormatDisplayVersion(new Version(2, 2))` returns "2.2.0" — a two-component Version
      reports Build == -1 and must normalize to 0, never render "2.2.-1"
    - `FormatDisplayVersion(new Version(2, 2, 1, 7))` returns "2.2.1" — the fourth
      (revision) component is dropped, matching this project's vX.Y.Z tag scheme
    - `FormatDisplayVersion(new Version(0, 0))` returns "0.0.0" — the Program.cs fallback
      version when the entry assembly reports none
    - `FormatDisplayVersion(null)` throws ArgumentNullException
  </behavior>
  <action>
    Extract the running-version display string that currently lives as an inline
    interpolation inside `CheckAsync` (the `runningVersionText` local, around line 154) into a
    reusable member, so the About dialog added in Task 2 renders the SAME text as the
    "already on the latest version" balloon rather than re-deriving its own.

    Add to `UpdateOrchestrator`: a `public static string FormatDisplayVersion(Version version)`
    producing Major, Minor and Build joined by dots, with Build normalized through
    `Math.Max(..., 0)`; guard a null argument with `ArgumentNullException` matching the
    constructor's existing guard style. Move the existing explanation onto this member as its
    XML doc comment (three-component Major.Minor.Patch matching the project's vX.Y.Z tag
    scheme; never the four-component `System.Version.ToString()`, which would read as a
    four-segment string in a balloon; Build normalized because a two-component running
    `Version` reports Build as negative one). Also add
    `public string RunningVersionText => FormatDisplayVersion(_runningVersion);`.

    Then replace the inline interpolation inside `CheckAsync` with a read of
    `RunningVersionText`. The produced value must be byte-identical to what that method
    produces today — this is a pure extraction, not a behavior change; every existing
    `UpdateCheckResult.RunningVersionText` payload stays exactly as it was.

    Write the tests first (they fail to compile until the member exists), then implement.
    Name each test method so its fully-qualified name contains `FormatDisplayVersion` — the
    verify gate filters on that substring. Follow the file's existing xunit style; a single
    `[Theory]` with `[InlineData]` rows plus one `[Fact]` for the null guard is sufficient.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza &amp;&amp; dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo -v q --filter "FullyQualifiedName~FormatDisplayVersion" 2>&amp;1 | tail -3 &amp;&amp; dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo -v q 2>&amp;1 | tail -3</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; grep -v '^[[:space:]]*//' src/RigToggle.Core/UpdateOrchestrator.cs | grep -c 'FormatDisplayVersion'</automated>
  </verify>
  <done>
    The filtered run reports at least 5 passing tests and zero failures; the full run reports
    at least 228 passed (223 baseline + the new cases) and zero failures, exit 0. The filtered
    grep counts at least 3 code-line occurrences of `FormatDisplayVersion` (the static method,
    the `RunningVersionText` property body, and — indirectly — no remaining inline duplicate:
    `grep -v '^[[:space:]]*//' src/RigToggle.Core/UpdateOrchestrator.cs | grep -c 'Math.Max(_runningVersion.Build'`
    must be 0, proving the inline copy in `CheckAsync` is gone rather than duplicated).
  </done>
</task>

<task type="auto">
  <name>Task 2: MenuStrip Help &gt; About on MainForm, opening a themed AboutForm</name>
  <files>src/RigToggle.App/AboutForm.cs, src/RigToggle.App/AboutForm.Designer.cs, src/RigToggle.App/ThemeApplier.cs, src/RigToggle.App/MainForm.Designer.cs, src/RigToggle.App/MainForm.cs</files>
  <read_first>
    src/RigToggle.App/UpdatePromptDialog.cs and UpdatePromptDialog.Designer.cs IN FULL — they
    are the exact structural template for AboutForm (themed transient dialog: ctor theming
    block, ThemeChanged subscribe + FormClosed unsubscribe + Dispose backstop, IsDark
    property, marshalled OnThemeChanged, Designer-file layout convention).
    src/RigToggle.App/MainForm.Designer.cs IN FULL (it is small).
    src/RigToggle.App/ThemeApplier.cs class doc comment (lines 7-24) and `ThemeButton` /
    `ThemeFormSurface` (lines 149-172, 348-369) for the helper conventions and the exact
    color literals to reuse.
    src/RigToggle.App/MainForm.cs at the line anchors listed in `<context>` — targeted reads
    only, do not read the whole file.
  </read_first>
  <action>
    Four coordinated edits. Purely additive: do not remove, rename, reorder or re-wire
    `trayCheckUpdatesMenuItem`, `TrayCheckUpdatesMenuItem_Click`, `traySettingsMenuItem`,
    `trayToggleMenuItem`, `traySeparator`, `trayExitMenuItem`, or anything in
    `SettingsForm.cs` / `Program.cs`. Those files are out of scope for this task.

    (a) New `AboutForm` (`AboutForm.cs` + `AboutForm.Designer.cs`), mirroring
    `UpdatePromptDialog`'s structure exactly, including its `using` block. Constructor
    signature: `public AboutForm(string versionText, IThemeProvider themeProvider, Action? performManualUpdateCheck)`.
    Guard `versionText` and `themeProvider` with `ArgumentNullException`; a null
    `performManualUpdateCheck` is legal and means "no update orchestrator is wired" — set
    `btnCheckForUpdates.Enabled = false` in that case rather than shipping a button that
    silently does nothing. Wire the button's `Click` to invoke the delegate. Set
    `lblVersion.Text` from `versionText` prefixed with the word Version. Set both
    `AcceptButton` and `CancelButton` to `btnClose` so Enter and Esc both dismiss. Copy
    `UpdatePromptDialog`'s theming block verbatim in shape: `ThemeApplier.ApplyEffectiveColorMode(IsDark)`,
    `DwmTitleBar.ApplyRoundedCornersAndMica(Handle, IsDark)`, then `ThemeApplier.ThemeButton`
    for each of the two buttons; the same `_themeProvider.ThemeChanged += OnThemeChanged` /
    `FormClosed`-unsubscribe pair; the same `Dispose(bool)` backstop unsubscribe; the same
    `IsDark` expression-bodied property; the same marshalled, try/caught `OnThemeChanged`
    re-theming the same set. Do NOT call `ThemeApplier.ThemeFormSurface` — that helper's own
    doc comment states it is for `MainForm` only, and `UpdatePromptDialog` correctly does not
    call it. Do NOT set label `ForeColor` explicitly, for the same reason
    `UpdatePromptDialog.lblHeadline` does not.

    Designer layout (all values at 100% scale, `AutoScaleMode.Font` handles the rest, same as
    UpdatePromptDialog): `lblAppName` at Point(12, 12), Size(336, 26), `AutoSize` false, Font
    "Segoe UI" 12F Bold, Text "Rig Toggle". `lblVersion` at Point(12, 44), Size(336, 20),
    `AutoSize` false (Text assigned in the constructor). `btnCheckForUpdates` Text
    "Check for Updates" at Point(12, 108), MinimumSize(150, 32), Height 32, `AutoSize` true
    with `AutoSizeMode.GrowAndShrink`, `FlatStyle.Flat`. `btnClose` Text "Close" at
    Point(258, 108), MinimumSize(90, 32), Height 32, `AutoSize` true with
    `AutoSizeMode.GrowAndShrink`, `DialogResult.Cancel`, `FlatStyle.Flat`. Form:
    `AutoScaleDimensions` SizeF(7F, 15F), `AutoScaleMode.Font`, `ClientSize` Size(360, 152),
    `FormBorderStyle.FixedDialog`, `MaximizeBox` false, `MinimizeBox` false, `ShowInTaskbar`
    false, `StartPosition.CenterParent`, Text "About Rig Toggle", Name "AboutForm".
    `Controls.Add` in the order lblAppName, lblVersion, btnCheckForUpdates, btnClose.
    Nothing else — no changelog, no scrolling text box, no license block.

    (b) New `ThemeApplier.ThemeMenuStrip(MenuStrip menu, bool dark)`, following every rule in
    that class's own doc comment: idempotent, wrapped in try/catch, never throws, targeted at
    the one instance the caller passes rather than a Controls-tree walk. Set the strip's
    `BackColor` to the same two literals `ThemeFormSurface` uses (so the bar reads as part of
    the window surface) and its `ForeColor` to the same two literals `ThemeButton` uses (so
    menu text matches button text). Then apply the same pair to each top-level
    `ToolStripItem` in `menu.Items` and, for each item that is a `ToolStripMenuItem`, to its
    `DropDown` and each of its `DropDownItems`. Document in the method's doc comment that this
    two-level walk is deliberately shallow and exhaustive for this menu's fixed
    Help-then-About shape, that it is NOT the recursive Controls-tree walk the class doc
    forbids, and that the known stale-color-on-live-flip ToolStrip limitation already
    documented on `trayContextMenu` in MainForm.Designer.cs (dotnet/winforms#12027) applies
    here too and is accepted, not to be chased with a custom `ToolStripRenderer`.

    (c) `MainForm.Designer.cs`: declare three new fields — `menuStrip` (`MenuStrip`),
    `helpMenuItem` and `helpAboutMenuItem` (both `ToolStripMenuItem`). Instantiate them in
    `InitializeComponent` alongside the existing control instantiations. Do NOT pass
    `this.components` to the `MenuStrip` constructor — it is a `Control` added to
    `this.Controls` and disposed with the form, unlike `notifyIcon`/`trayContextMenu`. Give
    `helpAboutMenuItem` Text "About", its Name, and a `Click` handler pointing at
    `this.HelpAboutMenuItem_Click`. Give `helpMenuItem` Text "Help", its Name, and add
    `helpAboutMenuItem` to its `DropDownItems`. Give `menuStrip` its Name,
    `Dock = DockStyle.Top`, and add `helpMenuItem` to its `Items`. In the MainForm section set
    `this.MainMenuStrip = this.menuStrip;` and append `this.Controls.Add(this.menuStrip);`
    AFTER the five existing `Controls.Add` calls, with a comment recording that it is appended
    last deliberately so the documented D-09 reading/tab order (tiles, Identify, toggle,
    Settings gear) is left byte-for-byte untouched.

    (d) `MainForm.cs`, three edits:
    - Add `private void HelpAboutMenuItem_Click(object? sender, EventArgs e) => ShowAboutDialog();`
      beside `TrayCheckUpdatesMenuItem_Click` (around line 2067), plus a
      `private void ShowAboutDialog()` that resolves the version text as
      `_updateOrchestrator?.RunningVersionText` falling back — only for the null-orchestrator
      test-harness case — to `UpdateOrchestrator.FormatDisplayVersion` applied to
      `System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version` with
      `new Version(0, 0)` as the final fallback, mirroring Program.cs line 299 exactly so the
      two never disagree. Pass `PerformManualUpdateCheck` as the delegate when
      `_updateOrchestrator` is non-null and `null` when it is null. Construct and show the
      dialog with `using var` and the SAME owner convention `ShowUpdatePromptDialog` uses —
      owned by `this` only when this form is currently `Visible`, unowned otherwise, because
      under `--tray` hidden startup the main window is never shown. Wrap the whole body in
      try/catch that traces the failure via `System.Diagnostics.Trace.WriteLine` and swallows
      it: opening an informational dialog must never crash the toggle flow, matching this
      file's existing convention for cosmetic/non-critical failures.
    - In `ApplyDashboardTheming()`, add `ThemeApplier.ThemeMenuStrip(menuStrip, IsDark);`
      immediately after the existing `ThemeApplier.ThemeFormSurface(this, IsDark);` line.
      It goes in THIS helper and nowhere else — that is precisely what keeps the two theming
      call sites (`OnThemeChanged` and `InitializeTrayState`) structurally unable to drift, per
      the 19-RESEARCH.md Pitfall 1 rule that method's doc comment already states.
    - In `LayoutDashboard()`, offset `stripTop` (line 1418) by the menu bar's height. A
      `Dock = Top` MenuStrip occupies the top of the CLIENT area, but every control this
      method positions uses an explicit client-relative `Location`, and WinForms does not
      shift absolutely-positioned siblings out from under a docked control — without the
      offset the tile row renders underneath the menu bar. Read the height as
      `Math.Max(menuStrip.Height, menuStrip.PreferredSize.Height)`, not `Height` alone, for
      exactly the reason the `count > 0` rig fix immediately above it exists:
      `InitializeTrayState()` runs this method before the form has ever been shown on every
      startup path including `--tray`, so a layout pass may not yet have resolved the strip's
      autosized `Height`, whereas `PreferredSize` is computed on demand from the items and
      font. Do NOT wrap the result in `Scaled()` — the strip already autosizes with the form
      font, so scaling it again would double-count. Record all of that as a comment. Nothing
      else in the method changes: `contentBottom`, `actionRowY`, `btnSettings.Bottom` and the
      final `ClientSize` all cascade from `stripTop`, so the window grows by exactly the menu
      bar's height on its own.

    Scope discipline: if the menu bar's dark-mode rendering turns out imperfect on the rig,
    that is a Task 3 finding to report, not a reason to pre-emptively build a custom
    `ToolStripRenderer` here.
  </action>
  <verify>
    <automated>cd /home/bpivk/moza &amp;&amp; dotnet build RigToggle.sln --nologo -v q 2>&amp;1 | tail -5</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; for f in src/RigToggle.App/AboutForm.cs src/RigToggle.App/AboutForm.Designer.cs; do test -f "$f" &amp;&amp; echo "OK $f" || echo "MISSING $f"; done</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; echo "themeMenuStripHelper=$(grep -v '^[[:space:]]*//' src/RigToggle.App/ThemeApplier.cs | grep -c 'public static void ThemeMenuStrip')" &amp;&amp; echo "themeCall=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.cs | grep -c 'ThemeApplier.ThemeMenuStrip')" &amp;&amp; echo "layoutOffset=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.cs | grep -c 'menuStrip.PreferredSize')" &amp;&amp; echo "aboutHandler=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.cs | grep -c 'HelpAboutMenuItem_Click')" &amp;&amp; echo "reusesManualCheck=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.cs | grep -c 'new AboutForm')"</automated>
    <automated>cd /home/bpivk/moza &amp;&amp; echo "trayItemIntact=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.Designer.cs | grep -c 'trayCheckUpdatesMenuItem')" &amp;&amp; echo "traySettingsIntact=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.Designer.cs | grep -c 'traySettingsMenuItem')" &amp;&amp; echo "settingsBtnIntact=$(grep -v '^[[:space:]]*//' src/RigToggle.App/SettingsForm.cs | grep -c 'BtnCheckForUpdates_Click')" &amp;&amp; echo "settingsFormDiff=$(git diff --name-only -- src/RigToggle.App/SettingsForm.cs src/RigToggle.App/SettingsForm.Designer.cs src/RigToggle.App/Program.cs | wc -l)" &amp;&amp; echo "menuInDesigner=$(grep -v '^[[:space:]]*//' src/RigToggle.App/MainForm.Designer.cs | grep -c 'helpAboutMenuItem')"</automated>
  </verify>
  <done>
    `dotnet build RigToggle.sln` reports "Build succeeded", 0 errors. Both AboutForm files
    report OK. `themeMenuStripHelper` is 1, `themeCall` is 1, `layoutOffset` is at least 1,
    `aboutHandler` is at least 1, `reusesManualCheck` is 1, `menuInDesigner` is at least 4.
    The additive-preservation gates hold: `trayItemIntact` is at least 4, `traySettingsIntact`
    is at least 3, `settingsBtnIntact` is at least 1, and `settingsFormDiff` is 0 (this task
    touched neither SettingsForm nor Program.cs).
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: Rig verification — menu bar layout, About dialog, theming, additive check</name>
  <precondition>A Windows machine able to build and run `RigToggle.App.exe` natively. The
  net10.0-windows project compiles on the Linux dev host but cannot be launched or visually
  inspected there, so nothing in Tasks 1-2 proves how the menu bar or the About dialog
  actually render.</precondition>
  <what-built>
    A `Help` menu bar on the main window whose single `About` item opens a small modal dialog
    showing "Rig Toggle", the running version, a `Check for Updates` button and a `Close`
    button. The button calls the same `PerformManualUpdateCheck` the tray item and the
    Settings button already call. The main window's layout was offset downward by the menu
    bar's height, and the menu bar plus both dialog buttons were wired into the app's existing
    ThemeApplier theming.
  </what-built>
  <how-to-verify>
    Build and launch on the rig: `dotnet build RigToggle.sln`, then run
    `src/RigToggle.App/bin/Debug/net10.0-windows/RigToggle.App.exe`.

    1. Main window layout — the menu bar sits at the top; the monitor tile row, the
       Identify/toggle row and the Settings gear are all fully visible and NOT clipped or
       overlapped. The window is taller by roughly the menu bar's height and nothing overflows
       past the bottom edge.
    2. Repeat check 1 at 125% and 150% Windows display scaling (this app has a documented
       history of scale-dependent layout regressions) — no overlap at any scale.
    3. `Help` > `About` opens the dialog centered on the main window. It shows "Rig Toggle"
       and a version reading exactly `2.2.1` — three components, not `2.2` and not `2.2.1.0`.
    4. Click `Check for Updates` in the About dialog. It behaves exactly like the tray item:
       either an update prompt appears, or a tray balloon reports you are already on the
       latest version naming `2.2.1`, or a warning balloon reports the failure reason.
    5. Press `Esc`, then reopen and click `Close` — both dismiss the dialog. Reopen a third
       time to confirm it can be opened repeatedly with no error and no leaked window.
    6. ADDITIVE CHECK (the one regression that would make this change unacceptable): the tray
       icon's `Check for Updates` item still works, and Settings' own `Check for Updates`
       button still works. Both must behave exactly as they did before.
    7. Theming — with Windows in dark mode (or the app's theme override set to Dark), confirm
       the menu bar and the About dialog are legible: no dark-on-dark or white-on-white text
       in the `Help` bar, the open `About` dropdown, the two dialog buttons, or the app
       name/version labels. Repeat in light mode.
    8. Live theme flip — with the main window open, flip Windows between light and dark. The
       menu bar recolors along with the rest of the window. NOTE: the dropdown's separator/
       arrow glyphs keeping a stale color across a live flip is a pre-existing, accepted
       WinForms limitation (dotnet/winforms#12027, already documented for the tray menu) — not
       a failure of this change.
    9. `--tray` startup path — launch with the `--tray` argument, then restore the window from
       the tray icon. Check 1's layout must be correct on that first paint too, since that
       path never runs `Form.Load`.

    Report per-check pass/fail. For any failure, include what you saw (a screenshot is ideal
    for layout/theming issues) — do not report a check as passed if it was not actually run.
  </how-to-verify>
  <resume-signal>Type "approved" if checks 1-9 pass, or list the failing check numbers with what you observed.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| user → MainForm menu bar | New local UI entry point; no untrusted data crosses it |
| AboutForm → MainForm.PerformManualUpdateCheck → GitHub Releases | Pre-existing network boundary, reached through an unmodified entry point |

No new package-manager installs occur in this plan (no npm/pip/cargo/NuGet additions), so no
package-legitimacy gate applies.

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-Q260829-01 | Information Disclosure | `AboutForm` version label | low | accept | The running version is already published as a public GitHub release tag and already shown in the existing up-to-date balloon; the About dialog exposes nothing new. No build paths, machine names or config values are rendered. |
| T-Q260829-02 | Tampering | About dialog `Check for Updates` → download/apply chain | medium | mitigate | The button invokes the existing `MainForm.PerformManualUpdateCheck` unchanged — Task 2 forbids any re-implementation — so the established `UpdateChecksum` verification and `UpdateRollbackChecker` failed-apply recovery cover this third entry point identically to the other two. Verified by the `reusesManualCheck` / `new AboutForm` gate plus the build gate. |
| T-Q260829-03 | Denial of Service | Concurrent update checks from a third trigger | medium | mitigate | `PerformManualUpdateCheckAsync`'s `_updateCheckInProgress` `Interlocked.CompareExchange` guard already rejects a concurrent call; routing the new button through that same method (rather than through `UpdateOrchestrator` directly) is what makes the guard cover it. A second click while a check is in flight silently no-ops instead of racing on the `FileShare.None`-locked staging path. |
| T-Q260829-04 | Denial of Service | Modal `AboutForm` nested message loop | low | mitigate | Shown with the same `Visible ? ShowDialog(this) : ShowDialog()` owner convention as `ShowUpdatePromptDialog`, inside `using var`, wrapped in try/catch — a dialog that cannot be shown is swallowed and traced, never propagated into the toggle flow. |
| T-Q260829-05 | Denial of Service | New `MenuStrip` shifting `LayoutDashboard` geometry | medium | mitigate | The offset is derived from `Math.Max(menuStrip.Height, menuStrip.PreferredSize.Height)` so it is correct even before a layout pass has run under `--tray` hidden startup; `LayoutDashboard` is already fully wrapped in try/catch so a layout failure cannot crash the toggle flow. Rig checks 1, 2 and 9 confirm no control is rendered unreachable (a menu bar overlapping the toggle switch would be a genuine loss of the app's primary function). |
</threat_model>

<verification>
- `dotnet build RigToggle.sln --nologo` succeeds with 0 errors.
- `dotnet test src/RigToggle.Tests/RigToggle.Tests.csproj --nologo` passes with at least 228
  tests and 0 failures (223 baseline plus the new `FormatDisplayVersion` cases).
- `git diff --name-only` lists exactly the seven files in `files_modified` and no others —
  in particular `SettingsForm.cs`, `SettingsForm.Designer.cs` and `Program.cs` are untouched.
- The running-version string has exactly one implementation:
  `grep -rn 'Math.Max(_runningVersion.Build' src/` returns nothing outside
  `FormatDisplayVersion`.
- Task 3's rig checkpoint is approved by the operator.
</verification>

<success_criteria>
1. `Help > About` on the main window opens a modal dialog naming the app, showing the running
   version as three components (`2.2.1`), with a working `Check for Updates` button and a
   `Close` button.
2. That button reaches the identical code path as the tray item and the Settings button —
   `MainForm.PerformManualUpdateCheck` — with no duplicated update logic anywhere.
3. The tray `Check for Updates` item and the Settings `Check for Updates` button both still
   work exactly as before; nothing about them was removed, moved or altered.
4. The menu bar and the About dialog render legibly in both light and dark themes, using the
   existing `ThemeApplier` helpers and routed through the single `ApplyDashboardTheming` call
   site.
5. No control on the main window is overlapped or clipped by the new menu bar at 100%, 125%
   or 150% scaling, on both the normal and the `--tray` startup path.
</success_criteria>

<output>
Create `.planning/quick/260829-fnt-add-a-menustrip-help-about-item-to-mainf/260829-fnt-SUMMARY.md` when done.
</output>
