using System;
using System.Linq;
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

            // Skip magazines installed in a slot (e.g. inserted into a weapon). The game forbids
            // loading an installed magazine, and doing so during a reload corrupts the weapon state.
            if (magazine.CurrentAddress?.Container is Slot)
            {
                return;
            }

            // Must be within the player's equipment tree (any equipped slot / container, no exclusions).
            if (!controller.Inventory.IsEquipmentAddress(args.To, out _))
            {
                return;
            }

            // Skip already-full magazines; empty and partially filled are both refilled.
            if (magazine.Count >= magazine.Cartridges.MaxCount)
            {
                return;
            }

            if (string.IsNullOrEmpty(tagName))
            {
                return;
            }

            // Strict (case-sensitive) preset name match, restricted to presets compatible with this
            // magazine, so same-named presets for other magazine types / calibers are ignored.
            var session = Singleton<ClientApplication<IEftSession>>.Instance?.Session;
            var preset = session?.MagBuildsStorage?.Presets
                .FirstOrDefault(p =>
                    string.Equals(p.Name, tagName, StringComparison.Ordinal) && IsPresetCompatible(p, magazine));

            if (preset == null)
            {
                return;
            }

            // Overwrite semantics: discard current rounds, then load the full preset.
            if (magazine.Count > 0)
            {
                ClearCartridges(magazine, controller);
            }

            Fill(magazine, preset, controller);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[InfiniteMagazineReload] OnItemAdded failed: {ex}");
        }
    }

    private static bool IsPresetCompatible(MagPreset preset, Magazine magazine)
    {
        var factory = Singleton<ItemFactory>.Instance;
        if (factory == null || magazine?.Cartridges == null)
        {
            return false;
        }

        var hasAmmo = false;
        foreach (var entry in preset.Items)
        {
            if (entry == null)
            {
                continue;
            }

            var probe = factory.CreateItem(factory.NextId, entry.TemplateId, null) as Ammo;
            if (probe == null || !magazine.Cartridges.Filters.CheckItemFilter(probe))
            {
                return false;
            }

            hasAmmo = true;
        }

        return hasAmmo;
    }

    private static void ClearCartridges(Magazine magazine, InventoryController controller)
    {
        while (magazine.Cartridges.Count > 0)
        {
            var round = magazine.Cartridges.Last;
            if (round == null)
            {
                break;
            }

            var result = ItemManipulator.Remove(round, controller);
            if (!result.Succeeded)
            {
                Plugin.Log?.LogWarning($"[InfiniteMagazineReload] Failed to remove round: {result.Error}");
                break;
            }

            result.Value.RaiseEvents(controller, CommandStatus.Begin);
            result.Value.RaiseEvents(controller, CommandStatus.Succeed);
        }
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
