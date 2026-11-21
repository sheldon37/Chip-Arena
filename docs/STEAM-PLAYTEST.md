# Windows Steam Playtest

For a fresh build and Steam package in one run, use `Build-Windows.cmd --steam-playtest`.

After the Windows builder produces its verified native update feed, run the following from the Unity conversion project. This stages the same Unity player that the standalone updater delivers, including its build identity and compiled assets. The starter launcher and updater feed stay outside the depot.

```bat
Package-Steam-Playtest.cmd
```

The command prints a fresh revision-specific folder under `Builds/SteamPlaytest` and the Windows executable for Steamworks launch options. Set that executable relative to the install root, with Windows as its platform and no launch arguments. Retail builds produced with the current bootstrap use `Chip Arena.exe` at the depot root.

Before the first upload, replace `PLAYTEST_APP_ID` and `WINDOWS_DEPOT_ID` in the staged `app_build.vdf` with the values for **Chip Arena Playtest**. Alternatively, pass `--app-id NUMBER --depot-id NUMBER` when packaging. To repeat a dry run of the same revision, specify a fresh `--output DIRECTORY`. The staging script does not need Steam credentials.

With SteamCMD authenticated under a build account with access to that app, run `run_app_build "ABSOLUTE_PATH_TO_app_build.vdf"`. The script uploads the staged content without setting any branch live. Confirm the Build ID on the Playtest app's SteamPipe Builds page and select the intended Playtest branch there. Check a Steam install on Windows before admitting the first testers.

For each subsequent build repeat the packaging step on its completed feed. Steam manages updates for the staged copy. The standalone build continues using the existing Update channel.

For a tester report, open **Settings → Connection report → Copy diagnostics**, or press **Ctrl+Shift+F9** while playing or on a loading screen. The short report includes the most recent local loading and disconnect events for the running process. Paste it into your test group's bug thread.

[Valve SteamPipe documentation](https://partner.steamgames.com/doc/sdk/uploading)
