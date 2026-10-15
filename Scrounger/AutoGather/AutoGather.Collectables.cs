using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.UI;
using GatherBuddy.Classes;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Scrounger.Utils.Extensions;

//Credit: https://github.com/FFXIV-CombatReborn/GatherBuddyReborn/blob/main/GatherBuddy/AutoGather/AutoGather.Collectables.cs
namespace Scrounger.AutoGather
{
    public partial class AutoGather
    {
        private CollectableRotation? CurrentCollectableRotation;

        // GatheringMasterpiece AtkValue indices, matching GatherBuddyReborn's GatheringMasterpieceReader
        private static class MasterpieceValue
        {
            public const int Collectability   = 13;
            public const int ScourGain        = 48;
            public const int BrazenGainMin    = 49;
            public const int MeticulousGain   = 51;
            public const int IntegrityCurrent = 62;
            public const int IntegrityMax     = 63;
            public const int LowThreshold     = 65;
            public const int MidThreshold     = 66;
            public const int HighThreshold    = 67;
        }

        private static unsafe int? ReadMasterpieceValue(AddonGatheringMasterpiece* addon, int index)
        {
            var unit = &addon->AtkUnitBase;
            if (unit->AtkValues == null || index >= unit->AtkValuesCount)
                return null;

            var value = unit->AtkValues[index];
            return value.Type switch
            {
                AtkValueType.UInt => (int)value.UInt,
                AtkValueType.Int  => value.Int,
                _                 => null,
            };
        }

        private unsafe partial class CollectableRotation
        {
            public CollectableRotation(ConfigPreset config, Gatherable item, uint quantity)
            {
                this.config = config;
                shouldUseFullRotation = Player.Object.CurrentGp >= config.CollectableActionsMinGP;
                this.item = item;
                this.quantity = quantity;
            }

            private readonly bool shouldUseFullRotation = false;
            private readonly ConfigPreset config;
            private readonly Gatherable item;
            private readonly uint quantity;

            public Actions.BaseAction GetNextAction(AddonGatheringMasterpiece* MasterpieceAddon)
            {
                var itemsLeft = (int)(quantity - item.GetInventoryCount());

                if (itemsLeft <= 0 && Scrounger.Config.AbandonNodes)
                    throw new NoGatherableItemsInNodeException();

                int collectability   = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.Collectability) ?? 0;
                int currentIntegrity = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.IntegrityCurrent) ?? 0;
                int maxIntegrity     = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.IntegrityMax) ?? 0;
                int scourColl        = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.ScourGain) ?? 0;
                int meticulousColl   = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.MeticulousGain) ?? 0;
                int brazenColl       = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.BrazenGainMin) ?? 0;

                if (ShouldUseWise(currentIntegrity, maxIntegrity))
                    return Actions.Wise;

                var (targetScore, minScore) = GetCollectabilityScores(MasterpieceAddon);

                if (collectability >= targetScore)
                {
                    if ((shouldUseFullRotation || config.CollectableAlwaysUseSolidAge)
                     && ShouldSolidAgeCollectables(currentIntegrity, maxIntegrity, itemsLeft))
                        return Actions.SolidAge;
                    else
                        return Actions.Collect;
                }

                if (currentIntegrity == 1
                 && collectability >= minScore)
                    return Actions.Collect;

                if (shouldUseFullRotation && NeedScrutiny(collectability, scourColl, meticulousColl, brazenColl, targetScore) && ShouldUseScrutiny())
                    return Actions.Scrutiny;

                if (meticulousColl + collectability >= targetScore
                 && ShouldUseMeticulous())
                    return Actions.Meticulous;

                if (Player.Status.Any(s => s.StatusId == 3911 /*Collector's High Standard*/) && ShouldUseBrazen())
                    return Actions.Brazen;

                if (scourColl + collectability >= targetScore
                 && ShouldUseScour())
                    return Actions.Scour;

                if (ShouldUseMeticulous())
                    return Actions.Meticulous;

                //Fallback path if some actions are disabled.
                if (Player.Status.Any(s => s.StatusId == 2418 /*Collector's Standard*/) && ShouldUseBrazen())
                    return Actions.Brazen;
                if (ShouldUseScour())
                    return Actions.Scour;
                if (ShouldUseBrazen())
                    return Actions.Brazen;

                throw new NoCollectableActionsException();
            }

            private (int targetScore, int minScore) GetCollectabilityScores(AddonGatheringMasterpiece* MasterpieceAddon)
            {
                if (config.CollectableManualScores)
                    return (config.CollectableTargetScore, config.CollectableMinScore);

                int targetScore, minScore;
                var lowScore = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.LowThreshold) ?? 0;

                // Check threshold markers in descending order (15, 14, 13) and use the first visible one for target score
                if (MasterpieceAddon->AtkUnitBase.GetNodeById(15)->IsVisible())
                    targetScore = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.HighThreshold) ?? lowScore;
                else if (MasterpieceAddon->AtkUnitBase.GetNodeById(14)->IsVisible())
                    targetScore = ReadMasterpieceValue(MasterpieceAddon, MasterpieceValue.MidThreshold) ?? lowScore;
                else
                    targetScore = lowScore;

                // For custom deliveries and quest items, we always want max collectability
                if (item.GatheringData.Unknown3 is 3 or 4 or 6)
                    minScore = targetScore;
                else
                    minScore = lowScore;

                return (targetScore, minScore);
            }

            private bool NeedScrutiny(int collectability, int scourColl, int meticulousColl, int brazenColl, int targetScore)
            {
                if (scourColl + collectability >= targetScore && ShouldUseScour())
                    return false;
                if (meticulousColl + collectability >= targetScore && ShouldUseMeticulous())
                    return false;
                if (brazenColl + collectability >= targetScore && ShouldUseBrazen())
                    return false;

                return true;
            }
            private bool ShouldUseMeticulous()
            {
                if (Player.Level < Actions.Meticulous.MinLevel)
                    return false;
                if (Player.Object.CurrentGp < Actions.Meticulous.GpCost)
                    return false;
                if (config.ChooseBestActionsAutomatically)
                    return true;
                if (Player.Object.CurrentGp < config.CollectableActions.Meticulous.MinGP
                 || Player.Object.CurrentGp > config.CollectableActions.Meticulous.MaxGP)
                    return false;

                return config.CollectableActions.Meticulous.Enabled;
            }

            private bool ShouldUseScour()
            {
                if (Player.Level < Actions.Brazen.MinLevel)
                    return false;
                if (Player.Object.CurrentGp < Actions.Brazen.GpCost)
                    return false;
                if (config.ChooseBestActionsAutomatically)
                    return true;
                if (Player.Object.CurrentGp < config.CollectableActions.Scour.MinGP
                 || Player.Object.CurrentGp > config.CollectableActions.Scour.MaxGP)
                    return false;

                return config.CollectableActions.Scour.Enabled;
            }

            private bool ShouldUseBrazen()
            {
                if (Player.Level < Actions.Meticulous.MinLevel)
                    return false;
                if (Player.Object.CurrentGp < Actions.Meticulous.GpCost)
                    return false;
                if (config.ChooseBestActionsAutomatically)
                    return true;
                if (Player.Object.CurrentGp < config.CollectableActions.Brazen.MinGP
                 || Player.Object.CurrentGp > config.CollectableActions.Brazen.MaxGP)
                    return false;

                return config.CollectableActions.Brazen.Enabled;
            }

            private bool ShouldUseScrutiny()
            {
                if (Player.Level < Actions.Scrutiny.MinLevel)
                    return false;
                if (Player.Object.CurrentGp < Actions.Scrutiny.GpCost)
                    return false;
                if (Player.Status.Any(s => s.StatusId == Actions.Scrutiny.EffectId))
                    return false;
                if (config.ChooseBestActionsAutomatically)
                    return true;
                if (Player.Object.CurrentGp < config.CollectableActions.Scrutiny.MinGP
                 || Player.Object.CurrentGp > config.CollectableActions.Scrutiny.MaxGP)
                    return false;

                return config.CollectableActions.Scrutiny.Enabled;
            }

            private bool ShouldSolidAgeCollectables(int integrity, int maxIntegrity, int itemsLeft)
            {
                if (integrity > Math.Min(2, maxIntegrity - 1))
                    return false;
                if (itemsLeft <= integrity)
                    return false;
                if (Player.Level < Actions.SolidAge.MinLevel)
                    return false;
                if (Player.Object.CurrentGp < Actions.SolidAge.GpCost)
                    return false;
                if (Player.Status.Any(s => s.StatusId == Actions.SolidAge.EffectId))
                    return false;
                if (config.ChooseBestActionsAutomatically)
                    return true;
                if (Player.Object.CurrentGp < config.CollectableActions.SolidAge.MinGP
                 || Player.Object.CurrentGp > config.CollectableActions.SolidAge.MaxGP)
                    return false;

                return config.CollectableActions.SolidAge.Enabled;
            }
        }
    }
}
