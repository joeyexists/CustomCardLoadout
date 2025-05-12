using HarmonyLib;
using System.Reflection;
    
namespace CustomCardLoadout
{
    internal class HarmonyPatcher
    {
        public static bool IsPatched_MechController_UseDiscardAbility = false;
        public static bool IsPatched_PlayerUICardHUD_UpdateHUD = false;

        // patches MechController.UseDiscardAbility to allow infinite discards
        private static readonly MethodInfo MechController_UseDiscardAbility_Original =
            AccessTools.Method(typeof(MechController), "UseDiscardAbility", [typeof(int)]);

        private static readonly MethodInfo MechController_UseDiscardAbility_PrefixMethod =
            typeof(HarmonyPatcher).GetMethod(nameof(MechController_UseDiscardAbility_Prefix));

        private static readonly HarmonyMethod MechController_UseDiscardAbility_PrefixPatch =
            new(MechController_UseDiscardAbility_PrefixMethod);

        // patches PlayerUICardHUD.UpdateHUD to update the ammo text
        private static readonly MethodInfo PlayerUICardHUD_UpdateHUD_Original =
            AccessTools.Method(typeof(PlayerUICardHUD), "UpdateHUD");

        private static readonly MethodInfo PlayerUICardHUD_UpdateHUD_PostfixMethod =
            typeof(HarmonyPatcher).GetMethod(nameof(PlayerUICardHUD_UpdateHUD_Postfix));

        private static readonly HarmonyMethod PlayerUICardHUD_UpdateHUD_PostfixPatch =
            new(PlayerUICardHUD_UpdateHUD_PostfixMethod);

        public enum Patches
        {
            MechController_UseDiscardAbility_Patch,
            PlayerUICardHUD_UpdateHUD_Patch
        }

        public static void TogglePatch(Patches patch, bool apply)
        {
            if(patch == Patches.MechController_UseDiscardAbility_Patch)
            {
                if (IsPatched_MechController_UseDiscardAbility == apply)
                    return;
                if (apply)
                {
                    IsPatched_MechController_UseDiscardAbility = true;
                    Core.HarmonyInstance.Patch(MechController_UseDiscardAbility_Original,
                        prefix: MechController_UseDiscardAbility_PrefixPatch);
                }
                else
                {
                    Core.HarmonyInstance.Unpatch(MechController_UseDiscardAbility_Original,
                        MechController_UseDiscardAbility_PrefixMethod);
                    IsPatched_MechController_UseDiscardAbility = false;
                }
            }

            if (patch == Patches.PlayerUICardHUD_UpdateHUD_Patch)
            {
                if (IsPatched_PlayerUICardHUD_UpdateHUD == apply)
                    return;
                if (apply)
                {
                    IsPatched_PlayerUICardHUD_UpdateHUD = true;
                    Core.HarmonyInstance.Patch(PlayerUICardHUD_UpdateHUD_Original,
                        postfix: PlayerUICardHUD_UpdateHUD_PostfixPatch);
                }
                else
                {
                    Core.HarmonyInstance.Unpatch(PlayerUICardHUD_UpdateHUD_Original,
                        PlayerUICardHUD_UpdateHUD_PostfixMethod);
                    IsPatched_PlayerUICardHUD_UpdateHUD = false;
                }
            }
        }

        public static bool MechController_UseDiscardAbility_Prefix(MechController __instance, ref bool __result, int cardInhHandIndex)
        {
            if (!NeonLite.Modules.Anticheat.Active)
                return true;

            FieldInfo deckField = AccessTools.Field(typeof(MechController), "deck");
            PlayerCardDeck deck = deckField.GetValue(__instance) as PlayerCardDeck;

            PlayerCard card = deck.GetCardInHand(cardInhHandIndex);
            PlayerCardData cardData = card.data;

            if (cardData != LoadoutManager.firstCardSlot.cardData &&
                cardData != LoadoutManager.secondCardSlot.cardData)
            {
                return true;
            }

            if (cardData.cardID == LoadoutManager.RAPTURE_ID || 
                cardData.cardID == LoadoutManager.MIRACLE_KATANA_ID)
            {
                return true;
            }

            var discardMethod = AccessTools.Method(typeof(MechController), "UseDiscardAbility",
                [typeof(PlayerCardData), typeof(int), typeof(bool), typeof(bool)]);

            const bool discardOnSuccess = false;

            __result = (bool)discardMethod.Invoke(__instance, [cardData, cardInhHandIndex, discardOnSuccess, false]);

            return false;
        }

        public static void PlayerUICardHUD_UpdateHUD_Postfix(PlayerUICardHUD __instance, PlayerCard card)
        {
            if (card.data == LoadoutManager.firstCardSlot.cardData ||
                card.data == LoadoutManager.secondCardSlot.cardData)
            {
                if (card.GetCurrentAmmo() > 9999)
                    __instance.textAmmo.text = "Inf";
            }
        }

    }
}
