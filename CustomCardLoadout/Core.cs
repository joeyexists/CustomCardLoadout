using MelonLoader;
using HarmonyLib;
using System.Reflection;
using UnityEngine.SceneManagement;
using static CustomCardLoadout.Core;

[assembly: MelonInfo(typeof(CustomCardLoadout.Core), "CustomCardLoadout", "1.0.1-beta", "joeyexists", null)]
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
        private static PlayerCardData startCardData = null;
        private static UnityEngine.Color? originalCardColor = null;
        //private static UnityEngine.Color? originalStartCardColor = null;

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
                startCardEntry = category.CreateEntry("Card to spawn with", Cards.Dominion,
                    description: "Applies to every level");
                startCardInfiniteEntry = category.CreateEntry("Infinite ammo & discards", true,
                    description: "Unlimited ammo, card will not be used upon discarding.");

                enabledEntry.OnEntryValueChanged.Subscribe((_, newValue) =>
                {
                    if (newValue) EnableMod(modInstance);
                    else DisableMod();
                });

                startCardEntry.OnEntryValueChanged.Subscribe((_, newValue) =>
                {
                    UpdateStartCard(GetCardID(newValue));
                });

                startCardInfiniteEntry.OnEntryValueChanged.Subscribe((_, newValue) =>
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

        private static void EnableMod(Core modInstance)
        {
            GameInstance.OnLevelLoadComplete += OnLevelLoadComplete;
            ToggleInfiniteAmmoAndDiscardsPatch(Settings.startCardInfiniteEntry.Value);
            UpdateStartCard(GetCardID(Settings.startCardEntry.Value));
            modInstance.RegisterAntiCheat();
            modEnabled = true;
        }

        private static void DisableMod()
        {
            GameInstance.OnLevelLoadComplete -= OnLevelLoadComplete;
            RestoreStartCard();
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
            if (SceneManager.GetActiveScene().name.Equals("Heaven_Environment"))
            {
                return;
            }

            TryAddCard(startCardData);
        }

        private static void RestoreStartCard()
        {
            if (startCardData != null && startCardData.cardName.EndsWith("_Infinite"))
            {
                startCardData.cardName = startCardData.cardName.Substring(0, startCardData.cardName.Length - "_Infinite".Length);
                startCardData.cardColor = originalCardColor.Value;
            }
        }

        private static void UpdateStartCard(string cardID)
        {
            RestoreStartCard();

            // Get new card
            if (GameInstance.GetGameData() is not GameData gameData)
            {
                MelonLogger.Warning("Failed to update start card: GameData is null.");
                return;
            }

            if (gameData.GetCard(cardID) is not PlayerCardData newCard)
            {
                MelonLogger.Warning($"Failed to update start card: could not get card data for '{cardID}'.");
                return;
            }

            originalCardColor = newCard.cardColor;

            if (infiniteAmmoAndDiscardsPatched)
            {
                newCard.cardColor = new UnityEngine.Color(1f, 1f, 1f, 1f);
                if (!newCard.cardName.EndsWith("_Infinite"))
                    newCard.cardName += "_Infinite";
            }

            startCardData = newCard;
        }

        private static void TryAddCard(PlayerCardData card)
        {
            if (card == null || !NeonLite.Modules.Anticheat.Active) 
                return;

            if (UnityEngine.Object.FindObjectOfType<MechController>() is not MechController mech)
            {
                MelonLogger.Warning("Failed to add card: MechController not found.");
                return;
            }

            int ammoOverride = 0;

            if (infiniteAmmoAndDiscardsPatched)
            {
                ammoOverride = int.MaxValue / 2;
            }

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

            if (!cardData.cardName.EndsWith("_Infinite"))
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