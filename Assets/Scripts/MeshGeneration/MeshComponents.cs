using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// ICompenentData is how you declare an entity component in Unity ECS.
// class IComponentDatas can have anyhting from managed C# as a member.
// struct IComponentDatas are restricted to value types, and cannot contain any array type, including native
// containers.
// entities can have array like data structs accosiated with them using the IBufferElement interface
// 
// according to unity it is best to have as few managed components as possible
// it is also best ot have components be as small as possible and for them to contain
// no logic beyond basic accessors and static methods. Logic goes in systems not components.


public class MeshAreaRef : IComponentData { public Mesh Value; }

[Serializable]
public struct MeshAreaSettings : IComponentData
{
    public int2 mapDimentions;
    public ShaderPicker shader;
    public ErosionMode erosionMode;
    public int2 textureTiling;
    [Range(0f, 1f)]
    public float floorPercentage;
    public Color32 floorColour;
    public Color32 lower;
    public Color32 higher;
}

[Serializable]
public struct RelativeNoiseData : IComponentData
{
    public float2 minMax;
    public float mid;
    public float flatFloor;
    [HideInInspector] public float minValue;
}


public struct UpdatingMeshArea : IComponentData { public double timeStamp; }

// components that do not declare any members are automatically catagoised as tagging components.
public struct UpdateMeshArea : IComponentData { }
public struct MeshAreaTriangulatorRun : IComponentData { }
