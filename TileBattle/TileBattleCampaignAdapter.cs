using System;
using UnityEngine;

namespace ProjectX.TileBattle
{
    /// <summary>Read-only adapter from existing campaign assets into lightweight formation data.</summary>
    public static class TileBattleCampaignAdapter
    {
        public static TileBattleUnitDefinition CreateDefinition(UnitSaveData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Weapon melee = source.MeleeWeapon;
            Weapon ranged = source.RangedWeapon;
            bool openingThrowable = ranged != null && ranged.rangedUsage == RangedWeaponUsage.OpeningThrowable;
            bool cavalry = source.unittype == UnitTypes.LightCavalry || source.unittype == UnitTypes.HeavyCavalry;
            bool rangedUnit = !openingThrowable && (source.unittype == UnitTypes.Ranged || ranged != null && ranged.Throwable != null);
            TileWeaponControl control = InferWeaponControl(melee);
            int mass = source.unittype == UnitTypes.HeavyInfantry ? 150 : source.unittype == UnitTypes.HeavyCavalry ? 180 :
                source.unittype == UnitTypes.LightCavalry ? 100 : source.unittype == UnitTypes.LightInfantry ? 85 : 75;
            return new TileBattleUnitDefinition
            {
                Id = source.name,
                DisplayName = !string.IsNullOrEmpty(source.unitname) ? source.unitname : source.name,
                ReactionTime = source.ReactionTime,
                Actions = Mathf.Max(1, source.actions),
                BaseMass = mass,
                Strength = Mathf.Max(1, source.health),
                MeleeDamage = melee != null ? Mathf.Max(1, melee.attack) : 10,
                MeleeRange = melee != null ? Mathf.Max(1, Mathf.RoundToInt((float)melee.combatdistance)) : 1,
                MeleeReachPattern = ReachPattern(melee),
                MeleeAttackIntervalTicks = AttackIntervalTicks(melee),
                ArmorPercent = ArmorOf(source),
                ShieldPercent = ShieldOf(source),
                ShieldFrontEffectivenessPercent = source.Shield != null
                    ? Mathf.Clamp(source.Shield.shieldFrontEffectiveness, 0, 100) : 0,
                ShieldSideEffectivenessPercent = source.Shield != null
                    ? Mathf.Clamp(source.Shield.shieldSideEffectiveness, 0, 100) : 0,
                FrontThreat = control == TileWeaponControl.Pike ? 3 : control == TileWeaponControl.Spear ? 2 : 1,
                SideThreat = 0,
                WeaponControl = control,
                Cavalry = cavalry,
                Ranged = rangedUnit,
                OpeningThrowable = openingThrowable,
                RangedRange = ranged != null ? Mathf.Max(1, Mathf.RoundToInt((float)ranged.combatdistance)) : 0,
                RangedDamage = ranged != null ? Mathf.Max(1, ranged.attack) : 0,
                RangedAttackIntervalTicks = AttackIntervalTicks(ranged),
                Ammunition = ranged != null ? openingThrowable ? Mathf.Min(1, Mathf.Max(0, ranged.ammo)) : Mathf.Max(0, ranged.ammo) : 0,
                FormationType = FormationType(source),
                ForestImmune = HasFlag(source, "Forest Immune") || HasFlag(source, "ForestImmune") ||
                    HasFlag(source, "Forestry_Immunity") || HasFlag(source, "Forester"),
                Forester = HasFlag(source, "Forester"),
                RetainsMomentum = HasFlag(source, "Momentum")
            };
        }

        public static TileGeneralPersonality CreatePersonality(FieldArmyHolder army)
        {
            string stableIdentity = army != null && !string.IsNullOrEmpty(army.NetworkArmyId)
                ? army.NetworkArmyId : army != null ? army.gameObject.name : "General";
            string generatedName = army != null && army.fieldArmy != null
                ? NationContentResolver.GenerateGeneralName(army.fieldArmy.nation, stableIdentity) : "General";
            if (generatedName == "Unnamed General" && army != null) generatedName = army.gameObject.name;
            TileGeneralPersonality result = new TileGeneralPersonality { Name = generatedName };
            if (army == null || army.flaglist == null) return result;
            bool hasCharacterFlag = false;
            if (army.flaglist.Contains("Aggressive")) { result.Aggressive = 60; result.Bold = 35; hasCharacterFlag = true; }
            if (army.flaglist.Contains("Defensive")) { result.Defensive = 60; result.Patient = 30; hasCharacterFlag = true; }
            if (army.flaglist.Contains("Cautious")) { result.Cautious = 60; result.Patient = 25; hasCharacterFlag = true; }
            if (army.flaglist.Contains("Opportunistic")) { result.Opportunistic = 60; hasCharacterFlag = true; }
            if (army.flaglist.Contains("CavalryCommander")) { result.CavalryMinded = 70; hasCharacterFlag = true; }
            if (army.flaglist.Contains("Stubborn")) { result.Stubborn = 40; hasCharacterFlag = true; }
            if (!hasCharacterFlag)
            {
                string nation = army.fieldArmy != null && army.fieldArmy.nation != null ? army.fieldArmy.nation.name : string.Empty;
                string identity = !string.IsNullOrEmpty(army.NetworkArmyId) ? army.NetworkArmyId : army.gameObject.name + "|" + nation;
                result = CreateGeneratedPersonality(identity, result.Name);
            }
            return result;
        }

        /// <summary>Creates a repeatable fallback character without relying on Unity's random state.</summary>
        public static TileGeneralPersonality CreateGeneratedPersonality(string stableIdentity, string displayName = "General")
        {
            uint state = StableHash(string.IsNullOrEmpty(stableIdentity) ? displayName : stableIdentity);
            TileGeneralPersonality result = new TileGeneralPersonality
            {
                Name = string.IsNullOrEmpty(displayName) ? "General" : displayName,
                Competence = 35 + NextRange(ref state, 46)
            };

            int primary = NextRange(ref state, 7);
            int primaryStrength = 48 + NextRange(ref state, 23);
            ApplyGeneratedTrait(result, primary, primaryStrength);

            // A quieter secondary tendency prevents generated generals from being one-note
            // without allowing it to overwhelm their recognizable main character.
            int secondary = NextRange(ref state, 6);
            if (secondary >= primary) secondary++;
            ApplyGeneratedTrait(result, secondary, 15 + NextRange(ref state, 21));
            return result;
        }

        private static void ApplyGeneratedTrait(TileGeneralPersonality result, int trait, int strength)
        {
            switch (trait)
            {
                case 0: result.Aggressive = Mathf.Max(result.Aggressive, strength); result.Bold = Mathf.Max(result.Bold, strength / 2); break;
                case 1: result.Defensive = Mathf.Max(result.Defensive, strength); result.Patient = Mathf.Max(result.Patient, strength / 2); break;
                case 2: result.Cautious = Mathf.Max(result.Cautious, strength); result.Patient = Mathf.Max(result.Patient, strength / 3); break;
                case 3: result.Opportunistic = Mathf.Max(result.Opportunistic, strength); break;
                case 4: result.CavalryMinded = Mathf.Max(result.CavalryMinded, strength); result.Bold = Mathf.Max(result.Bold, strength / 3); break;
                case 5: result.Methodical = Mathf.Max(result.Methodical, strength); result.Patient = Mathf.Max(result.Patient, strength / 3); break;
                default: result.Bold = Mathf.Max(result.Bold, strength); result.Stubborn = Mathf.Max(result.Stubborn, strength / 2); break;
            }
        }

        private static uint StableHash(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619u; }
            return hash == 0u ? 0x9E3779B9u : hash;
        }

        private static int NextRange(ref uint state, int maximum)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)Mathf.Max(1, maximum));
        }

        private static TileWeaponControl InferWeaponControl(Weapon weapon)
        {
            string text = weapon != null ? ((weapon.name ?? string.Empty) + " " + (weapon.attacktype ?? string.Empty)).ToLowerInvariant() : string.Empty;
            if (text.Contains("pike") || text.Contains("phalanx")) return TileWeaponControl.Pike;
            if (text.Contains("spear") || text.Contains("lance")) return TileWeaponControl.Spear;
            return TileWeaponControl.Sword;
        }

        private static int AttackIntervalTicks(Weapon weapon)
        {
            double seconds = weapon != null ? weapon.attacktime : 1d;
            return Mathf.Max(1, Mathf.RoundToInt((float)(Math.Max(.01d, seconds) * TileBattleRules.DefaultTicksPerSecond)));
        }

        private static MeleeReachPattern ReachPattern(Weapon weapon)
        {
            if (weapon == null) return MeleeReachPattern.Standard;
            if (weapon.meleeReachPattern != MeleeReachPattern.Auto) return weapon.meleeReachPattern;
            return weapon.combatdistance >= 1.5d ? MeleeReachPattern.Long : MeleeReachPattern.Standard;
        }

        private static bool HasFlag(UnitSaveData source, string flag) => source != null && source.flaglist != null &&
            source.flaglist.Exists(item => string.Equals(item, flag, StringComparison.OrdinalIgnoreCase));

        private static TileFormationType FormationType(UnitSaveData source)
        {
            if (HasFlag(source, "Testudo")) return TileFormationType.Testudo;
            if (HasFlag(source, "Phalanx")) return TileFormationType.Phalanx;
            if (HasFlag(source, "Shieldwall") || HasFlag(source, "Shield Wall")) return TileFormationType.Shieldwall;
            if (HasFlag(source, "CavalryCharge") || HasFlag(source, "Cavalry Charge")) return TileFormationType.CavalryCharge;
            return TileFormationType.None;
        }

        private static int ArmorOf(UnitSaveData source)
        {
            int armor = source.Armor != null && source.Armor.armor != null
                ? Mathf.Max(source.Armor.armor.armor, source.Armor.armor.rangedarmor) : 0;
            return Mathf.Clamp(armor, 0, 80);
        }

        private static int ShieldOf(UnitSaveData source)
        {
            return source.Shield != null && source.Shield.armor != null
                ? Mathf.Clamp(Mathf.Max(source.Shield.armor.armor, source.Shield.armor.rangedarmor), 0, 80) : 0;
        }
    }
}
