# Monitor-Detach Spike — Run Instructions

This is a throwaway console tool that answers one question: **can the rig PC's primary (DisplayPort) monitor be truly removed from Windows' active display list — not just powered off?** Follow these steps on the rig PC, in order, from an **ordinary (non-elevated)** terminal. Do not run any of this "as administrator" — see Step 4 for why that matters.

You said only VS Code is confirmed installed on the rig PC — the .NET SDK is a **separate** install from VS Code. Step 0 covers that.

---

## Step 0 — Confirm/install the .NET SDK

Check whether the .NET 10 SDK is already present:

```powershell
dotnet --list-sdks
```

If you see a line starting with `10.0.` (e.g. `10.0.302`), you already have it — skip to Step 1.

If the command fails (`dotnet` not found) or no `10.0.x` line appears, install it:

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e
```

If `winget` isn't available either, download and run the installer manually from:
`https://dotnet.microsoft.com/download/dotnet/10.0`

After installing, close and reopen your terminal, then re-run `dotnet --list-sdks` to confirm a `10.0.x` line now appears before continuing.

---

## Step 1 — Get the spike project onto the rig PC

The spike's two source files already exist in this repo:
- `spike/MonitorDetachSpike/MonitorDetachSpike.csproj`
- `spike/MonitorDetachSpike/Program.cs`

Copy the `spike/MonitorDetachSpike` folder onto the rig PC (e.g. via git clone/pull of this repo, a USB drive, or copy/paste of the two files' contents into a new folder named `MonitorDetachSpike`).

**Alternative — scaffold from scratch** (useful if copying files is inconvenient, e.g. you'd rather type the source in by hand):

```powershell
dotnet new console -n MonitorDetachSpike -f net10.0-windows
cd MonitorDetachSpike
dotnet add package WindowsDisplayAPI --version 1.3.0.13
```

Then paste the repo's `Program.cs` contents over the generated `Program.cs`, and make sure the generated `.csproj` has `<UseWindowsForms>true</UseWindowsForms>` added inside the `<PropertyGroup>` (needed to unlock `Screen.AllScreens` as a second, independent verification source — it does not turn this into a GUI app).

---

## Step 2 — Restore & build

From inside the `MonitorDetachSpike` folder:

```powershell
dotnet build
```

The first build restores the `WindowsDisplayAPI` NuGet package, which requires internet access. Subsequent builds are offline.

---

## Step 3 — Run each mode, in order

**3a. List the active displays:**

```powershell
dotnet run -- --list
```

Read the printed list. Find the row whose `OutputTechnology` shows DisplayPort and whose `IsGDIPrimary=True` — that row's `[index]` is the monitor to disable (per this project's D-04/D-05 decisions: the rig's primary monitor is the AMD/DisplayPort-connected one).

**3b. Disable that monitor:**

```powershell
dotnet run -- --disable <index>
```

Substitute `<index>` with the number from `--list`. This detaches the monitor's display path, runs an immediate verification, waits ~20 seconds and verifies again (to catch delayed hotplug re-detection), then prompts you to press Enter to restore.

**3c. Check current state at any time:**

```powershell
dotnet run -- --verify
```

Prints the current active-path count (WindowsDisplayAPI) and `Screen.AllScreens.Length` (the independent second source).

---

## Step 4 — How to read the output (PASS vs FAIL)

**PASS** = the tool prints `PASS` on **both** the immediate check and the ~20-second delayed re-check. This means the monitor is gone from WindowsDisplayAPI's re-query **and** from `Screen.AllScreens`'s count, on both checks. Also glance at Windows Display Settings — a true PASS should show only the remaining monitor(s) listed there, and the taskbar/desktop icons should reflow onto the remaining screen.

**FAIL** = either check still detects the monitor, OR the monitor merely goes black/blank while both counts stay the same (the display is DPMS-powered-off, not actually removed from Windows' topology — this is the exact failure mode this spike exists to catch).

`--disable` restores the original topology automatically when you press Enter at its final prompt. If it does NOT restore correctly, you can re-run `dotnet run -- --verify` to check current state, or simply reboot — this tool does not persist any topology changes across a reboot.

### Troubleshooting: immediate PASS but delayed (~20s) re-check FAILs

If the monitor comes back after the ~20-second wait (the immediate check said PASS but the delayed check says FAIL), this points to DisplayPort hotplug re-detection silently re-adding the path. Try closing or stopping the AMD Software (Adrenalin) background service/tray app, then re-run `dotnet run -- --disable <index>` and see if the delayed re-check now stays PASS. If stopping Adrenalin changes the result, that indicates AMD's software has its own topology-interference layer distinct from the driver itself (see 01-RESEARCH.md Open Question 2) — note this in your results either way.

**IMPORTANT — do NOT run this tool "as administrator."** It must stay non-elevated for the whole test to be valid: if the primary approach happens to need elevation, that's an important finding, but only if you tested it non-elevated in the first place. Elevating the spike tool itself would contaminate that conclusion.

---

## Step 5 — What to capture and report back

1. Run `winver` and note the exact Windows build string shown.
2. Fill in `spike/RESULTS-TEMPLATE.md` with everything from your test run (before/after counts from both sources, the immediate and delayed PASS/FAIL lines, restore result, and whether any UAC/administrator prompt appeared).
3. If the primary (non-elevated) approach FAILs, see `spike/FALLBACK.md` for the separate, manually-invoked admin `pnputil` escalation path — do NOT try to make the spike tool itself elevated.

Report the filled-in `RESULTS-TEMPLATE.md` back — that is the actual go/no-go signal for this phase, not just running the commands above.
