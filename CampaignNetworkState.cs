using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct CampaignArmyState : INetworkSerializable
{
    public FixedString64Bytes ArmyId;
    public FixedString64Bytes DisplayName;
    public FixedString64Bytes NationName;
    public ulong OwnerClientId;
    public Vector3 MapPosition;
    public Vector3 MapTarget;
    public int Supply;
    public int UnitCount;
    public bool InEncounter;

    public static CampaignArmyState FromArmy(FieldArmyHolder army)
    {
        return new CampaignArmyState
        {
            ArmyId = army.NetworkArmyId,
            DisplayName = army.gameObject.name,
            NationName = army.fieldArmy.nation.name,
            OwnerClientId = army.IsHumanControlled ? army.NetworkOwnerClientId : ulong.MaxValue,
            MapPosition = army.transform.position,
            MapTarget = army.target,
            Supply = army.fieldArmy.ArmySupply,
            UnitCount = army.fieldArmy.GrabArmySize()
            ,InEncounter = army.flaglist.Contains("Battle")
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ArmyId);
        serializer.SerializeValue(ref DisplayName);
        serializer.SerializeValue(ref NationName);
        serializer.SerializeValue(ref OwnerClientId);
        serializer.SerializeValue(ref MapPosition);
        serializer.SerializeValue(ref MapTarget);
        serializer.SerializeValue(ref Supply);
        serializer.SerializeValue(ref UnitCount);
        serializer.SerializeValue(ref InEncounter);
    }
}

public struct CampaignProvinceState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public ushort NationIndex;
    public int Supply;
    public int Population;
    public int Urbanization;
    public byte TerrainProfile;
    public int RegionalFoodStorage;
    public int RegionalFoodStorageCapacity;
    public int RegionalFoodShortage;

    public static CampaignProvinceState FromProvince(int provinceIndex, int nationIndex, Province province)
    {
        CampaignRegion region = Owners.Instance != null ? Owners.Instance.CallRegionByString(province.region) : null;
        RegionalLoyaltyShare foodShare = region != null ? region.GetLoyaltyShare(province.nation, true) : null;
        return new CampaignProvinceState
        {
            ProvinceIndex = (ushort)provinceIndex,
            NationIndex = (ushort)nationIndex,
            Supply = province.supply,
            Population = province.population,
            Urbanization = Mathf.Clamp(province.urbanization, -100, province.MaximumDevelopment),
            TerrainProfile = (byte)province.terrainProfile,
            RegionalFoodStorage = foodShare != null ? foodShare.foodStorage : 0,
            RegionalFoodStorageCapacity = foodShare != null ? foodShare.foodStorageCapacity : 1000,
            RegionalFoodShortage = foodShare != null ? foodShare.lastFoodShortage : 0
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex);
        serializer.SerializeValue(ref NationIndex);
        serializer.SerializeValue(ref Supply);
        serializer.SerializeValue(ref Population);
        serializer.SerializeValue(ref Urbanization);
        serializer.SerializeValue(ref TerrainProfile);
        serializer.SerializeValue(ref RegionalFoodStorage);
        serializer.SerializeValue(ref RegionalFoodStorageCapacity);
        serializer.SerializeValue(ref RegionalFoodShortage);
    }
}

public struct CampaignUnitState : INetworkSerializable
{
    public FixedString64Bytes ArmyId;
    public FixedString64Bytes UnitName;
    public int Amount;
    public byte Origin;
    public FixedString128Bytes EntitlementId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ArmyId);
        serializer.SerializeValue(ref UnitName);
        serializer.SerializeValue(ref Amount);
        serializer.SerializeValue(ref Origin);
        serializer.SerializeValue(ref EntitlementId);
    }
}

public struct CampaignRecruitmentOrderState : INetworkSerializable
{
    public FixedString64Bytes ArmyId;
    public FixedString64Bytes UnitName;
    public int Amount;
    public int RemainingTicks;
    public byte Origin;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ArmyId);
        serializer.SerializeValue(ref UnitName);
        serializer.SerializeValue(ref Amount);
        serializer.SerializeValue(ref RemainingTicks);
        serializer.SerializeValue(ref Origin);
    }
}

public struct CampaignConstructionOrderState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public int SlotIndex;
    public FixedString64Bytes BuildingId;
    public int TargetLevel;
    public int RemainingTicks;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex);
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref BuildingId);
        serializer.SerializeValue(ref TargetLevel);
        serializer.SerializeValue(ref RemainingTicks);
    }
}

public struct CampaignNationState : INetworkSerializable
{
    public ushort NationIndex;
    public int Manpower;
    public int BarracksLevel;
    public int MercenaryLevel;
    public int FarmLevel;
    public int Income;
    public int Gold;
    public int UpkeepDebt;
    public int LevyLawPermille;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex);
        serializer.SerializeValue(ref Manpower);
        serializer.SerializeValue(ref BarracksLevel);
        serializer.SerializeValue(ref MercenaryLevel);
        serializer.SerializeValue(ref FarmLevel);
        serializer.SerializeValue(ref Income);
        serializer.SerializeValue(ref Gold);
        serializer.SerializeValue(ref UpkeepDebt);
        serializer.SerializeValue(ref LevyLawPermille);
    }
}

public struct CampaignLawState : INetworkSerializable
{
    public ushort NationIndex;
    public FixedString64Bytes Id;
    public FixedString64Bytes DisplayName;
    public int AmountPermille;
    public byte Effect;
    public byte Operation;
    public byte Target;
    public bool AnySocioEconomicClass;
    public byte SocioEconomicClass;
    public byte CultureScope;
    public FixedString64Bytes CultureName;
    public bool AnyUnitOrigin;
    public byte UnitOrigin;
    public bool AnyAllegiance;
    public FixedString64Bytes AllegianceId;
    public bool UseAllegianceFocusedRegions;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex); serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref DisplayName); serializer.SerializeValue(ref AmountPermille);
        serializer.SerializeValue(ref Effect); serializer.SerializeValue(ref Operation); serializer.SerializeValue(ref Target);
        serializer.SerializeValue(ref AnySocioEconomicClass); serializer.SerializeValue(ref SocioEconomicClass);
        serializer.SerializeValue(ref CultureScope); serializer.SerializeValue(ref CultureName);
        serializer.SerializeValue(ref AnyUnitOrigin); serializer.SerializeValue(ref UnitOrigin);
        serializer.SerializeValue(ref AnyAllegiance); serializer.SerializeValue(ref AllegianceId);
        serializer.SerializeValue(ref UseAllegianceFocusedRegions);
    }
}

public struct CampaignClassRuleState : INetworkSerializable
{
    public ushort NationIndex;
    public FixedString64Bytes LawId;
    public FixedString64Bytes DisplayName;
    public byte Type;
    public byte AffectedClass;
    public byte ResultingClass;
    public FixedString64Bytes CultureName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex); serializer.SerializeValue(ref LawId);
        serializer.SerializeValue(ref DisplayName); serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref AffectedClass); serializer.SerializeValue(ref ResultingClass);
        serializer.SerializeValue(ref CultureName);
    }
}

public struct CampaignActiveEdictState : INetworkSerializable
{
    public ushort NationIndex;
    public FixedString64Bytes ExtensionId;
    public FixedString128Bytes Title;
    public FixedString64Bytes TargetAllegianceId;
    public int RemainingTicks;
    public bool IsAftermath;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex); serializer.SerializeValue(ref ExtensionId);
        serializer.SerializeValue(ref Title); serializer.SerializeValue(ref TargetAllegianceId);
        serializer.SerializeValue(ref RemainingTicks); serializer.SerializeValue(ref IsAftermath);
    }
}

public struct CampaignFactionFlagState : INetworkSerializable
{
    public ushort NationIndex;
    public FixedString64Bytes Flag;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex);
        serializer.SerializeValue(ref Flag);
    }
}

public struct CampaignBuildingState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public FixedString64Bytes BuildingId;
    public int Level;
    public int MaxLevel;
    public int SlotIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex);
        serializer.SerializeValue(ref BuildingId);
        serializer.SerializeValue(ref Level);
        serializer.SerializeValue(ref MaxLevel);
        serializer.SerializeValue(ref SlotIndex);
    }
}

public struct CampaignHoldingState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public FixedString128Bytes InstanceId;
    public FixedString64Bytes HoldingId;
    public int Level;
    public int SlotIndex;
    public FixedString64Bytes CultureName;
    public byte SocioEconomicClass;
    public FixedString64Bytes Allegiance;
    public bool LevyEnabled;
    public FixedString64Bytes AdaptationTargetId;
    public int AdaptationPressure;
    public int AdaptationCooldownTicks;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex); serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref HoldingId);
        serializer.SerializeValue(ref Level); serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref CultureName); serializer.SerializeValue(ref SocioEconomicClass);
        serializer.SerializeValue(ref Allegiance);
        serializer.SerializeValue(ref LevyEnabled);
        serializer.SerializeValue(ref AdaptationTargetId);
        serializer.SerializeValue(ref AdaptationPressure);
        serializer.SerializeValue(ref AdaptationCooldownTicks);
    }
}

public struct CampaignAllegianceState : INetworkSerializable
{
    public ushort NationIndex;
    public FixedString64Bytes Id;
    public FixedString64Bytes DisplayName;
    public byte Type;
    public FixedString64Bytes PrimaryIdentityId;
    public FixedString64Bytes DynamicIdentityId;
    public FixedString512Bytes CurrentInterestRegionIds;
    public FixedString512Bytes FutureInterestRegionIds;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex); serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref DisplayName); serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref PrimaryIdentityId); serializer.SerializeValue(ref DynamicIdentityId);
        serializer.SerializeValue(ref CurrentInterestRegionIds); serializer.SerializeValue(ref FutureInterestRegionIds);
    }
}

public struct CampaignHoldingConstructionOrderState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public int SlotIndex;
    public FixedString128Bytes HoldingInstanceId;
    public FixedString64Bytes HoldingId;
    public int TargetLevel;
    public int RemainingTicks;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex); serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref HoldingInstanceId);
        serializer.SerializeValue(ref HoldingId); serializer.SerializeValue(ref TargetLevel);
        serializer.SerializeValue(ref RemainingTicks);
    }
}

public struct CampaignMercenaryState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public FixedString64Bytes UnitName;
    public int Available;
    public int Capacity;
    public float RegenerationPerTurn;
    public float RegenerationProgress;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex);
        serializer.SerializeValue(ref UnitName);
        serializer.SerializeValue(ref Available);
        serializer.SerializeValue(ref Capacity);
        serializer.SerializeValue(ref RegenerationPerTurn);
        serializer.SerializeValue(ref RegenerationProgress);
    }
}

public struct CampaignLevyState : INetworkSerializable
{
    public ushort ProvinceIndex;
    public FixedString128Bytes EntitlementId;
    public FixedString64Bytes RuleId;
    public FixedString64Bytes UnitName;
    public int BuildingSlot;
    public FixedString64Bytes HoldingId;
    public FixedString128Bytes HoldingInstanceId;
    public int Ordinal;
    public byte State;
    public bool Eligible;
    public int RemainingTicks;
    public FixedString64Bytes RaisedArmyId;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex); serializer.SerializeValue(ref EntitlementId);
        serializer.SerializeValue(ref RuleId); serializer.SerializeValue(ref UnitName);
        serializer.SerializeValue(ref BuildingSlot); serializer.SerializeValue(ref Ordinal);
        serializer.SerializeValue(ref HoldingId); serializer.SerializeValue(ref HoldingInstanceId);
        serializer.SerializeValue(ref State); serializer.SerializeValue(ref Eligible);
        serializer.SerializeValue(ref RemainingTicks); serializer.SerializeValue(ref RaisedArmyId);
    }
}
