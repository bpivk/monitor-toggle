# Monitor-Detach Spike — Admin Fallback (pnputil)

**This is a SEPARATE, MANUALLY-INVOKED escalation path.** Only try this if the primary non-elevated tool (`spike/RUN-INSTRUCTIONS.md`) reports **FAIL** — i.e. the monitor blanks but stays in the enumeration (WindowsDisplayAPI `--list` count and/or `Screen.AllScreens.Length` do not drop).

**Run this from a SEPARATELY-OPENED elevated (administrator) PowerShell or cmd window — never by running the spike `.exe` itself as administrator.** Elevating the spike tool would invalidate the elevation conclusion from your primary test and could hide a UIPI (User Interface Privilege Isolation) problem that would later affect the real app's cross-process window-focus feature on the Moza Companion app (Phase 3). Keep the two terminals — the ordinary one running the spike, and the elevated one running these commands — strictly separate.

---

## Step 1 — Find the monitor's device Instance ID

This read-only step can be run from an ordinary (non-elevated) terminal:

```powershell
Get-PnpDevice -Class Monitor | Format-Table FriendlyName, InstanceId, Status
```

Identify the row matching the target monitor (the DisplayPort/primary one you identified via `dotnet run -- --list` in the primary test) and note its `InstanceId`.

## Step 2 — Disable the device node (requires elevation)

Open a **separate, elevated** PowerShell or cmd window (right-click → "Run as administrator"), then run:

```powershell
pnputil /disable-device "<InstanceId from Step 1>"
```

## Step 3 — Re-verify from the non-elevated spike terminal

Switch back to your ordinary (non-elevated) terminal where the spike tool lives, and run:

```powershell
dotnet run -- --verify
```

Confirm the active-path count dropped in **both** enumeration sources (WindowsDisplayAPI count and `Screen.AllScreens.Length`), the same PASS bar used in the primary test.

## Step 4 — Re-enable (also requires elevation)

Back in the elevated terminal:

```powershell
pnputil /enable-device "<InstanceId from Step 1>"
```

---

## Notes

- `devcon.exe` is **deprecated** — Microsoft no longer distributes it as a standalone download and has stated it was originally a code sample, not a tool to be relied upon. Do **not** hunt down a `devcon.exe` binary from a third-party mirror. `pnputil` ships built into every Windows install and is Microsoft's own recommended replacement.
- If this fallback is what actually works (the primary non-elevated `ApplyPathInfos` approach failed, but `pnputil` succeeds), record that under **"GO (with fallback)"** in `spike/RESULTS-TEMPLATE.md`. That outcome makes Phase 4's elevated-helper-process isolation **mandatory** rather than optional (Assumption A1) — the production app must still stay non-elevated overall (D-08), with only this one operation isolated into a small separate elevated helper process, never the whole app running elevated.
