using MelonLoader;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace CustomCardLoadout
{
    public class LoadoutManager
    {
        public static CustomCardSlot firstCardSlot = new();
        public static CustomCardSlot secondCardSlot = new();

        public static readonly UnityEngine.Color UnlimitedDiscardsCardColor = new(1f, 1f, 1f);
        private const int InfiniteAmmoOverride = 1000000000;

        public static bool IsInfiniteAmmoEnabled = false;
        public static bool removeDiscardLocks = false;

        public const string RAPTURE_ID = "RAPTURE";
        public const string MIRACLE_KATANA_ID = "KATANA_MIRACLE";

        public class CustomCardSlot(
            PlayerCardData cardData = null, 
            UnityEngine.Color? originalCardColor = null, 
            int cardCount = 0)
        {
            public PlayerCardData cardData = cardData;
            public UnityEngine.Color? originalCardColor = originalCardColor;
            public int cardCount = cardCount;

            public void SetOverrideColor(UnityEngine.Color overrideColor)
            {
                if (cardData != null)
                    if (cardData.cardID != RAPTURE_ID && cardData.cardID != MIRACLE_KATANA_ID)
                        cardData.cardColor = overrideColor;
            }

            public void RestoreColor()
            {
                if (cardData != null && originalCardColor.HasValue)
                    cardData.cardColor = originalCardColor.Value;
            }
        }

        public static string GetCardID(Core.Settings.CardOptions card) => card switch
        {
            Core.Settings.CardOptions.Purify => "MACHINEGUN",
            Core.Settings.CardOptions.Elevate => "PISTOL",
            Core.Settings.CardOptions.Godspeed => "RIFLE",
            Core.Settings.CardOptions.Stomp => "UZI",
            Core.Settings.CardOptions.Fireball => "SHOTGUN",
            Core.Settings.CardOptions.Dominion => "ROCKETLAUNCHER",
            Core.Settings.CardOptions.BookOfLife => "RAPTURE",
            Core.Settings.CardOptions.MiracleKatana => "KATANA_MIRACLE",
            _ => string.Empty
        };

        public static void UpdateCardSlot(ref CustomCardSlot cardSlot, Core.Settings.CardOptions selectedCard, int cardCount = 1)
        {
            string selectedCardID = GetCardID(selectedCard);

            if (HarmonyPatcher.IsPatched_MechController_UseDiscardAbility)
                RestoreCardColors();

            if (selectedCard == Core.Settings.CardOptions.None)
            {
                cardSlot = new CustomCardSlot();
                return;
            }

            if (Core.GameInstance.GetGameData() is not GameData gameData)
            {
                MelonLogger.Warning("Failed to update card slot: GameData is null.");
                return;
            }

            if (gameData.GetCard(selectedCardID) is not PlayerCardData cardData)
            {
                MelonLogger.Warning($"Failed to update card slot: could not get card data for '{selectedCardID}'.");
                return;
            }

            cardSlot.cardData = cardData;
            cardSlot.originalCardColor = cardData.cardColor;

            cardSlot.cardCount = cardCount;

            if (HarmonyPatcher.IsPatched_MechController_UseDiscardAbility)
                SetOverrideCardColors(UnlimitedDiscardsCardColor);
        }

        public static void SetOverrideCardColors(UnityEngine.Color color)
        {
            firstCardSlot.SetOverrideColor(color);
            secondCardSlot.SetOverrideColor(color);
        }

        public static void RestoreCardColors()
        {
            firstCardSlot.RestoreColor();
            secondCardSlot.RestoreColor();
        }

        public static void ToggleInfiniteAmmo(bool enable)
        {
            IsInfiniteAmmoEnabled = enable;
        }

        public static void EnableRemoveDiscardLocks()
        {
            removeDiscardLocks = true;
        }

        private static void AddCards(CustomCardSlot firstCardSlot, CustomCardSlot secondCardSlot)
        {
            if (firstCardSlot.cardData == null && secondCardSlot.cardData == null)
                return;

            if (UnityEngine.Object.FindObjectOfType<MechController>() is not MechController mech)
            {
                MelonLogger.Warning("Failed to add card: MechController not found.");
                return;
            }

            MethodInfo pickupMethod = typeof(MechController).GetMethod("DoCardPickup", BindingFlags.NonPublic | BindingFlags.Instance);

            void ProcessCardSlot(CustomCardSlot slot)
            {
                if (slot.cardData == null || slot.cardCount <= 0)
                    return;

                int ammo = 0;
                if (IsInfiniteAmmoEnabled && slot.cardData.cardID != RAPTURE_ID)
                    ammo = InfiniteAmmoOverride / slot.cardCount;

                for (int i = 0; i < slot.cardCount; i++)
                {
                    pickupMethod.Invoke(mech, [slot.cardData, ammo]);
                }
            }

            ProcessCardSlot(secondCardSlot);
            ProcessCardSlot(firstCardSlot);
        }

        private static void RemoveDiscardLocks()
        {
            if (Core.GameInstance.GetCurrentLevel() is not LevelData currentLevel)
            {
                MelonLogger.Warning($"Failed to remove discard locks: LevelData is null.");
                return;
            }

            foreach (DiscardLockData discardLockData in currentLevel.discardLockData)
                discardLockData.cards.Clear();
        }
        public static void OnLevelLoadComplete()
        {
            if (SceneManager.GetActiveScene().name.Equals("Heaven_Environment")
                || !NeonLite.Modules.Anticheat.Active)
                return;

            AddCards(firstCardSlot, secondCardSlot);

            if (removeDiscardLocks)
                RemoveDiscardLocks();
        }
    }
}
