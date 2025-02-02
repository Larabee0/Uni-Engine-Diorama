using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(MeshArea))]
public class MeshAreaEditor : Editor
{
    private MeshArea meshArea;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Generate"))
        {
            meshArea.Generate();
        }
    }

    private void OnEnable()
    {
        meshArea = (MeshArea)target;
    }
}
