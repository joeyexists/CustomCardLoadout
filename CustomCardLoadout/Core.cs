using MelonLoader;
using HarmonyLib;
using System.Reflection;

[assembly: MelonInfo(typeof(CustomCardLoadout.Core), "CustomCardLoadout", "1.0.0-beta", "joeyexists", null)]
[assembly: MelonGame("Little Flag Software, LLC", "Neon White")]

namespace CustomCardLoadout
{
    public class Core : MelonMod
    {
        internal static Game GameInstance { get; private set; }
        internal static new HarmonyLib.Harmony HarmonyInstance { get; private set; }

        private static readonly MethodInfo OriginalUseDiscardAbilityMethod =
            AccessTools.Method(typeof(MechController), "UseDiscardAbility", [typeof(int)]);

        private static readonly MethodInfo UseDiscardAbilityPrefixMethod =
            typeof(Core).GetMethod(nameof(UseDiscardAbilityPrefixPatch));

        private static readonly HarmonyMethod UseDiscardAbilityPrefix =
            new(UseDiscardAbilityPrefixMethod);

        private static readonly MethodInfo OriginalUpdateCardHUDMethod =
            AccessTools.Method(typeof(PlayerUICardHUD), "UpdateHUD");

        private static readonly MethodInfo UpdateCardHUDPostfixMethod =
            typeof(Core).GetMethod(nameof(UpdateCardHUDPostfixPatch));

        private static readonly HarmonyMethod UpdateCardHUDPostfix =
            new(UpdateCardHUDPostfixMethod);

        private static bool modEnabled = false;
        private static bool infiniteAmmoAndDiscardsPatched = false;
        private static string startCard = null;
        private static UnityEngine.Color? originalStartCardColor = null;

        public enum Cards
        {
            Purify,
            Elevate,
            Godspeed,
            Stomp,
            Fireball,
            Dominion
        }

        public override void OnLateInitializeMelon()
        {
            GameInstance = Singleton<Game>.Instance;
            HarmonyInstance = new HarmonyLib.Harmony("com.joeyexists.CustomCardLoadout");

            Settings.Register(this);

            GameInstance.OnInitializationComplete += () =>
            {
                if (Settings.enabledEntry.Value)
                    EnableMod(this);
            };
        }

        public static string GetCardID(Cards card) => card switch
        {
            Cards.Purify => "MACHINEGUN",
            Cards.Elevate => "PISTOL",
            Cards.Godspeed => "RIFLE",
            Cards.Stomp => "UZI",
            Cards.Fireball => "SHOTGUN",
            Cards.Dominion => "ROCKETLAUNCHER",
            _ => string.Empty
        };

        public static class Settings
        {
            public static MelonPreferences_Category category;

            public static MelonPreferences_Entry<bool> enabledEntry;
            public static MelonPreferences_Entry<Cards> startCardEntry;
            public static MelonPreferences_Entry<bool> startCardInfiniteEntry;

            public static void Register(Core modInstance)
            {
                category = MelonPreferences.CreateCategory("Custom Card Loadout");

                enabledEntry = category.CreateEntry("Enabled", true,
                    description: "Use a custom loadout.\n\nTriggers anti-cheat. To reset it, return to the hub.");
                startCardEntry = category.CreateEntry("Card to start with", Cards.Dominion,
                    description: "Applies to every level");
                startCardInfiniteEntry = category.CreateEntry("Infinite ammo & discards", true,
                    description: "Unlimited ammo, card will not be used upon discarding.");

                enabledEntry.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
                {
                    if (newValue) EnableMod(modInstance);
                    else DisableMod();
                });

                startCardEntry.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
                {
                    startCard = GetCardID(newValue);
                    originalStartCardColor = null;
                    if (modEnabled && NeonLite.Modules.Anticheat.Active)
                    {
                        modInstance.UpdateGhostName();
                    }
                });

                startCardInfiniteEntry.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
                {
                    if (modEnabled)
                        ToggleInfiniteAmmoAndDiscardsPatch(newValue);
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

        public void UpdateGhostName()
        {
            NeonLite.Modules.Anticheat.SetGhostName(MelonAssembly, $"CustomCardLoadout_{startCard}");
        }

        private static void EnableMod(Core modInstance)
        {
            GameInstance.OnLevelLoadComplete += OnLevelLoadComplete;
            ToggleInfiniteAmmoAndDiscardsPatch(Settings.startCardInfiniteEntry.Value);
            startCard = GetCardID(Settings.startCardEntry.Value);
            modInstance.RegisterAntiCheat();
            modInstance.UpdateGhostName();
            modEnabled = true;
        }

        private static void DisableMod()
        {
            GameInstance.OnLevelLoadComplete -= OnLevelLoadComplete;
            UnpatchInfiniteAmmoAndDiscards();
            modEnabled = false;
        }

        private static void ToggleInfiniteAmmoAndDiscardsPatch(bool apply)
        {
            if (infiniteAmmoAndDiscardsPatched == apply)
                return;
            if (apply)
                DoPatchInfiniteAmmoAndDiscards();
            else
                UnpatchInfiniteAmmoAndDiscards();
        }

        private static void DoPatchInfiniteAmmoAndDiscards()
        {
            if (infiniteAmmoAndDiscardsPatched)
                return;

            HarmonyInstance.Patch(OriginalUseDiscardAbilityMethod, prefix: UseDiscardAbilityPrefix);
            HarmonyInstance.Patch(OriginalUpdateCardHUDMethod, postfix: UpdateCardHUDPostfix);
            infiniteAmmoAndDiscardsPatched = true;
        }

        private static void UnpatchInfiniteAmmoAndDiscards()
        {
            if (!infiniteAmmoAndDiscardsPatched)
                return;

            HarmonyInstance.Unpatch(OriginalUseDiscardAbilityMethod, UseDiscardAbilityPrefixMethod);
            HarmonyInstance.Unpatch(OriginalUpdateCardHUDMethod, UpdateCardHUDPostfixMethod);
            infiniteAmmoAndDiscardsPatched = false;
        }

        public override void OnSceneWasLoaded(int buildindex, string sceneName)
        {
            if (sceneName.Equals("HUB_HEAVEN")
                && modEnabled == false
                && infiniteAmmoAndDiscardsPatched == false
                && NeonLite.Modules.Anticheat.Active)
            {
                UnregisterAntiCheat();
            }
        }

        public static void OnLevelLoadComplete()
        {
            TryAddCard(startCard, infiniteAmmoAndDiscardsPatched);
        }

        private static void TryAddCard(string cardID, bool isInfinite = false)
        {
            if (string.IsNullOrEmpty(cardID) || !NeonLite.Modules.Anticheat.Active) 
                return;


            if (UnityEngine.Object.FindObjectOfType<MechController>() is not MechController mech)
            {
                MelonLogger.Warning("Failed to add card: MechController not found.");
                return;
            }

            if (GameInstance.GetGameData() is not GameData gameData)
            {
                MelonLogger.Warning("Failed to add card: GameData is null.");
                return;
            }

            if (gameData.GetCard(cardID) is not PlayerCardData card)
            {
                MelonLogger.Warning($"Failed to add card: could not get card data for '{startCard}'.");
                return;
            }

            int ammoOverride = 0;
            UnityEngine.Color white = new(1f, 1f, 1f, 1f);

            if (!originalStartCardColor.HasValue)
                originalStartCardColor = card.cardColor;

            if (isInfinite)
            {
                card.cardColor = white;
                ammoOverride = int.MaxValue / 2;
                if (!card.cardName.EndsWith("_Infinite"))
                    card.cardName += "_Infinite";
            }
            else
                card.cardColor = originalStartCardColor.Value;

            MethodInfo pickupMethod = typeof(MechController).GetMethod("DoCardPickup", BindingFlags.NonPublic | BindingFlags.Instance);
            pickupMethod.Invoke(mech, [card, ammoOverride]);
        }

        public static bool UseDiscardAbilityPrefixPatch(MechController __instance, ref bool __result, int cardInhHandIndex)
        {
            if (!NeonLite.Modules.Anticheat.Active)
                return true;

            var deckField = AccessTools.Field(typeof(MechController), "deck");
            PlayerCardDeck deck = deckField.GetValue(__instance) as PlayerCardDeck;

            var card = deck.GetCardInHand(cardInhHandIndex);
            PlayerCardData cardData = card.data;

            if (!cardData.cardName.EndsWith("Infinite"))
            {
                return true;
            }

            var discardMethod = AccessTools.Method(typeof(MechController), "UseDiscardAbility",
                [typeof(PlayerCardData), typeof(int), typeof(bool), typeof(bool)]);

            const bool discardOnSuccess = false;

            __result = (bool)discardMethod.Invoke(__instance, [cardData, cardInhHandIndex, discardOnSuccess, false]);

            return false;
        }

        public static void UpdateCardHUDPostfixPatch(PlayerUICardHUD __instance, PlayerCard card)
        {
            if (card.data.cardName.EndsWith("Infinite"))
                __instance.textAmmo.text = "Inf";
        }
    }
}