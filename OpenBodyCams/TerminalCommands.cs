using HarmonyLib;
using OpenBodyCams.API;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OpenBodyCams;

public static class TerminalCommands
{
    private static TerminalNode ViewMonitorNode;

    private static TerminalNode ViewBodyCamNode;
    private static TerminalKeyword BodyCamKeyword;
    private static TerminalNode BodyCamFailedNode;
    private static TerminalNode BodyCamLockedNode;

    private static RawImage PiPImage;

    private static readonly List<TerminalKeyword> newTerminalKeywords = [];
    private static readonly Dictionary<TerminalKeyword, List<TerminalKeyword>> addedCompatibleNouns = [];

    public static void Initialize()
    {
        if (ShipObjects.TerminalScript == null || ShipObjects.MainBodyCam == null)
            return;

        if (PiPImage != null)
            Object.Destroy(PiPImage.gameObject);
        PiPImage = null;

        ShipObjects.MainBodyCam.OnRenderTextureCreated -= SetRenderTexture;
        ShipObjects.MainBodyCam.OnBlankedSet -= SetBodyCamBlanked;
        BodyCam.OnBodyCamReceiverBecameDisabled -= DisablePiPImage;

        if (Plugin.TerminalPiPBodyCamEnabled.Value)
        {
            var pipImageObject = new GameObject("PictureInPictureImage");
            pipImageObject.transform.SetParent(ShipObjects.TerminalScript.terminalImageMask.transform, worldPositionStays: false);
            PiPImage = pipImageObject.AddComponent<RawImage>();

            pipImageObject.AddComponent<TerminalBodyCamVisibilityTracker>().BodyCamToActivate = ShipObjects.MainBodyCam;

            var bigImageTransform = ShipObjects.TerminalScript.terminalImage.rectTransform;

            var origin = new Vector2(0, 0);
            var inward = new Vector2(1, 1);
            var pipPosition = (int)Plugin.TerminalPiPPosition.Value;
            (origin.x, inward.x) = (pipPosition & 1) switch
            {
                0 => (bigImageTransform.offsetMin.x, 1),
                _ => (bigImageTransform.offsetMax.x, -1),
            };
            (origin.y, inward.y) = (pipPosition & 2) switch
            {
                0 => (bigImageTransform.offsetMin.y, 1),
                _ => (bigImageTransform.offsetMax.y, -1),
            };

            var corner = origin + (new Vector2(1f, 3f / 4f) * inward * Plugin.TerminalPiPWidth.Value);
            PiPImage.rectTransform.offsetMin = Vector2.Min(origin, corner);
            PiPImage.rectTransform.offsetMax = Vector2.Max(origin, corner);

            if (ShipObjects.MainBodyCam.Camera != null)
                PiPImage.texture = ShipObjects.MainBodyCam.Camera.targetTexture;

            ShipObjects.MainBodyCam.OnRenderTextureCreated += SetRenderTexture;
            ShipObjects.MainBodyCam.OnBlankedSet += SetBodyCamBlanked;
            BodyCam.OnBodyCamReceiverBecameDisabled += DisablePiPImage;

            pipImageObject.SetActive(false);
        }

        InitializeCommands();
    }

    public static void SetRenderTexture(RenderTexture texture)
    {
        PiPImage.texture = texture;
    }

    public static void SetBodyCamBlanked(bool blanked)
    {
        PiPImage.enabled = !blanked;
    }

    private static void DisablePiPImage()
    {
        PiPImage.gameObject.SetActive(false);
    }

    private static void EnablePiPImage()
    {
        PiPImage.gameObject.SetActive(true);
    }

    static void InitializeCommands()
    {
        RemoveAddedKeywords();
        ViewBodyCamNode = null;
        BodyCamKeyword = null;

        var viewKeyword = FindKeyword("view", verb: true);
        ViewMonitorNode = viewKeyword?.FindCompatibleNoun("monitor")?.result;

        if (Plugin.TerminalPiPBodyCamEnabled.Value)
        {
            if (ViewMonitorNode == null)
            {
                Plugin.Instance.Logger.LogWarning("'view monitor' command does not exist, terminal PiP body cam view will be disabled.");
                return;
            }

            ViewBodyCamNode = ScriptableObject.CreateInstance<TerminalNode>();
            ViewBodyCamNode.name = "ViewBodyCam";
            ViewBodyCamNode.displayText = "Toggling picture-in-picture body cam.\n\n";
            ViewBodyCamNode.clearPreviousText = true;

            BodyCamKeyword = FindOrCreateKeyword("BodyCam", "bodycam", verb: false);

            AddCompatibleNoun(viewKeyword, BodyCamKeyword, ViewBodyCamNode);

            viewKeyword.compatibleNouns = [
                .. viewKeyword.compatibleNouns ?? [],
                new CompatibleNoun()
                {
                    noun = BodyCamKeyword,
                    result = ViewBodyCamNode,
                }
            ];

            BodyCamFailedNode = ScriptableObject.CreateInstance<TerminalNode>();
            BodyCamFailedNode.name = "BodyCamFailed";
            BodyCamFailedNode.displayText = "Map view is currently disabled.\n\n";
            BodyCamFailedNode.clearPreviousText = true;

            BodyCamLockedNode = ScriptableObject.CreateInstance<TerminalNode>();
            BodyCamLockedNode.name = "BodyCamFailed";
            BodyCamLockedNode.displayText = "Please place a body cams receiver antenna on the ship.\n\n";
            BodyCamLockedNode.clearPreviousText = true;
        }

        AddNewlyCreatedCommands();
    }

    private static bool TerminalIsDisplayingMap()
    {
        if (ViewMonitorNode == null)
            return false;

        return ShipObjects.TerminalScript.displayingPersistentImage == ViewMonitorNode.displayTexture;
    }

    internal static bool TerminalIsDisplayingBodyCam()
    {
        if (PiPImage == null)
            return false;

        return PiPImage.gameObject.activeSelf;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.LoadNewNode))]
    static void LoadNewNodePrefix(ref TerminalNode node)
    {
        if (node == null)
            return;

        if (node == ViewBodyCamNode)
        {
            if (TerminalIsDisplayingBodyCam())
            {
                DisablePiPImage();
                return;
            }

            if (!BodyCam.BodyCamsAreAvailable)
            {
                node = BodyCamLockedNode;
                return;
            }

            if (!TerminalIsDisplayingMap())
                ShipObjects.TerminalScript.LoadTerminalImage(ViewMonitorNode);
            if (!TerminalIsDisplayingMap())
            {
                node = BodyCamFailedNode;
                return;
            }

            EnablePiPImage();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.LoadNewNode))]
    static void LoadNewNodePostfix(TerminalNode node)
    {
        if (PiPImage == null)
            return;
        if (node != ViewMonitorNode)
            return;

        if (!TerminalIsDisplayingMap())
            DisablePiPImage();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SwitchMapMonitorPurpose))]
    private static void SwitchMapMonitorPurposePostfix()
    {
        if (PiPImage == null)
            return;
        if (!TerminalIsDisplayingMap())
            DisablePiPImage();
    }

    static TerminalKeyword FindKeyword(string word, bool verb)
    {
        return ShipObjects.TerminalScript.terminalNodes.allKeywords.FirstOrDefault(keyword => keyword.word == word && keyword.isVerb == verb);
    }

    static CompatibleNoun FindCompatibleNoun(this TerminalKeyword keyword, string noun)
    {
        return keyword.compatibleNouns.FirstOrDefault(compatible => compatible.noun.word == noun);
    }

    static TerminalKeyword FindOrCreateKeyword(string name, string word, bool verb, CompatibleNoun[] compatibleNouns = null)
    {
        Plugin.Instance.Logger.LogInfo($"Creating terminal {(verb ? "verb" : "noun")} '{word}' ({name}).");
        TerminalKeyword keyword = FindKeyword(word, verb);
        if (keyword == null)
        {
            keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            keyword.name = name;
            keyword.isVerb = verb;
            keyword.word = word;
            keyword.compatibleNouns = compatibleNouns;
            newTerminalKeywords.Add(keyword);
            Plugin.Instance.Logger.LogInfo($"  Keyword was not found, created a new one.");
        }
        else if (compatibleNouns != null)
        {
            foreach (var compatibleNoun in compatibleNouns)
                AddCompatibleNoun(keyword, compatibleNoun);
            Plugin.Instance.Logger.LogInfo($"  Keyword existed, appended nouns.");
        }

        return keyword;
    }

    private static void AddCompatibleNoun(TerminalKeyword keyword, CompatibleNoun compatibleNoun)
    {
        if (!addedCompatibleNouns.TryGetValue(keyword, out var compatibleNounsList))
        {
            compatibleNounsList = new List<TerminalKeyword>();
            addedCompatibleNouns[keyword] = compatibleNounsList;
        }

        keyword.compatibleNouns = [.. keyword.compatibleNouns ?? [], compatibleNoun];
        compatibleNounsList.Add(compatibleNoun.noun);
    }

    private static void AddCompatibleNoun(TerminalKeyword keyword, TerminalKeyword compatibleKeyword, TerminalNode result)
    {
        AddCompatibleNoun(keyword, new CompatibleNoun()
        {
            noun = compatibleKeyword,
            result = result,
        });
    }

    static void AddNewlyCreatedCommands()
    {
        var nodes = ShipObjects.TerminalScript.terminalNodes;
        nodes.allKeywords = [.. nodes.allKeywords, .. newTerminalKeywords];
    }

    private static void RemoveAddedKeywords()
    {
        // Remove references to new keywords.
        foreach (var (keyword, addedNouns) in addedCompatibleNouns)
        {
            if (keyword.compatibleNouns != null)
                keyword.compatibleNouns = [.. keyword.compatibleNouns.Where(compatible => !addedNouns.Contains(compatible.noun))];
        }
        addedCompatibleNouns.Clear();

        // Remove new keywords.
        var nodes = ShipObjects.TerminalScript.terminalNodes;
        nodes.allKeywords = [.. nodes.allKeywords.Where(keyword => !newTerminalKeywords.Contains(keyword))];

        foreach (var keyword in newTerminalKeywords)
            Object.Destroy(keyword);

        newTerminalKeywords.Clear();
    }
}

public enum PiPPosition : uint
{
    BottomLeft = 0b00,
    BottomRight = 0b01,
    TopLeft = 0b10,
    TopRight = 0b11,
}

public class TerminalBodyCamVisibilityTracker : MonoBehaviour
{
    public BodyCamComponent BodyCamToActivate;

    void OnEnable()
    {
        if (BodyCamToActivate != null)
            BodyCamToActivate.ForceEnableCamera = true;
    }

    void OnDisable()
    {
        if (BodyCamToActivate != null)
            BodyCamToActivate.ForceEnableCamera = false;
    }
}
