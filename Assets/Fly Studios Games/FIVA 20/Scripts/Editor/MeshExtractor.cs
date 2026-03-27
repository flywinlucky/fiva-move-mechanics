using UnityEngine;
using UnityEditor;
using System.IO;

public class MeshExtractor : EditorWindow
{
    [MenuItem("Tools/Extract Selected Mesh")]
    public static void ExtractMesh()
    {
        // Verificăm dacă avem ceva selectat în ierarhie
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null)
        {
            Debug.LogError("MeshExtractor: Selectează un obiect din scenă mai întâi!");
            return;
        }

        MeshFilter meshFilter = selectedObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogError("MeshExtractor: Obiectul selectat nu are un MeshFilter sau un Mesh valid.");
            return;
        }

        // Creăm o copie a mesh-ului pentru a rupe legătura cu FBX-ul original
        Mesh meshCopy = Instantiate(meshFilter.sharedMesh);
        
        // Alegem unde să salvăm fișierul
        string path = EditorUtility.SaveFilePanelInProject(
            "Salvează Mesh-ul extras",
            selectedObject.name + "_Extracted",
            "asset",
            "Introduceți un nume pentru noul mesh asset");

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(meshCopy, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Opțional: Înlocuim automat mesh-ul în scenă cu cel nou creat (pentru confirmare)
            meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            Debug.Log("<color=green>Succes!</color> Mesh-ul a fost extras și salvat la: " + path);
        }
    }
}