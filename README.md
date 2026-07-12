# Aerial Plane Attack Cool XXX3

A Unity 6 flight-simulation project where AI-controlled jets learn to fly using fixed-topology neuroevolution, NEAT, PPO, or SAC.

## Requirements

- Windows 10 or 11
- [Unity Hub](https://unity.com/download) with **Unity 6000.3.10f1** installed
- About 4 GB of additional free space if you want to use the PPO or SAC modes
- Git, if you clone the repository instead of downloading its ZIP archive

The evolutionary modes (`FixedNeuroEvo` and `NEAT`) need no Python installation or other setup. The reinforcement-learning modes (`PPO_MLAgents` and `SAC_MLAgents`) use a bundled Python environment downloaded separately because it is too large for GitHub source control.

## Download and open the project

### Option 1: clone with Git

```powershell
git clone https://github.com/Tal-Gordon/Aerial-Plane-Attack-Cool-XXX3.git
cd Aerial-Plane-Attack-Cool-XXX3
```

### Option 2: download a ZIP

1. Open the repository on GitHub.
2. Select **Code > Download ZIP**.
3. Extract the archive to a normal writable folder. Avoid opening the project directly from inside the ZIP.
4. Open PowerShell in the extracted project folder.

Then open Unity Hub, select **Add > Add project from disk**, choose the project folder (the folder containing `Assets`, `Packages`, and `ProjectSettings`), and open it with Unity **6000.3.10f1**. Unity installs the required project packages automatically on the first import; this can take several minutes.

Once the import finishes, open a scene from `Assets/Scenes` and press **Play**. FixedNeuroEvo and NEAT are ready immediately.

## Enable PPO and SAC training

From PowerShell in the project root, run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\download-env.ps1
```

The first command changes script policy only for the current PowerShell process. The download script:

- downloads the tag-pinned `env-v1` environment archive (about 1.7 GB);
- extracts about 3.3 GB into `Assets/StreamingAssets/mlagents-env`;
- skips the download when `python.exe` is already present; and
- requires a Windows `tar.exe` build with Zstandard support (included with Windows 11 and recent Windows 10 versions).

After the script reports success, open or return to Unity and choose PPO or SAC in the game UI. The project finds the bundled interpreter automatically and launches the ML-Agents trainer; you do not need to install Python or Conda.

If extraction reports that the archive format is unsupported, update Windows or install a `tar` implementation with Zstandard support, then run the script again. Interrupted downloads are kept in your temporary directory and resumed automatically when you rerun the script.

## Alternative RL setup for developers

If you already maintain a compatible ML-Agents Python environment, set `MLAGENTS_PYTHON` to its `python.exe`. Fully quit both the Unity Editor **and Unity Hub** before relaunching: the Editor inherits environment variables from the Hub process, so restarting only the Editor is insufficient.

The lookup order is:

1. `Assets/StreamingAssets/mlagents-env/python.exe`
2. the `MLAGENTS_PYTHON` environment variable
3. a Conda environment named `mlagents`

The checked-in `environment.yml` documents the pinned developer environment. `package.ps1` is for maintainers producing the downloadable bundle; ordinary users should run `download-env.ps1` instead.

## Standalone-build note

There are two distribution options.

### Thin build (recommended for sharing)

The thin build stays small and downloads the environment from the `env-v1` GitHub Release on its first training launch:

1. Ensure `Assets/StreamingAssets/mlagents-env` is absent, then build the Windows player normally.
2. Distribute the entire resulting build folder. Unity automatically includes the small `setup-training-env.ps1` installer in StreamingAssets.

The recipient simply double-clicks the game `.exe`. Before showing the menu, the player opens the setup window, downloads the ~1.7 GB release asset, and extracts ~3.3 GB into `<Game>_Data/StreamingAssets/mlagents-env`. It then automatically relaunches itself with ML-Agents port 5004. Setup is mandatory: if it fails, the player exits instead of continuing without RL. Interrupted downloads resume automatically, while subsequent launches skip setup because `python.exe` is already installed. `Launch (training).bat` remains available only as a manual fallback.

The recipient therefore still needs ~5 GB of free space during installation (the compressed download plus extracted environment), but the download is not part of Git or the build archive.

### Self-contained build

For an offline build, run `download-env.ps1` before building and leave the copied environment in the output. Unity automatically copies the extracted folder below into the build:

```text
Game.exe
Game_Data/
  StreamingAssets/
    mlagents-env/
      python.exe
      ...the rest of the bundled environment...
```

In either distribution, users do **not** install Python/Conda or manually copy the `config` directory. The trainer YAML is generated at runtime. Keep the build in a writable location because the trainer writes `config/`, `results/`, and save data while it runs. Unity also includes the scenes and project packages selected in Build Settings automatically.

RL training in a standalone player requires launching the executable with the same ML-Agents port used by the trainer:

```powershell
.\Game.exe --mlagents-port 5004
```

Without `--mlagents-port 5004`, the player does not create the trainer connection and the jets receive zero actions. This argument is not needed when running inside the Unity Editor.
