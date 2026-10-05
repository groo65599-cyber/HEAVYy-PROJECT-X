# Building HPX PIRATE

Complete, copy-pasteable instructions for building the application and the installer.

---

## 0. Prerequisites

| Tool | Version | Notes |
| --- | --- | --- |
| **.NET SDK** | 8.0.x | https://dotnet.microsoft.com/download/dotnet/8.0 |
| **Inno Setup** | 6.x | https://jrsoftware.org/isdl.php — only for the `.exe` installer |
| **Windows** | 10 1809+ / 11, x64 | required to *run*; not required to *compile* |

Verify:

```powershell
dotnet --version      # 8.0.x
```

> **Cross-compiling on Linux/macOS:** the project sets
> `<EnableWindowsTargeting>true</EnableWindowsTargeting>`, so `dotnet build`/`publish`
> work on non-Windows hosts. You just cannot *run* the WPF app there.

---

## 1. Clone

```bash
git clone https://github.com/groo65599-cyber/HEAVYy-PROJECT-X.git
cd HEAVYy-PROJECT-X
```

---

## 2. Restore

```bash
dotnet restore HPX-PIRATE.sln
```

---

## 3. Build (Debug)

```bash
dotnet build src/HPXPirate/HPXPirate.csproj -c Debug
```

## 4. Build (Release)

```bash
dotnet build src/HPXPirate/HPXPirate.csproj -c Release
```

Expected result: **0 warnings, 0 errors**.

---

## 5. Run

```bash
dotnet run --project src/HPXPirate/HPXPirate.csproj -c Release
```

On Windows this opens the WPF window. The app manifest requests **administrator**
rights (for restore points and system tuning).

---

## 6. Publish

### 6a. Framework-dependent (small, needs .NET 8 Desktop Runtime on the target)

```bash
dotnet publish src/HPXPirate/HPXPirate.csproj -c Release -r win-x64 \
    --self-contained false -o publish
```

Produces `publish/HPXPirate.exe` + dependency DLLs.

### 6b. Self-contained single file (larger, no runtime needed on the target)

```bash
dotnet publish src/HPXPirate/HPXPirate.csproj -c Release -r win-x64 \
    --self-contained true -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true -o publish-selfcontained
```

> Note: the ETW FPS monitor ships native helper DLLs
> (`amd64/KernelTraceControl.dll`, `msdia140.dll`). Keep them alongside the exe, or use
> `IncludeNativeLibrariesForSelfExtract=true` as shown.

---

## 7. Build the installer (`HPX-PIRATE-Setup.exe`)

### Option A — helper script (recommended)

From an **elevated** PowerShell at the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```

This runs the publish step and then compiles the installer. Result:

```
dist\HPX-PIRATE-Setup.exe
```

### Option B — manual

```powershell
# 1) publish (see step 6a)
dotnet publish src\HPXPirate\HPXPirate.csproj -c Release -r win-x64 --self-contained false -o publish

# 2) compile the Inno Setup script
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" installer\HPX-PIRATE.iss
```

### Optional: bundle the .NET runtime into the installer

Place the runtime installer at `installer\redist\windowsdesktop-runtime-8.0.10-win-x64.exe`
(download from https://dotnet.microsoft.com/download/dotnet/8.0 → *Desktop Runtime* →
*Windows x64*). The installer will detect a missing runtime and install it silently.

---

## 8. Run the logic tests

```bash
dotnet run --project tests/HPXPirate.LogicTests -c Release
```

Expected: `56 passed, 0 failed`. Isolate test data with:

```bash
XDG_CONFIG_HOME=/tmp/hpx_test_config dotnet run --project tests/HPXPirate.LogicTests -c Release
```

---

## 9. Clean

```bash
dotnet clean HPX-PIRATE.sln
rm -rf publish publish-selfcontained dist
```

---

## 10. Troubleshooting the build

| Problem | Cause / fix |
| --- | --- |
| `NETSDK1100: ... EnableWindowsTargeting` | Add `<EnableWindowsTargeting>true</EnableWindowsTargeting>` (already set) or build on Windows. |
| `CS0104: ambiguous reference` (WPF vs WinForms) | Caused by `UseWindowsForms` implicit usings; the csproj removes `System.Windows.Forms`/`System.Drawing` global usings. |
| `CS0103: 'Path'/'File' does not exist` | Add `<Using Include="System.IO" />` (already set). |
| `iscc.exe` not found | Install Inno Setup 6 or pass its full path. |
| Missing `Microsoft.WindowsDesktop.App` at runtime | Install the **.NET 8 Desktop Runtime (x64)**. |
| Build succeeds on Linux but app won't run | WPF only runs on Windows — expected. |

---

## CI (optional)

A minimal GitHub Actions workflow to build on `windows-latest`:

```yaml
name: build
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet build src/HPXPirate/HPXPirate.csproj -c Release
      - run: dotnet run --project tests/HPXPirate.LogicTests -c Release
      - run: dotnet publish src/HPXPirate/HPXPirate.csproj -c Release -r win-x64 --self-contained false -o publish
```
