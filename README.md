# RLSHub (WPF)

Desktop companion for **RLS Career Overhaul** (BeamNG mod). WPF version — single .exe, no store dependency.

- **Dashboard** — Launch BeamNG (Vulkan/DirectX), optional console, auto-run CarSwap bridge
- **CarSwap** — Marketplace listings, run/update bridge (sync with game)
- **Updates** — Installed mod version, check for updates
- **Settings** — Gameplay toggles (Map Dev, No Police, No Parked)
- **About** — Community and GitHub links

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 (or use the self-contained publish — no install needed)

## Build

```bash
dotnet build RLSHub.Wpf.csproj -c Release
```

## Publish (single .exe for sharing)

```bash
dotnet publish -c Release -p:PublishProfile=win-x64-singlefile -p:Platform=x64
```

Output: `bin\Release\net8.0-windows\win-x64\publish\RLSHub.Wpf.exe`

## Upload to GitHub Releases (optional)

After publishing, you can push the .exe to the repo’s Releases (only when you want to ship):

1. Install [GitHub CLI](https://cli.github.com/) and log in: `gh auth login`
2. From the repo root, run:
   ```powershell
   .\upload-release.ps1
   ```
3. The script asks **“Upload this build to GitHub Releases? (y/n)”** — answer `n` for minor builds, `y` when you’re ready to publish.
4. If you choose `y`, enter a tag (e.g. `v1.0.0`) and optional release notes; the script creates the release and uploads the exe.

## Bridge (CarSwap)

Place `bridge.exe` in `Assets\Bridge\`. The app copies it to `%LocalAppData%\RLSHub\Bridge\` on "Update bridge" and runs it from there (no Python required).

## Related

- [RLS Career Overhaul](https://github.com/RLS-Modding/rls_career_overhaul) — the mod
- RLSHub WinUI 3 — store/installer version (separate repo)
