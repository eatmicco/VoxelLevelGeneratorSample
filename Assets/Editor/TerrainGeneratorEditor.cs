using UnityEngine;
using UnityEditor;
using System.Collections;

namespace Automata.Editor
{
    [CustomEditor(typeof(TerrainGenerator))]
    public class TerrainGeneratorEditor : UnityEditor.Editor
    {

        public override void OnInspectorGUI()
        {
            TerrainGenerator generator = (TerrainGenerator)target;

            DrawDefaultInspector();

            if (GUILayout.Button("Update"))
            {
                generator.GenerateTerrain();
            }
        }

    }
}