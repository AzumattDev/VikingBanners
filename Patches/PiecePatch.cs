using System;
using HarmonyLib;

namespace VikingBanners.Patches
{
    [HarmonyPatch(typeof(Piece),nameof(Piece.Awake))]
    static class ShipAwakePatch
    {
        static void Postfix(Piece __instance)
        {
            //if (Utils.GetPrefabName(__instance.gameObject).Contains("piece_banner") || Utils.GetPrefabName(__instance.gameObject).Contains("piece_cloth_hanging_door"))
            if (Utils.GetPrefabName(__instance.gameObject).Contains("piece_banner") || Utils.GetPrefabName(__instance.gameObject).Contains("rae_ob_banner"))
            {
                if (!__instance.gameObject.GetComponent<VikingBannerURL>())
                    __instance.gameObject.AddComponent<VikingBannerURL>();
            }
        }
    }   
}