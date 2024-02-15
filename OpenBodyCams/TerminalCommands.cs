using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace OpenBodyCams
{
    public static class TerminalCommands
    {
        public static TerminalNode ViewMonitorNode;
        public static TerminalNode ViewBodyCamNode;
        public static TerminalKeyword BodyCamKeyword;

        public static RawImage PiPImage;

        private static readonly List<TerminalKeyword> newTerminalKeywords = [];
        private static readonly List<TerminalKeyword> modifiedTerminalKeywords = [];

        public static void Initialize()
        {
            if (ShipObjects.TerminalScript == null || ShipObjects.MainBodyCam == null)
                return;

            Object.Destroy(PiPImage?.gameObject);
            PiPImage = null;

            ShipObjects.MainBodyCam.OnRenderTextureCreated -= SetRenderTexture;

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
            PiPImage.color = blanked ? Color.black : Color.white;
        }

        static void InitializeCommands()
        {
            RemoveAddedKeywords();
            ViewBodyCamNode = null;
            BodyCamKeyword = null;

            if (Plugin.TerminalPiPBodyCamEnabled.Value)
            {
                ViewBodyCamNode = ScriptableObject.CreateInstance<TerminalNode>();
                ViewBodyCamNode.name = "ViewBodyCam";
                ViewBodyCamNode.displayText = "Toggling body cam\n\n";
                ViewBodyCamNode.clearPreviousText = true;

                BodyCamKeyword = FindOrCreateKeyword("BodyCam", "bodycam", verb: false);
                var viewKeyword = FindOrCreateKeyword("View", "view", verb: true,
                    [
                        new CompatibleNoun() {
                            noun = BodyCamKeyword,
                            result = ViewBodyCamNode,
                        },
                    ]);

                ViewMonitorNode = viewKeyword.FindCompatibleNoun("monitor").result;
            }

            AddNewlyCreatedCommands();
        }

        private static bool TerminalIsDisplayingMap()
        {
            return ShipObjects.TerminalScript.displayingPersistentImage == ViewMonitorNode.displayTexture;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.RunTerminalEvents))]
        static bool RunTerminalEventsPrefix(TerminalNode node)
        {
            if (node == ViewBodyCamNode)
            {
                if (PiPImage.gameObject.activeSelf)
                {
                    PiPImage.gameObject.SetActive(false);
                    return false;
                }

                if (!TerminalIsDisplayingMap())
                    ShipObjects.TerminalScript.LoadTerminalImage(ViewMonitorNode);
                if (TerminalIsDisplayingMap())
                    PiPImage.gameObject.SetActive(true);
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), nameof(Terminal.LoadNewNode))]
        static void LoadNewNodePostfix(TerminalNode node)
        {
            if (PiPImage == null || node != ViewMonitorNode)
                return;

            if (!TerminalIsDisplayingMap())
                PiPImage.gameObject.SetActive(false);
        }

        static void RemoveAddedKeywords()
        {
            // Remove references to new keywords.
            foreach (var keyword in modifiedTerminalKeywords)
            {
                if (keyword.compatibleNouns != null)
                    keyword.compatibleNouns = [.. keyword.compatibleNouns.Where(compatible => !newTerminalKeywords.Contains(compatible.noun))];
            }
            modifiedTerminalKeywords.Clear();

            // Remove new keywords.
            foreach (var keyword in newTerminalKeywords)
                Object.Destroy(keyword);

            var nodes = ShipObjects.TerminalScript.terminalNodes;
            nodes.allKeywords = [.. nodes.allKeywords.Where(keyword => !newTerminalKeywords.Contains(keyword))];

            newTerminalKeywords.Clear();
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
            else
            {
                keyword.compatibleNouns = [.. keyword.compatibleNouns, .. compatibleNouns];
                Plugin.Instance.Logger.LogInfo($"  Keyword existed, appended nouns.");
            }

            modifiedTerminalKeywords.Add(keyword);
            return keyword;
        }

        static void AddNewlyCreatedCommands()
        {
            var nodes = ShipObjects.TerminalScript.terminalNodes;
            nodes.allKeywords = [.. nodes.allKeywords, .. newTerminalKeywords];
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
}
