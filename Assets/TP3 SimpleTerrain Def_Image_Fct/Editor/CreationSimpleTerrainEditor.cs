using System.Globalization;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CreationSimpleTerrain))]
public class CreationSimpleTerrainEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();


        // ------------------------------------------------------------
        // Script
        // ------------------------------------------------------------

        GUI.enabled = false;

        EditorGUILayout.ObjectField(
            "Script",
            MonoScript.FromMonoBehaviour(
                (CreationSimpleTerrain)target
            ),
            typeof(CreationSimpleTerrain),
            false
        );

        GUI.enabled = true;


        // ------------------------------------------------------------
        // Paramètres classiques
        // ------------------------------------------------------------

        DrawPropertiesExcluding(
            serializedObject,
            "choixModeDeformation",
            "m_Script",
            "numTexture",
            "textures",
            "typeFonction"
        );


        CreationSimpleTerrain terrain =
            (CreationSimpleTerrain)target;


        // ------------------------------------------------------------
        // Informations du maillage
        // ------------------------------------------------------------

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Caractéristiques du maillage",
            EditorStyles.boldLabel
        );


        long resolution =
            1L << terrain.puissance2Resolution;


        long nombreVertices =
            resolution * resolution;


        long nombreTriangles =
            2L *
            (resolution - 1) *
            (resolution - 1);


        EditorGUILayout.LabelField(
            "Résolution = "
            + resolution.ToString(
                "N0",
                CultureInfo.GetCultureInfo("fr-FR")
            )
        );


        EditorGUILayout.LabelField(
            "Vertices = "
            + nombreVertices.ToString(
                "N0",
                CultureInfo.GetCultureInfo("fr-FR")
            )
        );


        EditorGUILayout.LabelField(
            "Triangles = "
            + nombreTriangles.ToString(
                "N0",
                CultureInfo.GetCultureInfo("fr-FR")
            )
        );


        // ------------------------------------------------------------
        // Index format
        // ------------------------------------------------------------

        if (nombreVertices > 65535)
        {
            EditorGUILayout.HelpBox(
                "Le maillage dépassera 65 535 vertices : " +
                "les indices 32 bits seront utilisés.",
                MessageType.Info
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Les indices 16 bits peuvent être utilisés.",
                MessageType.Info
            );
        }


        // ------------------------------------------------------------
        // Mode de génération
        // ------------------------------------------------------------

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Mode de génération",
            EditorStyles.boldLabel
        );


        SerializedProperty choixMode =
            serializedObject.FindProperty(
                "choixModeDeformation"
            );


        EditorGUILayout.PropertyField(
            choixMode
        );


        CreationSimpleTerrain.ChoixModeDeformation choix =
            (CreationSimpleTerrain.ChoixModeDeformation)
            choixMode.enumValueIndex;


        // ------------------------------------------------------------
        // HeightMap
        // ------------------------------------------------------------

        if (choix ==
            CreationSimpleTerrain.ChoixModeDeformation.Texture)
        {
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(
                    "numTexture"
                )
            );


            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(
                    "textures"
                ),
                true
            );
        }


        // ------------------------------------------------------------
        // Fonctions
        // ------------------------------------------------------------

        else
        {
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty(
                    "typeFonction"
                )
            );
        }


        serializedObject.ApplyModifiedProperties();
    }
}