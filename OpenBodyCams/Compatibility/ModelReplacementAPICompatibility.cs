using System.Linq;
using System.Runtime.CompilerServices;

using GameNetcodeStuff;
using ModelReplacement;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class ModelReplacementAPICompatibility
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            if (player.GetComponentInChildren<BodyReplacementBase>() is BodyReplacementBase bodyReplacement)
            {
                Plugin.Instance.Logger.LogInfo($"Getting {player.playerUsername}'s model replacement");
                return bodyReplacement.replacementModel.GetComponentsInChildren<Transform>().Select(cosmeticObject => cosmeticObject.gameObject).ToArray();
            }

            return new GameObject[0];
        }
    }
}
