using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using System;

/// <summary>
/// IConvertGameObjectToEntity gets called when a gameobject converts into an entity
/// and allows you to add extra componenets during the converstion system - during which time you can
/// still access all game object components and transform hierarchy.
/// this method runs all on instances of the component implementing it in the scene.
/// </summary>
public class MeshArea : MonoBehaviour, IConvertGameObjectToEntity
{
    [SerializeField] private MeshAreaSettings mapSettings;

    private void Awake()
    {
        GetComponent<MeshFilter>().mesh = new Mesh() { subMeshCount = 1, name = "MeshArea" };
    }

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        MeshAreaRef mesh = new() { Value = GetComponent<MeshFilter>().mesh };

        dstManager.AddComponentData(entity, mapSettings);
        dstManager.AddComponentData(entity, mesh);
        dstManager.AddComponent<UpdateMeshArea>(entity);
    }
}

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
public struct MeshAreaSettings : IComponentData { public int2 mapDimentions; }

public struct UpdatingMeshArea : IComponentData { public double timeStamp; }

// components that do not declare any members are automatically catagoised as tagging components.
public struct UpdateMeshArea : IComponentData { }
public struct MeshAreaTriangulatorRun : IComponentData { }
