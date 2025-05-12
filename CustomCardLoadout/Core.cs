using MelonLoader;

[assembly: MelonInfo(typeof(CustomCardLoadout.Core), "CustomCardLoadout", "2.0.1-beta", "joeyexists", null)]
[assembly: MelonGame("Little Flag Software, LLC", "Neon White")]

namespace CustomCardLoadout
{
    public class Core : MelonMod
    {
        internal static Game GameInstance { get; private set; }
        internal static new HarmonyLib.Harmony HarmonyInstance { get; private set; }
        private static bool isModEnabled = false;

        public override void OnLateInitializeMelon()
        {
            GameInstance = Singleton<Game>.Instance;
            HarmonyInstance = new HarmonyLib.Harmony("com.joeyexists.CustomCardLoadout");

            Settings.Register(this);
            Settings.removeDiscardLocksEntry.Value = false;

            GameInstance.OnInitializationComplete += () =>
            {
                if (Settings.modEnabledEntry.Value)
                    EnableMod(this);
            };
        }

        public static class Settings
        {
            public static MelonPreferences_Category category;

            public static MelonPreferences_Entry<bool> modEnabledEntry;
            public static MelonPreferences_Entry<CardOptions> firstCardSlotEntry;
            public static MelonPreferences_Entry<CardOptions> secondCardSlotEntry;
            public static MelonPreferences_Entry<int> firstCardSlotCountEntry;
            public static MelonPreferences_Entry<int> secondCardSlotCountEntry;
            public static MelonPreferences_Entry<bool> infiniteAmmoEntry;
            public static MelonPreferences_Entry<bool> unlimitedDiscardsEntry;
            public static MelonPreferences_Entry<bool> removeDiscardLocksEntry;
            public enum CardOptions
            {
                None,
                Purify,
                Elevate,
                Godspeed,
                Stomp,
                Fireball,
                Dominion,
                BookOfLife,
                MiracleKatana
            }
            public static void Register(Core modInstance)
            {
                category = MelonPreferences.CreateCategory("Custom Card Loadout");

                modEnabledEntry = category.CreateEntry("Enabled", false,
                    description: "Enables the mod.\n\nTriggers anti-cheat. To reset it, return to the hub.");

                firstCardSlotEntry = category.CreateEntry("Card Slot 1", CardOptions.Dominion);
                secondCardSlotEntry = category.CreateEntry("Card Slot 2", CardOptions.None);

                firstCardSlotCountEntry = category.CreateEntry("Slot 1 Card Count", 1, 
                    validator: new MelonLoader.Preferences.ValueRange<int>(1, 3)); 
                secondCardSlotCountEntry = category.CreateEntry("Slot 2 Card Count", 1,
                    validator: new MelonLoader.Preferences.ValueRange<int>(1, 3));

                infiniteAmmoEntry = category.CreateEntry("Infinite Ammo", false);
                unlimitedDiscardsEntry = category.CreateEntry("Unlimited Discards", false);

                removeDiscardLocksEntry = category.CreateEntry("Remove Discard Locks", false,
                    description: "Allows you to discard in Yellow's sidequests.\n\nNote: Once enabled, this cannot be turned off without restarting the game. Anti-cheat will also remain active.");

                modEnabledEntry.OnEntryValueChanged.Subscribe((_, enable) =>
                    ToggleMod(modInstance, enable));

                firstCardSlotEntry.OnEntryValueChanged.Subscribe((_, selectedCard) =>
                {
                    if (isModEnabled)
                        LoadoutManager.UpdateCardSlot(ref LoadoutManager.firstCardSlot, selectedCard, firstCardSlotCountEntry.Value);
                });

                secondCardSlotEntry.OnEntryValueChanged.Subscribe((_, selectedCard) =>
                {
                    if (isModEnabled)
                        LoadoutManager.UpdateCardSlot(ref LoadoutManager.secondCardSlot, selectedCard, secondCardSlotCountEntry.Value);
                });

                firstCardSlotCountEntry.OnEntryValueChanged.Subscribe((_, count) =>
                {
                    if (isModEnabled && LoadoutManager.firstCardSlot.cardData != null)
                        LoadoutManager.firstCardSlot.cardCount = count;
                });

                secondCardSlotCountEntry.OnEntryValueChanged.Subscribe((_, count) =>
                {
                    if (isModEnabled && LoadoutManager.secondCardSlot.cardData != null)
                        LoadoutManager.secondCardSlot.cardCount = count;
                });

                infiniteAmmoEntry.OnEntryValueChanged.Subscribe((_, enable) =>
                {
                    if (isModEnabled)
                    {
                        var patch = HarmonyPatcher.Patches.PlayerUICardHUD_UpdateHUD_Patch;
                        HarmonyPatcher.TogglePatch(patch, enable);
                        LoadoutManager.ToggleInfiniteAmmo(enable);
                    }
                });

                unlimitedDiscardsEntry.OnEntryValueChanged.Subscribe((_, enable) =>
                {
                    if (isModEnabled)
                    {
                        var patch = HarmonyPatcher.Patches.MechController_UseDiscardAbility_Patch;
                        HarmonyPatcher.TogglePatch(patch, enable);
                        if (enable)
                            LoadoutManager.SetOverrideCardColors(LoadoutManager.UnlimitedDiscardsCardColor);
                        else
                            LoadoutManager.RestoreCardColors();
                    }
                });

                removeDiscardLocksEntry.OnEntryValueChanged.Subscribe((_, enable) =>
                {
                    if (isModEnabled && enable)
                        LoadoutManager.EnableRemoveDiscardLocks();
                });
            }
        }

        public void RegisterAntiCheat()
        {
            NeonLite.Modules.Anticheat.Register(MelonAssembly);
        }

        public void UnregisterAntiCheat()
        {
            NeonLite.Modules.Anticheat.Unregister(MelonAssembly);
        }

        private static void ToggleMod(Core modInstance, bool enable)
        {
            if (enable == isModEnabled)
                return;
            if (enable)
                EnableMod(modInstance);
            else 
                DisableMod();
        }

        private static void EnableMod(Core modInstance)
        {
            modInstance.RegisterAntiCheat();

            GameInstance.OnLevelLoadComplete += LoadoutManager.OnLevelLoadComplete;

            LoadoutManager.UpdateCardSlot(ref LoadoutManager.firstCardSlot, Settings.firstCardSlotEntry.Value, Settings.firstCardSlotCountEntry.Value);
            LoadoutManager.UpdateCardSlot(ref LoadoutManager.secondCardSlot, Settings.secondCardSlotEntry.Value, Settings.secondCardSlotCountEntry.Value);

            bool isUnlimitedDiscardsEnabled = Settings.unlimitedDiscardsEntry.Value;
            bool isInfiniteAmmoEnabled = Settings.infiniteAmmoEntry.Value;

            if (isUnlimitedDiscardsEnabled)
            {
                HarmonyPatcher.TogglePatch(HarmonyPatcher.Patches.MechController_UseDiscardAbility_Patch, true);
                LoadoutManager.SetOverrideCardColors(LoadoutManager.UnlimitedDiscardsCardColor);
            }

            HarmonyPatcher.TogglePatch(HarmonyPatcher.Patches.PlayerUICardHUD_UpdateHUD_Patch, isInfiniteAmmoEnabled);
            LoadoutManager.ToggleInfiniteAmmo(isInfiniteAmmoEnabled);

            if (Settings.removeDiscardLocksEntry.Value)
                LoadoutManager.removeDiscardLocks = true;

            isModEnabled = true;
        }

        private static void DisableMod()
        {
            GameInstance.OnLevelLoadComplete -= LoadoutManager.OnLevelLoadComplete;
            HarmonyPatcher.TogglePatch(HarmonyPatcher.Patches.MechController_UseDiscardAbility_Patch, false);
            HarmonyPatcher.TogglePatch(HarmonyPatcher.Patches.PlayerUICardHUD_UpdateHUD_Patch, false);
            LoadoutManager.RestoreCardColors();

            isModEnabled = false;
        }

        public override void OnSceneWasLoaded(int buildindex, string sceneName)
        {
            if (sceneName.Equals("HUB_HEAVEN")
                && isModEnabled == false
                && HarmonyPatcher.IsPatched_MechController_UseDiscardAbility == false
                && !LoadoutManager.removeDiscardLocks
                && NeonLite.Modules.Anticheat.Active)
            {
                UnregisterAntiCheat();
            }
        }
    }
}