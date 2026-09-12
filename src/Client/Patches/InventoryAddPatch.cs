using EFT.InventoryLogic;
using HarmonyLib;
using InfiniteMagazineReload.Services;

namespace InfiniteMagazineReload.Patches;

[HarmonyPatch(typeof(ItemController), nameof(ItemController.RaiseAddEvent))]
internal static class InventoryAddPatch
{
    [HarmonyPostfix]
    private static void Postfix(ItemController __instance, AddItemEventArgs args)
    {
        MagazineRefillService.OnItemAdded(__instance, args);
    }
}
