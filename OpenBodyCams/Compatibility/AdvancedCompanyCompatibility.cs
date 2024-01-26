using System.Linq;
using System.Runtime.CompilerServices;

using AdvancedCompany.Lib;
using GameNetcodeStuff;
using UnityEngine;

namespace OpenBodyCams.Compatibility
{
    public static class AdvancedCompanyCompatibility
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static GameObject[] CollectCosmetics(PlayerControllerB player)
        {
            return Cosmetics.GetSpawnedCosmetics(player).SelectMany(cosmetic => cosmetic.GetComponentsInChildren<Transform>()).Select(transform => transform.gameObject).ToArray();
        }
    }
}
