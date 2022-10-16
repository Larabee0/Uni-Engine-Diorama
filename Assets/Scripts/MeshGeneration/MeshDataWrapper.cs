using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public struct MeshDataWrapper
{
    public double TimeStamp;
    public NativeParallelHashSet<int> areasIncluded;
    public Mesh.MeshDataArray meshDataArray;

    public MeshDataWrapper(double timeStamp, NativeParallelHashSet<int> chunksIncluded, Mesh.MeshDataArray meshDataArray)
    {
        TimeStamp = timeStamp;
        this.areasIncluded = chunksIncluded;
        this.meshDataArray = meshDataArray;
    }
}
