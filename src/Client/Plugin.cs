using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace InfiniteMagazineReload;

[BepInPlugin("com.spt.infinitemagazinereload", "InfiniteMagazineReload", "1.0.0")]
public class Plugin : BaseUnityPlugin
{
    public static ConfigEntry<bool> Enabled = null!;
    public static ManualLogSource Log = null!;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Enable automatic refill of tagged empty magazines in raid (backpack / rig / pockets)."
        );

        new Harmony("com.spt.infinitemagazinereload").PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo("[InfiniteMagazineReload] loaded.");
    }
}
