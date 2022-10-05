using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshArea))]
public class MeshAreaEditor : Editor
{
    private MeshArea meshArea;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Generate 1 layer"))
        {
            meshArea.GenerateOneLayers();
        }
        if (GUILayout.Button("Generate 2 layers"))
        {
            meshArea.GenerateTwoLayers();
        }
    }

    private void OnEnable()
    {
        meshArea = (MeshArea)target;
    }
}
