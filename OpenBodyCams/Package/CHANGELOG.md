## Version 1.1.1
- Hopefully prevented an issue where in rare cases, masked players being viewed by the body cam would cause the screen to freeze. This was perhaps caused by other mods deleting renderers attached to the masked players after they were created.

## Version 1.1.0
- Refactored the mod to allow creation of multiple body cams through an API. This feature is not used in OpenBodyCams, but may be used in the future to provide a separate body cam for the terminal, or other use cases.
- When no valid target is selected (i.e. when players are still respawning), the screen will now display nothing, but it will remain illuminated to indicate that it is still powered.
- Fixed an issue where the screen would get frozen with the green transition visible when a radar booster is left behind on a planet.
- Note: Due to refactoring much of the code that reacts to the events in the game, I'm anticipating there may be regressions in the way the body cam tracks its target's status changes (death, masking, respawning). If the camera gets stuck displaying something unexpected, please report that (preferably through a GitHub issue) with steps to reproduce the problem.

## Version 1.0.24
- Added support for ModelReplacementAPI's third person model replacements.
- Added support for LethalVRM's third person model replacements.

## Version 1.0.23
- Fixed the external camera staying frozen when looking at the screen by the door controls.

## Version 1.0.22
- Added handling of AdvancedCompany equipment to the compatibility feature, preventing equipped items from obstructing the body cam.

## Version 1.0.21
- Fixed all cameras in the ship staying enabled when looking away from the front of the ship with DisableInternalShipCamera off. The internal camera is disabled if its screen is not visible to any camera but its own, which occurs when it is not visible to the player.

## Version 1.0.20
- Fixed parsing of the emissive color option in locales that use `,` as their decimal separator.

## Version 1.0.19
- Added an option to adjust the emissive color of the body cam screen.
- Added an option for the panning speed for cameras when attached to a radar booster. When set to 0, the camera will be stationary and face in the direction the player was looking when placing the radar booster.
- Implemented an optional fix for dropped item rotation being desynced between the player dropping the item and other players. This will affect all items in the game. Without this fix, radar boosters will not face in the direction that they were dropped by the player that had been holding them.
- Allowed the camera to be disabled when the player being viewed enters the ship to avoid rendering the items on board twice.

## Version 1.0.18
- Fixed the radar map not disabling correctly when it was not visible, or while the player is outside the ship.

## Version 1.0.17
- Fixed body cams being unable to display on the GeneralImprovements large monitors with UseBetterMonitors enabled.

## Version 1.0.16
- Fixed a reduction in performance that began in version 1.0.14 with the fix for the DisableInternalShipCamera bug.

## Version 1.0.15
- Updated GeneralImprovements compatibility to work with version 1.1.1. This updated compatibility will not work with any previous versions of GeneralImprovements.

## Version 1.0.14
- Fixed a bug that caused error spam when MoreCompany compatibility was being used.
- Forced the radar map to keep rendering when interacting with the terminal while the DisableInternalShipCamera option is enabled.

## Version 1.0.13
- Supported the GeneralImprovements UseBetterMonitors option with a config option to select the monitor number to display the body cam on.

## Version 1.0.12
- Stopped placing the body cam on the monitor by the door.
- Reduced the emissivity of the bodycam monitor.

## Version 1.0.11
- Added an option to turn the body cam screen off, which can be used in-game if performance issues are encountered.

## Version 1.0.10
- Added support for AdvancedCompany cosmetics. They should now be hidden on the body cam when viewing other players.

## Version 1.0.9
- Made the bodycam monitor turn off when the radar map is powered down by the button or a lightning strike.

## Version 1.0.8
- Added an option to adjust the brightness of the night vision light. This can be used to counteract the green tint of the screen making it difficult to see when players are in dark areas.

## Version 1.0.7
- Fixed a compatibility issue with Immersive Visor which would cause the body cam to render to the screen instead of a texture when a player respawned.
- Prevented the radar map's night vision light from showing up on the body cam.

## Version 1.0.6
- Prevented MoreCompany cosmetics from appearing and obstructing the view when joining a game with the camera viewing someone with cosmetics equipped.
- Made the body cam attach to dead bodies on clients other than the one of the player that died.

## Version 1.0.5
- Fixed an compatibility issue with MirrorDecor that caused the first person arms to be invisible in the body cam.

## Version 1.0.4
- Fixed an issue where the local player's body would be invisible in the body cam with LethalEmotesAPI or MirrorDecor installed.

## Version 1.0.3
- Fixed an error that would occur when a third person emote was started by the LethalEmotesAPI mod.
- Added a config option to disable MoreCompany cosmetic support that can be used if compatibility issues arise.

## Version 1.0.2
- Fixed error spam that would occur if the camera was attached to an enemy or dead body being destroyed after a round ends.

## Version 1.0.1
- Fixed an error that would occur when MoreCompany is not installed.

## Version 1.0.0
- Initial release.
