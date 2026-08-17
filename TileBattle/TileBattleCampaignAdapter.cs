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
            bool cavalry = source.unittype == UnitTypes.LightCavalry || source.unittype == UnitTypes.HeavyCavalry;
            bool rangedUnit = source.unittype == UnitTypes.Ranged || ranged != null && ranged.Throwable != null;
            TileWeaponControl control = InferWeaponControl(melee);
            int mass = source.unittype == UnitTypes.HeavyInfantry ? 150 : source.unittype == UnitTypes.HeavyCavalry ? 180 :
                source.unittype == UnitTypes.LightCavalry ? 100 : source.unittype == UnitTypes.LightInfantry ? 85 : 75;
            return new TileBattleUnitDefinition
            {
                Id = source.name,
                DisplayName = !string.IsNullOrEmpty(source.unitname) ? source.unitname : source.name,
                Initiative = Mathf.Max(1, source.Initiative),
                Actions = Mathf.Max(1, source.actions),
                BaseMass = mass,
                Strength = Mathf.Max(1, source.health),
                MeleeDamage = melee != null ? Mathf.Max(1, melee.attack) : 10,
                ArmorPercent = ArmorOf(source),
                ShieldPercent = ShieldOf(source),
                ShieldFrontEffectivenessPercent = Mathf.Clamp(source.shieldFrontEffectiveness, 0, 100),
                ShieldSideEffectivenessPercent = Mathf.Clamp(source.shieldSideEffectiveness, 0, 100),
                FrontThreat = control == TileWeaponControl.Pike ? 3 : control == TileWeaponControl.Spear ? 2 : 1,
                SideThreat = 0,
                WeaponControl = control,
                Cavalry = cavalry,
                Ranged = rangedUnit,
                RangedRange = ranged != null ? Mathf.Max(1, Mathf.RoundToInt((float)ranged.combatdistance)) : 0,
                RangedDamage = ranged != null ? Mathf.Max(1, ranged.attack) : 0,
                Ammunition = ranged != null ? Mathf.Max(0, ranged.ammo) : 0
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

        private static int ArmorOf(UnitSaveData source)
        {
            int armor = source.Armor != null && source.Armor.armor != null ? source.Armor.armor.armor : 0;
            return Mathf.Clamp(armor, 0, 80);
        }

        private static int ShieldOf(UnitSaveData source)
        {
            return source.Shield != null && source.Shield.armor != null
                ? Mathf.Clamp(Mathf.Max(source.Shield.armor.armor, source.Shield.armor.rangedarmor), 0, 80) : 0;
        }
    }
}
