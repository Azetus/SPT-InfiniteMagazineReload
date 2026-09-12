using Comfort.Common;
using EFT;
using EFT.Builds;
using EFT.InventoryLogic;
using UnityEngine;

namespace InfiniteMagazineReload.Services;

internal static class MagazineRefillService
{
    public static void OnItemAdded(ItemController owner, AddItemEventArgs args)
    {
        try
        {
            if (!Plugin.Enabled.Value)
            {
                return;
            }

            if (args.Status != CommandStatus.Succeed)
            {
                return;
            }

            if (owner is not InventoryController controller)
            {
                return;
            }

            var mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
            if (mainPlayer == null || controller != mainPlayer.InventoryController)
            {
                return;
            }

            // Raid only: the hideout main player is a HideoutPlayer (a LocalPlayer subclass).
            if (mainPlayer is HideoutPlayer)
            {
                return;
            }

            // Only the magazine itself (smallest scope).
            if (args.Item is not Magazine magazine)
            {
                return;
            }

            var tagName = magazine.Tag?.Name;

            // Destination must be backpack / rig / pockets.
            if (!IsInUsableStorage(args.To, controller.Inventory))
            {
                return;
            }

            // Empty magazine with a tag.
            if (magazine.Count != 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(tagName))
            {
                return;
            }

            // Strict (case-sensitive) preset name match.
            var session = Singleton<ClientApplication<IEftSession>>.Instance?.Session;
            var preset = session?.MagBuildsStorage?.Presets
                .FirstOrDefault(p => string.Equals(p.Name, tagName, StringComparison.Ordinal));

            if (preset == null)
            {
                return;
            }

            Fill(magazine, preset, controller);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[InfiniteMagazineReload] OnItemAdded failed: {ex}");
        }
    }

    private static bool IsInUsableStorage(ItemAddress to, Inventory inventory)
    {
        if (to == null || inventory?.Equipment == null)
        {
            return false;
        }

        foreach (var slotName in new[] { EquipmentSlot.Backpack, EquipmentSlot.TacticalVest, EquipmentSlot.Pockets })
        {
            var container = inventory.Equipment.GetSlot(slotName)?.ContainedItem;
            if (container != null && to.IsChildOf(container))
            {
                return true;
            }
        }

        return false;
    }

    private static void Fill(Magazine magazine, MagPreset preset, InventoryController controller)
    {
        var factory = Singleton<ItemFactory>.Instance;
        if (factory == null)
        {
            return;
        }

        // Bottom -> Loop -> Top, already clamped to the magazine capacity and merged.
        var sequence = preset.GetMagTemplateReference(magazine);
        if (sequence == null || sequence.Count == 0)
        {
            return;
        }

        foreach (var entry in sequence)
        {
            if (entry == null)
            {
                continue;
            }

            var remaining = magazine.Cartridges.MaxCount - magazine.Cartridges.Count;
            if (remaining <= 0)
            {
                break;
            }

            var probe = factory.CreateItem(factory.NextId, entry.TemplateId, null) as Ammo;
            if (probe == null || !magazine.CheckCompatibility(probe))
            {
                continue;
            }

            var count = Mathf.Min(entry.Count, remaining);
            while (count > 0)
            {
                var take = Mathf.Min(count, probe.StackMaxSize);
                var ammo = factory.CreateItem(factory.NextId, entry.TemplateId, null) as Ammo;
                if (ammo == null)
                {
                    break;
                }

                ammo.StackObjectsCount = take;

                var result = ItemManipulator.Add(ammo, magazine.Cartridges.CreateItemAddress(), controller);
                if (!result.Succeeded)
                {
                    Plugin.Log?.LogWarning($"[InfiniteMagazineReload] Failed to add ammo: {result.Error}");
                    break;
                }

                result.Value.RaiseEvents(controller, CommandStatus.Begin);
                result.Value.RaiseEvents(controller, CommandStatus.Succeed);

                count -= take;
            }
        }

        Plugin.Log?.LogInfo($"[InfiniteMagazineReload] Refilled magazine {magazine.Id} from preset '{preset.Name}'.");
    }
}
