﻿using System.Linq;
using System.Threading.Tasks;
using ff14bot;
using ff14bot.Managers;
using Magitek.Extensions;
using Magitek.Models.WhiteMage;
using Magitek.Utilities;

namespace Magitek.Logic.WhiteMage
{
    internal static class SingleTarget
    {
        public static async Task<bool> Stone()
        {
            if (!WhiteMageSettings.Instance.Stone)
                return false;

            if (Core.Me.ClassLevel >= 72)
            {
                return await Spells.Glare.Cast(Core.Me.CurrentTarget);
            }

            if (Core.Me.ClassLevel >= 64)
            {
                return await Spells.Stone4.Cast(Core.Me.CurrentTarget);
            }

            if (Core.Me.ClassLevel >= 54)
            {
                return await Spells.Stone3.Cast(Core.Me.CurrentTarget);
            }

            if (Core.Me.ClassLevel >= 18)
            {
                return await Spells.Stone2.Cast(Core.Me.CurrentTarget);
            }

            return await Spells.Stone.Cast(Core.Me.CurrentTarget);
        }

        public static async Task<bool> FluidAura()
        {
            if (!WhiteMageSettings.Instance.DoDamage)
                return false;

            if (!WhiteMageSettings.Instance.FluidAura)
                return false;

            var enemy = GameObjectManager.Attackers.FirstOrDefault(r => r.TargetGameObject == Core.Me && Core.Me.Distance(r) < 7);

            if (enemy == null)
                return false;

            return await Spells.FluidAura.Cast(enemy);
        }

        public static async Task<bool> AfflatusMisery()
        {
            if (!WhiteMageSettings.Instance.DoDamage)
                return false;

            if (ActionResourceManager.WhiteMage.BloodLily < 3)
                return false;

            return await Spells.AfflatusMisery.Cast(Core.Me.CurrentTarget);
        }

        public static async Task<bool> Dots()
        {
            if (WhiteMageSettings.Instance.UseTimeTillDeathForDots)
            {
                var combatTimeLeft = Core.Me.CurrentTarget.CombatTimeLeft();

                if (combatTimeLeft > 0 && combatTimeLeft < WhiteMageSettings.Instance.DontDotIfEnemyDyingWithin)
                    return false;
            }
            else
            {
                if (!Core.Me.CurrentTarget.HealthCheck(WhiteMageSettings.Instance.DotHealthMinimum, WhiteMageSettings.Instance.DotHealthMinimumPercent))
                    return false;
            }
            
            return await Aero();
        }

        private static async Task<bool> Aero()
        {
            if (!WhiteMageSettings.Instance.Aero)
                return false;

            if (Core.Me.ClassLevel < 46)
            {
                if (Core.Me.CurrentTarget.HasAura(Auras.Aero, true, WhiteMageSettings.Instance.DotRefreshSeconds * 1000))
                    return false;

                return await Spells.Aero.CastAura(Core.Me.CurrentTarget, Auras.Aero, true, WhiteMageSettings.Instance.DotRefreshSeconds * 1000);
            }

            if (Core.Me.ClassLevel < 72)
            {
                if (Core.Me.CurrentTarget.HasAura(Auras.Aero2, true, WhiteMageSettings.Instance.DotRefreshSeconds * 1000))
                    return false;

                return await Spells.Aero2.CastAura(Core.Me.CurrentTarget, Auras.Aero2, true, WhiteMageSettings.Instance.DotRefreshSeconds * 1000);
            }

            else
            {
                if (Core.Me.CurrentTarget.HasAura(Auras.Dia, true, WhiteMageSettings.Instance.DotRefreshSeconds * 1000))
                    return false;

                return await Spells.Dia.CastAura(Core.Me.CurrentTarget, Auras.Dia, true, WhiteMageSettings.Instance.DotRefreshSeconds * 1000);
            }
        }
    }
}