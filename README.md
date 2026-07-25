# Rig Toggle

A Windows GUI utility that switches between "normal desktop mode" and "Moza rig mode"
with one click. Toggling to rig mode disables the primary monitor at the OS level,
switches the default audio output to the rig speakers, and launches/focuses the Moza
Companion app. Toggling back restores the exact previous monitor/audio state and
minimizes the Moza Companion app.

## Build a standalone .exe

Publish is self-contained, single-file, and untrimmed (win-x64 only). From the repo root:

```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -p:PublishProfile=win-x64
```

If the RID is ever not picked up for any reason (e.g. an older/mismatched SDK), fall back
to the explicit-flag form:

```bash
dotnet publish src/RigToggle.App/RigToggle.App.csproj -c Release -r win-x64 --self-contained true -p:PublishProfile=win-x64
```

The output single-file exe lands in `src/RigToggle.App/bin/publish/win-x64/` and requires
no separate .NET runtime install to run (PACKAGING-01).

Note: the build is intentionally untrimmed (`PublishTrimmed=false`) — trimming can strip
the COM interop (audio default-device switching) and P/Invoke (display CCD topology)
marshalling this app depends on — and it targets Windows x64 only.
