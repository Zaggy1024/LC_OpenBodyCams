using System.Runtime.CompilerServices;

using BepInEx.Bootstrap;
using LethalLib.Modules;
using UnityEngine;

using OpenBodyCams.Compatibility;
using OpenBodyCams.API;

namespace OpenBodyCams
{
    internal static class ShipUpgrades
    {
        internal static UnlockableItem BodyCamUnlockable;
        internal static bool BodyCamUnlockableIsPlaced = false;

        public static void Initialize()
        {
            if (!Chainloader.PluginInfos.ContainsKey(ModGUIDs.LethalLib))
            {
                Plugin.Instance.Logger.LogInfo("LethalLib is not present, body cam will be enabled by default.");
                return;
            }

            RegisterUnlockables();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RegisterUnlockables()
        {
            RegisterBodyCamShipUpgrade();
        }

        private static void RegisterBodyCamShipUpgrade()
        {
            if (Plugin.Assets == null)
                return;
            if (!Plugin.ShipUpgradeEnabled.Value)
                return;

            var bodyCamUnlockablePrefab = Plugin.Assets.LoadAsset<GameObject>("Assets/OpenBodyCams/BodyCamAntenna.prefab");

            if (bodyCamUnlockablePrefab == null)
            {
                Plugin.Instance.Logger.LogInfo("Body cam antenna prop was not found within the asset bundle.");
                return;
            }

            bodyCamUnlockablePrefab.AddComponent<EnableMainBodyCam>();

            var bodyCamUnlockable = new UnlockableItem
            {
                unlockableName = "Bodycam",
                unlockableType = 1,
                prefabObject = bodyCamUnlockablePrefab,
                IsPlaceable = true,
                alwaysInStock = true,
            };

            var price = Plugin.ShipUpgradePrice.Value;
            Unlockables.RegisterUnlockable(bodyCamUnlockable, price, StoreType.ShipUpgrade);
            NetworkPrefabs.RegisterNetworkPrefab(bodyCamUnlockablePrefab);
            LethalLib.Modules.Utilities.FixMixerGroups(bodyCamUnlockablePrefab);

            BodyCamUnlockable = bodyCamUnlockable;
            Plugin.Instance.Logger.LogInfo($"Registered body cam unlockable for {price} credits.");
        }
    }

    internal class EnableMainBodyCam : MonoBehaviour
    {
        private void OnEnable()
        {
            ShipUpgrades.BodyCamUnlockableIsPlaced = true;
            BodyCam.BodyCamReceiverBecameEnabled();

            if (ShipObjects.MainBodyCam == null)
                return;
            ShipObjects.MainBodyCam.enabled = true;
        }

        private void OnDisable()
        {
            ShipUpgrades.BodyCamUnlockableIsPlaced = false;
            BodyCam.BodyCamReceiverBecameDisabled();

            if (ShipObjects.MainBodyCam == null)
                return;
            ShipObjects.MainBodyCam.enabled = false;
        }
    }
}
