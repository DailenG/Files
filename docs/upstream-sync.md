# Upstream Synchronization Guide

This repository (`DailenG/Files`) is maintained as an upstream-friendly fork of [files-community/Files](https://github.com/files-community/Files).

## Remote Configuration

To check existing remotes:
```powershell
git remote -v
```

Expected configuration:
```text
origin    https://github.com/DailenG/Files.git (fetch & push)
upstream  https://github.com/files-community/Files.git (fetch & push)
```

If `upstream` is not yet configured:
```powershell
git remote add upstream https://github.com/files-community/Files.git
```

## Upstream Baseline

- **Initial Baseline Commit**: `c6c05c6cc84137ed554c28b2b6521c3a9ae4b049`
- **Upstream Tag**: Post-`v4.2.27` (`main` branch)

## Routine Synchronization Process

Never push directly into the default `main` branch without a pull request or verification.

1. **Fetch Upstream Changes**:
   ```powershell
   git fetch upstream main --tags
   ```

2. **Create a Sync Branch**:
   ```powershell
   git checkout -b sync/upstream-$(Get-Date -Format 'yyyyMMdd') origin/main
   ```

3. **Merge or Rebase**:
   ```powershell
   git merge upstream/main
   ```

4. **Resolve Conflicts**:
   - Conflicts should be minimal if fork-specific code is isolated in separate service files and clean interfaces.
   - Run verification builds:
     ```powershell
     & 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe' -restore src/Files.App/Files.App.csproj -p:Configuration=Debug -p:Platform=x64 -v:quiet -clp:ErrorsOnly
     ```

5. **Open a Pull Request**:
   - Push the sync branch to `origin`.
   - Open a PR in `DailenG/Files` titled `Maintenance: Sync with upstream main (YYYY-MM-DD)`.
   - Verify CI and merge into `main`.
