using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Buddy.Coroutines;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Helpers;
using ff14bot.Managers;
using ff14bot.Objects;
using ICSharpCode.SharpZipLib.Zip;
using static ff14bot.Managers.ActionResourceManager.Astrologian;
using Magitek.Extensions;
using Magitek.Models.Account;
using Magitek.Models.Astrologian;
using Magitek.Utilities;
using Newtonsoft.Json;
using TreeSharp;
using static Magitek.Utilities.Routines.Astrologian;
using Auras = Magitek.Utilities.Auras;

namespace Magitek.Logic.Astrologian
{
    internal static class Card
    {
        public static async Task<bool> Play()
        {
            if (!AstrologianSettings.Instance.Play) return false;

            // should use sleeve draw with lightspeed and put out as many cards as possible
            // if (Core.Me.HasAura(Auras.Lightspeed)) return false;

            if (OnGcd) return false;

            if (Core.Me.IsCasting) return false;

            //if (DrawnCard() != AstrologianCard.None) return await PlayDrawn();
            if (DrawnCard() != AstrologianCard.None) return await PlayDrawnFromJson();

            return false;

        }

        public static async Task<bool> Draw()
        {
            if (!AstrologianSettings.Instance.Draw)
                return false;

            if (DrawnCard() != AstrologianCard.None)
                return false;

            // if (Core.Me.HasAura(Auras.Lightspeed)) return false;

            if (Globals.OnPvpMap)
            {
                if (Spells.PvpDraw.Cooldown != TimeSpan.Zero) return false;

                if (!Core.Me.InCombat) return false;

                if (Globals.InParty &&
                    PartyManager.VisibleMembers.Count(r => r.GameObject.Distance() < 30 && r.GameObject.HasCardAura()) > 2)
                    return false;

                return await Spells.PvpDraw.Cast(Core.Me);
            }

            if (Spells.Draw.Cooldown != TimeSpan.Zero) return false;

            CanRedraw = (ActionManager.HasSpell(Spells.Redraw.Id) && Spells.Redraw.Cooldown == TimeSpan.Zero);
            CanMinorArcana = (ActionManager.HasSpell(Spells.MinorArcana.Id) &&
                              Spells.MinorArcana.Cooldown == TimeSpan.Zero && Arcana == AstrologianCard.None);

            if (!ShouldPrepCardsOutOfCombat()) return false;

            if (Core.Me.InCombat && Combat.CombatTotalTimeLeft <
                AstrologianSettings.Instance.DontDrawWhenCombatTimeIs) return false;

            if (!Core.Me.InCombat)
            {
                if (!AstrologianSettings.Instance.CardRuleDefaultToMinorArcana || !CanMinorArcana) return false;
            }

            return await Spells.Draw.Cast(Core.Me);
        }

        private static async Task<bool> PlayDrawnSolo()
        {
            switch (DrawnCard())
            {
                // Melee DPS
                case AstrologianCard.Arrow:
                case AstrologianCard.Balance:
                case AstrologianCard.Spear:
                    if (CanRedraw) return await Spells.Redraw.Cast(Core.Me);
                    if (Core.Me.InCombat) return await Spells.PlayDrawn.Cast(Core.Me);
                break;
                // Ranged DPS
                case AstrologianCard.Bole:
                case AstrologianCard.Ewer:
                case AstrologianCard.Spire:
                    if (Core.Me.InCombat) return await Spells.PlayDrawn.Cast(Core.Me);
                    break;
                case AstrologianCard.LordofCrowns:
                case AstrologianCard.LadyofCrowns:
                    if (Core.Me.InCombat) return await Spells.PlayDrawn.Cast(Core.Me);
                    break;
                
            }
            return false;
        }

        private static async Task<bool> PlayDrawnFromJson() //All PlayDrawnFromJson Logic flows through here.
        {            
            var drawncard = DrawnCard();

            if (drawncard == AstrologianCard.None) return false;

            if (Core.Me.IsCasting) return false;

            if (OnGcd) return false;
            
            if (AstrologianSettings.Instance.CardRules == null)
            {
                Logger.WriteInfo(@"No Card Rules Found... Writing " +
                                 AstrologianDefaultCardRules.DefaultCardRules.Count + " Rules to Settings.");
                AstrologianSettings.Instance.CardRules = AstrologianDefaultCardRules.DefaultCardRules;
                Logger.WriteInfo(@"Default Card Rules Write complete.");
            }

            if (Globals.OnPvpMap) return await ProcessCardLogic(CardLogicType.Pvp, drawncard);

            CanRedraw = (ActionManager.HasSpell(Spells.Redraw.Id) && Spells.Redraw.Cooldown == TimeSpan.Zero);
            CanMinorArcana = (ActionManager.HasSpell(Spells.MinorArcana.Id) &&
                              Spells.MinorArcana.Cooldown == TimeSpan.Zero && Arcana == AstrologianCard.None);
            CanUndraw = (ActionManager.HasSpell(Spells.Undraw.Id) && Spells.Undraw.Cooldown == TimeSpan.Zero &&
                         DrawnCard() != AstrologianCard.None);

            if (!Globals.InParty) return await ProcessCardLogic(CardLogicType.Solo, drawncard);

            if (PartyManager.NumMembers <= 4) return await ProcessCardLogic(CardLogicType.Party, drawncard);

            if (PartyManager.NumMembers > 4) return await ProcessCardLogic(CardLogicType.LargeParty, drawncard);

            return false;
        }

        private static async Task<bool> ProcessCardLogic(CardLogicType logictype, AstrologianCard card)
        {
            if (AstrologianSettings.Instance.CardRules == null) return false;
            
            if (!LastCardAction.CanCastNewAction) return false;

            AstrologianCardType cardType = CardToCardType(card);
            var cardRulesToProcess = AstrologianSettings.Instance.CardRules.Where(r => r.CardType == cardType).OrderBy(r => r.CardPriority);

            return await ProcessCardRule(cardRulesToProcess, logictype);
        }

        private static async Task<bool> ProcessCardRule(IEnumerable<CardRule> cardRulesToProcess, CardLogicType logictype)
        {
            if (cardRulesToProcess == null) return false;

            var rulesToProcess = cardRulesToProcess as IList<CardRule> ?? cardRulesToProcess.ToList();

            var processed = false;

            // cards shouldnt be played out of combat for seals
            if (!Core.Me.InCombat)
                return false;
            
            //Logger.WriteInfo($@"Processing up to {rulesToProcess.Count} {logictype} rules");

            foreach (var cardRule in rulesToProcess)
            {
                //Logger.WriteInfo($"Processing rule: {cardRule.CardPriority}"); //For testing that the card rule processing is going by priority
                await Coroutine.Yield();
                if (processed)
                {
                    Logger.WriteInfo($"Detected that we've already processed a rule for {cardRule.CardType}");
                    return true;
                }
                
                CardTargets.Clear();

                GameObject target = null;
                // ReSharper disable once SuggestVarOrType_SimpleTypes
                TargetConditions targetconditions = cardRule.TargetConditions;

                // CardTargets.Add(Core.Me);
                CardTargets = PartyManager.VisibleMembers.Select(r => r.BattleCharacter).Where(r =>
                        r.IsTargetable && r.InLineOfSight() && r.Icon != PlayerIcon.Viewing_Cutscene).ToList();

                //if (cardRule.CardPriority == 33) Logger.WriteInfo($"CardTargets Starting Count: {CardTargets.Count()}");
                CardTargets.RemoveAll(r => r.HasCardAura() || r.CurrentHealth < 1 || r.IsDead || !r.IsValid);

                //if (cardRule.CardPriority == 33) Logger.WriteInfo($"CardTargets After Death Clean: {CardTargets.Count()}");

                if (targetconditions != null)
                {
                    if (targetconditions.HasTarget != null)
                    {
                        CardTargets.RemoveAll(r => r.HasTarget != targetconditions.HasTarget);
                    }

                    if (targetconditions.IsJob?.Count > 0)
                    {
                        CardTargets.RemoveAll(r => !targetconditions.IsJob.Contains(r.CurrentJob));
                    }

                    if (targetconditions.JobOrder != null)
                    {
                        CardTargets = CardTargets.OrderBy(x =>
                        {
                            var index = targetconditions.JobOrder.IndexOf(x.CurrentJob);

                            if (index == -1)
                                index = int.MaxValue;

                            return index;
                        }).ToList();
                    }
                    
                    if (targetconditions.PlayerName != null)
                        target = CardTargets.FirstOrDefault(r => r.Name == targetconditions.PlayerName);
                    else
                        target = CardTargets.FirstOrDefault();

                    if (target == null)
                        continue;
                }

                if (logictype == CardLogicType.Pvp)
                {
                    if (!await Spells.PvpPlayDrawn.Cast(target)) return false;
                    LogRuleProcessed(cardRule);
                    processed = true;
                    return true;
                }

                if (!await Spells.PlayDrawn.Cast(target)) return false;
                LogRuleProcessed(cardRule);
                processed = true;
                return true;
            }

            processed = true;
            return false;
        }

        private static void LogRuleDetails(CardRule rule)
        {
            /*Logger.WriteInfo(@"========================================Card Rule========================================");
            if (rule == null) Logger.WriteInfo("\t" + @"CardRule is null. Nothing to output.");
            else
            {
                Logger.WriteInfo("\t" + $@"LogicType: {rule.LogicType}" + "\t" + $@"PlayType: {rule.PlayType}" + "\t" + $@"Card: {rule.Card}" + "\t" + $@"Priority: {rule.CardPriority}");
                if (rule.Conditions != null) 
                {
                    Logger.WriteInfo("\t" + @"Conditions:");
                    if (rule.Conditions.InCombat != null) Logger.WriteInfo("\t\t" + $@"InCombat {rule.Conditions.InCombat}");
                    if (rule.Conditions.JobsNotInParty != null) Logger.WriteInfo("\t\t" + $@"JobsNotInParty: {string.Join(",", rule.Conditions.JobsNotInParty.Select(r => r.ToString()))}");
                    if (rule.Conditions.RolesNotInParty != null) Logger.WriteInfo("\t\t" + $@"RolesNotInParty: {string.Join(",", rule.Conditions.RolesNotInParty.Select(r => r.ToString()))}");
                }
                Logger.WriteInfo("\t" + $@"Action: {rule.Action}" + "\t" + $@"Target: {rule.Target}");
                if (rule.TargetConditions != null)
                {
                    Logger.WriteInfo("\tTargetConditions:");
                    if (rule.TargetConditions.HasTarget != null) Logger.WriteInfo("\t\t" + $@"HasTarget {rule.TargetConditions.HasTarget}");
                    if (rule.TargetConditions.HpLessThan != null) Logger.WriteInfo("\t\t" + $@"HpLessThan {rule.TargetConditions.HpLessThan}");
                    if (rule.TargetConditions.MpLessThan != null) Logger.WriteInfo("\t\t" + $@"MpLessThan {rule.TargetConditions.MpLessThan}");
                    if (rule.TargetConditions.TpLessThan != null) Logger.WriteInfo("\t\t" + $@"TpLessThan {rule.TargetConditions.TpLessThan}");
                    if (rule.TargetConditions.IsRole != null) Logger.WriteInfo("\t\t" + $@"IsRole {string.Join(",", rule.TargetConditions.IsRole.Select(r => r.ToString()))}");
                    if (rule.TargetConditions.JobOrder != null) Logger.WriteInfo("\t\t" + $@"JobOrder {string.Join(",", rule.TargetConditions.JobOrder.Select(r => r.ToString()))}");
                    if (rule.TargetConditions.IsJob != null) Logger.WriteInfo("\t\t" + $@"IsJob {string.Join(",", rule.TargetConditions.IsJob.Select(r => r.ToString()))}");
                    if (rule.TargetConditions.Choice != null) Logger.WriteInfo("\t\t" + $@"Choice {rule.TargetConditions.Choice}");
                    if (rule.TargetConditions.PlayerName != null) Logger.WriteInfo("\t\t" + $@"PlayerName {rule.TargetConditions.PlayerName}");
                    if (rule.TargetConditions.WithAlliesNearbyMoreThan != null) Logger.WriteInfo("\t\t" + $@"WithAlliesNearbyMoreThan {rule.TargetConditions.WithAlliesNearbyMoreThan}");
                }
                Logger.WriteInfo("\tCurrent Information");
                Logger.WriteInfo("\t\t" + $@"InCombat: {incombat}");
                Logger.WriteInfo("\t\t" + $@"CanRedraw: {CanRedraw}");
            }
            Logger.WriteInfo(@"========================================Card Rule========================================");*/
        }
        
        private static void LogRuleProcessed(CardRule rule)
        {
            /*if (rule == null) return;
            
            LastCardActionDateTime = DateTime.Now;
            LastCardAction.LastActionDateTime = DateTime.Now;
            
            var targetToPrint = "";
            if (rule.Target == CardTarget.Me) targetToPrint = "Me";
            if (rule.Target == CardTarget.PartyMember) targetToPrint = "a Party Member";
            if (rule.TargetConditions?.PlayerName != null)
                targetToPrint = targetToPrint + $" named {rule.TargetConditions?.PlayerName}";
            
            var relevantConditions = "";
            
            var relevantTargetConditions = "";
            
            switch (rule.Action)
            {
                case CardAction.Play:
                    if (rule.LogicType == CardLogicType.Pvp) LastCardAction.LastAction = Spells.PvpPlayDrawn;
                    else switch (rule.PlayType)
                    {
                        case CardPlayType.Held:
                            LastCardAction.LastAction = Spells.PlaySpread;
                            break;
                        case CardPlayType.Drawn:
                            LastCardAction.LastAction = Spells.PlayDrawn;
                            break;
                    }
                    Logger.WriteInfo("\t" + $@"We're Playing the {rule.PlayType} card {rule.Card} on {targetToPrint}");
                    break;
               case CardAction.MinorArcana:
                    LastCardAction.LastAction = Spells.MinorArcana;
                    Logger.WriteInfo("\t" + $@"We're using Minor Arcana on the card {rule.Card}.");
                    break;
                case CardAction.Redraw:
                    LastCardAction.LastAction = Spells.Redraw;
                    LastCardAction.CardBeforeRedrawn = rule.Card;
                    Logger.WriteInfo("\t" + $@"We're Redrawing the card {rule.Card}.");
                    break;
                case CardAction.Undraw:
                    LastCardAction.LastAction = Spells.Undraw;
                    Logger.WriteInfo("\t" + $@"We're Undrawing the card {rule.Card}.");
                    break;
            }
            if (!BaseSettings.Instance.DebugPlayerCasting) return;
            Logger.WriteInfo($"Drawn Card is {DrawnCard()}");
            Logger.WriteInfo("[CardRules]\t" + $@"LogicType: {rule.LogicType}" + "\t" + $@"PlayType: {rule.PlayType}" + "\t" +
                             $@"Card: {rule.Card}" + "\t" + $@"Priority: {rule.CardPriority}");
            
            if (rule.Conditions != null)
            {
                if (rule.Conditions.InCombat != null) relevantConditions = relevantConditions + $"InCombat ({rule.Conditions.InCombat}) ";
                if (rule.Conditions.JobsNotInParty.Count > 0) relevantConditions = relevantConditions + $@"JobsNotInParty: ({string.Join(",", rule.Conditions.JobsNotInParty.Select(r => r.ToString()))}) ";
                if (rule.Conditions.RolesNotInParty.Count > 0) relevantConditions = relevantConditions + $@"RolesNotInParty: ({string.Join(",", rule.Conditions.RolesNotInParty.Select(r => r.ToString()))}) ";

                if (relevantConditions != "") Logger.WriteInfo("[CardRules]\t" + $"Relevant Conditions: {relevantConditions}");
            }
            if (rule.TargetConditions == null) return;
            if (rule.TargetConditions.HasTarget != null) relevantTargetConditions = relevantTargetConditions + $@"HasTarget ({rule.TargetConditions.HasTarget}) "; 
            if (rule.TargetConditions.HpLessThan != null && rule.TargetConditions.HpLessThan > 1 && rule.TargetConditions.HpLessThan < 100) relevantTargetConditions = relevantTargetConditions + $@"HpLessThan ({rule.TargetConditions.HpLessThan}) ";
            if (rule.TargetConditions.MpLessThan != null && rule.TargetConditions.MpLessThan > 1 && rule.TargetConditions.MpLessThan < 100) relevantTargetConditions = relevantTargetConditions + $@"MpLessThan ({rule.TargetConditions.MpLessThan}) ";
            if (rule.TargetConditions.TpLessThan != null && rule.TargetConditions.TpLessThan > 1 && rule.TargetConditions.TpLessThan < 100) relevantTargetConditions = relevantTargetConditions + $@"TpLessThan ({rule.TargetConditions.TpLessThan}) ";
            if (rule.TargetConditions.IsRole.Count > 0) relevantTargetConditions = relevantTargetConditions + $@"IsRole ({string.Join(",", rule.TargetConditions.IsRole.Select(r => r.ToString()))}) ";
            if (rule.TargetConditions.JobOrder.Count > 0) relevantTargetConditions = relevantTargetConditions + $@"JobOrder ({string.Join(",",rule.TargetConditions.JobOrder.Select(r => r.ToString()))}) ";
            if (rule.TargetConditions.IsJob.Count > 0) relevantTargetConditions = relevantTargetConditions + $@"IsJob ({string.Join(",",rule.TargetConditions.IsJob.Select(r => r.ToString()))}) ";
            //if (rule.TargetConditions.Choice != null) relevantTargetConditions = relevantTargetConditions + $@"Choice ({rule.TargetConditions.Choice}) ";
            if (rule.TargetConditions.PlayerName != null) relevantTargetConditions = relevantTargetConditions + $@"PlayerName ({rule.TargetConditions.PlayerName}) "; 
            if (rule.TargetConditions.WithAlliesNearbyMoreThan != null && rule.TargetConditions.WithAlliesNearbyMoreThan > 0) relevantTargetConditions = relevantTargetConditions + $@"WithAlliesNearbyMoreThan ({rule.TargetConditions.WithAlliesNearbyMoreThan}) ";

            if (relevantTargetConditions != "")
                Logger.WriteInfo("[CardRules]\t" + $"Relevant Target Conditions: {relevantTargetConditions}");*/
        }

        private static List<BattleCharacter> CardTargets = new List<BattleCharacter>();

        private const double LastCardThreshold = 500;
        private static DateTime LastCardActionDateTime { get; set; }
        public static double TimeSinceTheLastCardAction => (DateTime.Now - LastCardActionDateTime).Milliseconds;

        private static class LastCardAction
        {
            private const double Threshold = 500;
            public static SpellData LastAction { private get; set; }

            public static DateTime LastActionDateTime { private get; set; }
            
            public static AstrologianCard CardBeforeRedrawn { private get; set; }

            private static double TimeSinceLastCardAction => (DateTime.Now - LastActionDateTime).Milliseconds;
            
            public static bool CanCastNewAction 
            {
                get
                {
                    if (LastAction == null) return true;
                    var lastActionOnCooldown = LastAction.Cooldown > TimeSpan.Zero;
                    var lastActionGoingOnCooldown = TimeSinceLastCardAction < LastAction.AdjustedCooldown.Milliseconds;
                    var lastActionDoneLongTimeAgo = TimeSinceLastCardAction > LastAction.AdjustedCooldown.Milliseconds;
                    var lastActionMoreThanThresholdAgo = true;
                    if (LastAction == Spells.Redraw)
                    {
                        if (DrawnCard() != CardBeforeRedrawn)
                            lastActionMoreThanThresholdAgo =
                                (DateTime.Now - LastActionDateTime).Milliseconds > Threshold;
                        else {
                            lastActionMoreThanThresholdAgo =
                                (DateTime.Now - LastActionDateTime).Seconds > 2;
                        }
                    }
                    return (lastActionOnCooldown || lastActionDoneLongTimeAgo) && !lastActionGoingOnCooldown && lastActionMoreThanThresholdAgo;
                }
            }
        }
    }
    
}