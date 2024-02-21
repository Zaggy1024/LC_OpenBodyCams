# OpenBodyCams
An open-source implementation of a body/head camera that is displayed on the bottom right monitor in the ship, with the goal of appearing almost identical to the player's actual perspective while providing good performance.

The camera view will display only the first person hands and disable the third person model as well as MoreCompany cosmetics to prevent obstructed vision.

# Features
- Selectable camera perspective between the head and body.
- Camera view is designed to render identically for the local player and other players in the game.
- MoreCompany and AdvancedCompany cosmetics support, see the [Compatibility](#compatibility) section.
- All enemies, terrain, etc. that is visible to the player will be visible in the camera view.
- The same green flash animation used when switching targets on the radar is displayed by the camera view.
- Performance:
  - The camera is attached to the player model in the engine rather than copying the transform from it.
  - Camera setup logic is done ahead of time based on game events whenever possible.
- A _opt-in_ `view bodycam` command in the terminal displays the body cam when viewing the radar map. See [Configuration/Terminal](#terminal)

# Compatibility
[MoreCompany](https://thunderstore.io/c/lethal-company/p/notnotnotswipez/MoreCompany/) cosmetics, [AdvancedCompany](https://thunderstore.io/c/lethal-company/p/PotatoePet/AdvancedCompany/) cosmetics and equipment, and third-person model replacements by [ModelReplacementAPI](https://thunderstore.io/c/lethal-company/p/BunyaPineTree/ModelReplacementAPI/) and [LethalVRM](https://thunderstore.io/c/lethal-company/p/Ooseykins/LethalVRM/) are supported. They will be hidden when viewing other players in the body cam, and your cosmetics/models will be visible on the camera when you are viewing another player looking at you.

[GeneralImprovements](https://thunderstore.io/c/lethal-company/p/ShaosilGaming/GeneralImprovements/)'s extended monitors set is supported through a config option to select the monitor number to use for the body cam. The body cam will override any selection in the GeneralImprovements config.

[TwoRadarMaps](https://thunderstore.io/c/lethal-company/p/Zaggy1024/TwoRadarMaps/) will use a separate body cam for the picture-in-picture view in the terminal when `EnablePiPBodyCam` is enabled.

# Configuration

## Camera options
- `Mode`: The attachment point of the camera. The head and the body are selectable.
- `HorizontalResolution`: The horizontal resolution of the rendered view.
- `FieldOfView`: The vertical FOV of the body cam's view.
- `RenderDistance`: The far clip plane of the camera.
- `Framerate`: The number of frames to render per second. The default setting renders at the game's framerate and has the least impact on performance.
- `NightVisionBrightness`: A multiplier for the brightness and range of the night vision light. A value of 1 matches the vision of the player being viewed.
- `MonitorEmissiveColor`: The color to emit from the screen displaying the body cam. Represented as comma-separated numbers to avoid losing precision by using a 32-bit color.
- `MonitorTextureFiltering`: Changes the texture filtering applied to the screen for the body cam. Point will result in sharp edges on the pixels, while bilinear and trilinear should both smooth out colors between them.
- `RadarBoosterPanRPM`: This controls the number of turns that the camera should make each minute. If set to 0, the camera will be fixed in the direction that the player placing the radar booster was facing.
- `UseTargetTransitionAnimation`: If enabled, the body cam will display a green flash animation when changing targets to mirror the behavior of the radar map.
- `DisableCameraWhileTargetIsOnShip`: This will cause the screen to turn off while the camera's target is onboard the ship. This can be used to avoid the load of rendering large numbers of items on the ship in long runs.
- `EnableCamera`: When this is enabled, the screen will be powered off. This can be changed in-game with LethalConfig or any similar mod.

## Terminal
- `EnablePiPBodyCam`: Off by default, this adds a `view bodycam` command to the terminal that displays the body cam in one corner of the radar map. When the radar map is hidden, the body cam will be hidden as well.
- `PiPPosition`: Selects the corner of the radar map that the body cam view in the terminal should reside in.
- `PiPWidth`: Sets the horizontal size of the body cam view in the terminal. This does not affect the render resolution of the camera.

## Miscellaneous
- `DisableInternalShipCamera`: Disables the camera at the front of the ship facing towards the center. This may improve performance inside the ship slightly.
- `FixDroppedItemRotation`: Defaulted to `true`, this fixes a desync of items' rotations when dropping them. See [Notes/Item rotations](#item-rotations).

## Debug
See [Debugging](#debugging).

# Notes

## Framerate limits
As mentioned above, using no framerate limit results in the best performance. Forcing the camera to render at certain intervals outside of the render pipeline seems to cause a lot of overhead, so setting the framerate limit to anything above 30fps may cause a severe dip in the game's framerate.

## Item rotations
An optional fix is included for items' rotations being desynced between the player dropping them and all other clients, which is caused by an ignored rotation parameter in the function handling dropped items. This is included to allow the radar boosters to face in a consistent direction for all clients in a game. The patch is designed to fail gracefully and allow the mod to still run, in case any other mods apply the same fix, but if problems arise, it can be disabled with the `FixDroppedItemRotation` config option.

## Debugging
***When providing logs for issues you encounter, PLEASE make sure to enable Unity logging!***
- Set BepInEx's `UnityLogListening` option in the `[Logging]` section to `true`.
- Set BepInEx's `LogLevels` option in the `[Logging.Disk]` section to `All`.
- Disable [DisableUnityLogs](https://thunderstore.io/c/lethal-company/p/Mhz/DisableUnityLogs/) if installed.

***Otherwise, there will be no error messages printed in the logs at all, and I cannot narrow down the cause of the problem.***

The logs can be found in the BepInEx folder within the mod manager's profile folder (`%appdata%\r2modmanPlus-local\LethalCompany\profiles\[profile name]` for r2modman), or inside the game's Steam install folder. Please ensure that the modification date indicates that the file is the most recent launch of the game.

### Screen freezes/error spam
If error spam or screen freezes are encountered, please reproduce the issue with `ReferencedObjectDestructionDetectionEnabled` enabled in the `[Debug]` section of the config, then provide the game logs in a [new issue on GitHub](https://github.com/Zaggy1024/LC_OpenBodyCams/issues/new)  (see [Debugging](#debugging) to find the `.log` file).  These will provide valuable information to narrow down the cause of the problem. After the issue occurs, `BruteForcePreventFreezes` can be used to resume normal gameplay.
- `BruteForcePreventFreezes`: Prevents the error spam by checking every frame whether any cosmetics on viewed players have been destroyed and updating the list if so. This can be used as a stopgap measure to prevent screen freezes if a mod conflict is unavoidable.
- `ReferencedObjectDestructionDetectionEnabled`: Prints a message and stack trace whenever an object is destroyed while a body cam is referencing it. This should point directly to any problematic mods causing issues.

### "Collected cosmetics" spam
If messages are spammed excessively in the console/logs saying `Collected [x] cosmetics objects for [name]`, then the `PrintCosmeticsDebugInfo` can be enabled to provide information on what is causing the collection of the cosmetics. Enable this option while the issue is occurring and provide the logs in a GitHub issue (see [Debugging](#debugging) to find the `.log` file).
- `PrintCosmeticsDebugInfo`: Prints extra information about the cosmetics being collected for each player, as well as the code that is causing the cosmetics to be collected. This is useful information to provide when reporting that you are seeing a  message getting spammed in the logs.

# Developers
If you wish to create a body cam separate from the default one included with this mod, you can simply add OpenBodyCams as a dependency and use `OpenBodyCams.API.BodyCam.CreateBodyCam()`:

```cs
var doorScreen = GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/SingleScreen");
BodyCam.CreateBodyCam(doorScreen, doorScreen.GetComponent<MeshRenderer>(), 1, StartOfRound.Instance.mapScreen);
```

The body cam component will be attached to the provided `GameObject`, and use the provided `Renderer` to check whether the display it is on is being rendered.

The `displayMaterialIndex` argument indicates which of the shared materials on the renderer should be replaced by the body cam's render texture. The texture that is in that index originally will be stored by the body cam, and when `SetScreenPowered(false)` is called, it will replace the body cam's output on the display. The body cam's output can then be brought back with a `SetScreenPowered(true)` call.

The `ManualCameraRenderer` argument must be a map renderer where its `cam` field is the same reference as its `mapCamera` field. However, the argument may be null, in which case the body cam's target may be controlled directly.
