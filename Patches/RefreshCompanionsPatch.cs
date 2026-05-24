using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace WiCCompanionExpandedTweaks.Patches
{
    [HarmonyPatch]
    internal static class RefreshCompanionsPatch
    {
        private const string WiCPrefix = "malfoycustom";

        private static MethodBase TargetMethod() =>
            AccessTools.Method(
                "BannerlordExpanded.CompanionExpanded.SpawnWanderers.Behaviors.SpawnCompanionsBehavior:RefreshCompanions");

        private static bool Prefix()
        {
            if (Campaign.Current == null) return false;

            var behavior = FindCompanionsBehavior();
            if (behavior == null) return false;

            var behaviorType = behavior.GetType();

            foreach (var town in Town.AllTowns)
            {
                var tavern = town.Settlement.LocationComplex?.GetLocationWithId("tavern");
                if (tavern == null) continue;

                var toRemove = town.Settlement.HeroesWithoutParty
                    .Where(h => h.IsWanderer && h.CompanionOf == null && h.Clan == null && !IsWiC(h))
                    .ToList();

                foreach (var hero in toRemove)
                {
                    LeaveSettlementAction.ApplyForCharacterOnly(hero);
                    hero.AddDeathMark(null, KillCharacterAction.KillCharacterActionDetail.Lost);
                    hero.ChangeState(Hero.CharacterStates.Dead);
                    hero.SetNewOccupation(Occupation.Headman);
                    AccessTools.Method(behaviorType, "RemoveFromAliveCompanions")
                        ?.Invoke(behavior, new object[] { hero });
                }
            }

            CleanUpDeadCompanions();

            int desired = (int)(float)(AccessTools
                .Method(behaviorType, "get__desiredTotalCompanionCount")
                ?.Invoke(behavior, null) ?? 0f);

            var trySpawn = AccessTools.Method(behaviorType, "TrySpawnNewCompanion");
            for (int i = 0; i < desired; i++)
                trySpawn?.Invoke(behavior, null);

            AccessTools.Method(behaviorType, "SwapCompanions")?.Invoke(behavior, null);

            return false;
        }

        private static CampaignBehaviorBase FindCompanionsBehavior()
        {
            var exact = Campaign.Current.GetCampaignBehavior<CompanionsCampaignBehavior>();
            if (exact != null) return exact;

            var all = GetAllBehaviors();
            if (all == null) return null;

            foreach (var b in all)
                if (b is CompanionsCampaignBehavior) return b;

            foreach (var b in all)
            {
                var m = b.GetType().GetMethod("TrySpawnNewCompanion",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (m != null) return b;
            }

            return null;
        }

        private static List<CampaignBehaviorBase> GetAllBehaviors()
        {
            var manager = AccessTools.Field(typeof(Campaign), "_campaignBehaviorManager")
                ?.GetValue(Campaign.Current);
            if (manager == null) return null;

            foreach (var fieldName in new[] { "_campaignBehavioprs", "_campaignBehaviors", "CampaignBehaviors", "_behaviors" })
            {
                var f = AccessTools.Field(manager.GetType(), fieldName);
                if (f == null) continue;
                var val = f.GetValue(manager);
                if (val == null) continue;

                if (val is IEnumerable<CampaignBehaviorBase> typed)
                    return typed.ToList();

                if (val is System.Collections.IEnumerable untyped)
                {
                    var result = new List<CampaignBehaviorBase>();
                    foreach (var item in untyped)
                    {
                        if (item is CampaignBehaviorBase b) result.Add(b);
                        else if (item != null)
                        {
                            var valueProp = item.GetType().GetProperty("Value");
                            if (valueProp?.GetValue(item) is CampaignBehaviorBase bv) result.Add(bv);
                        }
                    }
                    if (result.Count > 0) return result;
                }
            }

            return null;
        }

        private static void CleanUpDeadCompanions()
        {
            var unregister = AccessTools.Method(typeof(CampaignObjectManager), "UnregisterDeadHero");
            if (unregister == null) return;
            var mgr = Campaign.Current?.CampaignObjectManager;
            if (mgr == null) return;
            foreach (var hero in Hero.DeadOrDisabledHeroes.ToList())
            {
                if ((hero.Children == null || hero.Children.Count == 0) && hero.IsDead && hero.IsHeadman)
                    unregister.Invoke(mgr, new object[] { hero });
            }
        }

        private static bool IsWiC(Hero h)
        {
            var id = h?.Template?.StringId;
            return id != null && id.StartsWith(WiCPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}