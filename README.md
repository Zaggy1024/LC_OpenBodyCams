# OpenBodyCams
An open-source implementation of a body/head camera that is displayed on the bottom right monitor in the ship, with the goal of appearing almost identical to the player's actual perspective while providing good performance.

The camera view will use only the first person hands and disable the third person model to prevent obstructed vision.

# Features
- Selectable camera perspective between the head and body.
- Camera view is designed to render identically for the local player as well other players in the game.
- All enemies, terrain, etc. that is visible to the player will be visible in the camera view.
- The same green flash animation used when switching targets on the radar is displayed by the camera view.
- Performance:
  - The camera is attached to the player model in the engine rather than copying the transform from it.
  - Camera setup logic is done ahead of time based on game events whenever possible.

# Customization
- Camera mode: Select the attachment point of the camera between the head and the body.
- Resolution: Specify the horizontal resolution of the rendered view.
- Render distance: Determines the far clip plane of the camera.
- Framerate: The number of frames to render per second. The default setting renders at the game's framerate and has the least impact on performance.
- Disable internal ship camera: A little extra in case it can help with performance, this disables the camera at the front of the ship facing towards the center.
