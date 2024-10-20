#nullable enable

namespace OpenBodyCams.API;

public class Commands
{
    public bool IsPiPBodyCamDisplayed => TerminalCommands.TerminalIsDisplayingBodyCam();
}
