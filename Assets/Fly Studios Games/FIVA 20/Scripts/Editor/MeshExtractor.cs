using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class MeshRecursiveExtractor : EditorWindow
{
    [MenuItem("Tools/Extract All Meshes (Recursive)")]
    public static void ExtractMeshesRecursive()
    {
        GameObject root = Selection.activeGameObject;

        if (root == null)
        {
            Debug.LogError("Selectează obiectul ROOT (părintele) din ierarhie!");
            return;
        }

        // Creăm un folder dedicat pentru a nu amesteca sute de fișiere
        string folderPath = EditorUtility.OpenFolderPanel("Alege folderul unde salvăm mesh-urile", "Assets", "");
        if (string.IsNullOrEmpty(folderPath)) return;

        // Convertim path-ul absolut în path de proiect (Assets/...)
        folderPath = "Assets" + folderPath.Replace(Application.dataPath, "");

        // Găsim toate MeshFilter-ele din copii și sub-copii
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        int count = 0;

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            // Creăm copia mesh-ului
            Mesh meshCopy = Instantiate(mf.sharedMesh);
            string meshName = mf.gameObject.name + "_" + mf.sharedMesh.name + ".asset";
            
            // Curățăm numele de caractere invalide
            foreach (char c in Path.GetInvalidFileNameChars()) { meshName = meshName.Replace(c, '_'); }

            string assetPath = Path.Combine(folderPath, meshName);
            
            // Salvăm mesh-ul ca fișier .asset
            AssetDatabase.CreateAsset(meshCopy, assetPath);
            
            // Înlocuim referința în scenă cu noul fișier extras
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"<color=cyan>Gata!</color> Am extras {count} mesh-uri individuale în {folderPath}");
    }
}