using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Clio.Utilities.Collections;
using ff14bot.Enums;
using ff14bot.Managers;


namespace Magitek.Models.Astrologian
{
    public static class AstrologianDefaultCardRules
    {
        private static int _cardRuleCounter = 0;

        public static readonly ObservableCollection<CardRule> DefaultCardRules = new ObservableCollection<CardRule>()
        {
            // PvP
            new CardRule()
            {
                CardPriority = _cardRuleCounter++,
                LogicType = CardLogicType.Pvp,
                CardType = AstrologianCardType.MeleeDPS
            },
            new CardRule()
            {
                CardPriority = _cardRuleCounter++,
                LogicType = CardLogicType.Pvp,
                CardType = AstrologianCardType.RangedDPS
            },

            // Normal Parties
            new CardRule()
            {
                CardPriority = _cardRuleCounter++,
                LogicType = CardLogicType.Party,
                CardType = AstrologianCardType.MeleeDPS
            },
            new CardRule()
            {
                CardPriority = _cardRuleCounter++,
                LogicType = CardLogicType.Party,
                CardType = AstrologianCardType.RangedDPS
            },

            // Raid Parties
            new CardRule()
            {
                CardPriority = _cardRuleCounter++,
                LogicType = CardLogicType.LargeParty,
                CardType = AstrologianCardType.MeleeDPS
            },
            new CardRule()
            {
                CardPriority = _cardRuleCounter++,
                LogicType = CardLogicType.LargeParty,
                CardType = AstrologianCardType.RangedDPS
            }
        };
    }
}

