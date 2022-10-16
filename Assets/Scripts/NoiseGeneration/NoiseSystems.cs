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

public partial class NoiseGenSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbEndSys;

    private EntityQuery GenerateHeightMapQuery;
    protected override void OnCreate()
    {
        var entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(UpdateMeshArea),
                typeof(MeshAreaSettings),
                typeof(MeshAreaRef),
                typeof(GenerateHeightMap),
                typeof(SimpleNoise)
            },
            None = new ComponentType[]
            {
                typeof(UpdatingMeshArea)
            }
        };
        GenerateHeightMapQuery = GetEntityQuery(entityQueries);

        ecbEndSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        if (!GenerateHeightMapQuery.IsEmpty)
        {
            EntityCommandBuffer ecbEnd = ecbEndSys.CreateCommandBuffer();
            NativeArray<Entity> entities = GenerateHeightMapQuery.ToEntityArray(Allocator.TempJob);
            NativeArray<SimpleNoise> noiseComps =GenerateHeightMapQuery.ToComponentDataArray<SimpleNoise>(Allocator.TempJob);
            NativeArray<MeshAreaSettings> areaComps = GenerateHeightMapQuery.ToComponentDataArray<MeshAreaSettings>(Allocator.TempJob);

            for (int i = 0; i < entities.Length; i++)
            {
                NativeArray<HeightMapElement> heightMap = new(areaComps[i].mapDimentions.x * areaComps[i].mapDimentions.y, Allocator.TempJob);
                var heightMapGen = new SimpleNoiseHeightMapGenerator
                {
                    areaSettings = areaComps[i],
                    simpleNoise = noiseComps[i],
                    heightMap = heightMap
                };

                heightMapGen.Schedule(heightMap.Length, 64).Complete();
                ecbEnd.SetBuffer<HeightMapElement>(entities[i]).CopyFrom(heightMap);
                ecbEnd.RemoveComponent<GenerateHeightMap>(entities[i]);
                heightMap.Dispose();
            }
        }
    }
}
