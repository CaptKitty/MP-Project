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
    public byte TerrainProfile;

    public static CampaignProvinceState FromProvince(int provinceIndex, int nationIndex, Province province)
    {
        return new CampaignProvinceState
        {
            ProvinceIndex = (ushort)provinceIndex,
            NationIndex = (ushort)nationIndex,
            Supply = province.supply,
            Population = province.population,
            TerrainProfile = (byte)province.terrainProfile
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ProvinceIndex);
        serializer.SerializeValue(ref NationIndex);
        serializer.SerializeValue(ref Supply);
        serializer.SerializeValue(ref Population);
        serializer.SerializeValue(ref TerrainProfile);
    }
}

public struct CampaignUnitState : INetworkSerializable
{
    public FixedString64Bytes ArmyId;
    public FixedString64Bytes UnitName;
    public int Amount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ArmyId);
        serializer.SerializeValue(ref UnitName);
        serializer.SerializeValue(ref Amount);
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

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref NationIndex);
        serializer.SerializeValue(ref Manpower);
        serializer.SerializeValue(ref BarracksLevel);
        serializer.SerializeValue(ref MercenaryLevel);
        serializer.SerializeValue(ref FarmLevel);
        serializer.SerializeValue(ref Income);
        serializer.SerializeValue(ref Gold);
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
