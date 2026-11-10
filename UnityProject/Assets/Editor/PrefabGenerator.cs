using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Gera prefabs a partir de arquivos JSON em Assets/Resources/PrefabData/
/// Rode via: Unity -batchmode -executeMethod PrefabGenerator.GenerateAll -quit
/// </summary>
public static class PrefabGenerator
{
    const string DataPath = "Assets/Resources/PrefabData";
    const string OutputPath = "Assets/Prefabs";

    [MenuItem("Tools/Prefab Generator/Generate All Prefabs")]
    public static void GenerateAll()
    {
        EnsureFolders();
        var files = Directory.GetFiles(DataPath, "*.json");
        int total = 0;

        foreach (var file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                var data = JsonUtility.FromJson<PrefabCollection>(json);
                if (data?.prefabs != null)
                {
                    foreach (var prefabData in data.prefabs)
                    {
                        CreatePrefab(prefabData);
                        total++;
                    }
                }
                Debug.Log($"[PrefabGenerator] Processado: {Path.GetFileName(file)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PrefabGenerator] Erro em {file}: {e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[PrefabGenerator] Concluído! {total} prefabs gerados em {OutputPath}");

        // Em batch mode, encerra o editor
        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    [MenuItem("Tools/Prefab Generator/Clear All Prefabs")]
    public static void ClearAll()
    {
        if (Directory.Exists(OutputPath))
        {
            Directory.Delete(OutputPath, true);
            AssetDatabase.Refresh();
            Debug.Log($"[PrefabGenerator] Pasta {OutputPath} limpa");
        }
    }

    static void EnsureFolders()
    {
        if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);
        if (!Directory.Exists(OutputPath)) Directory.CreateDirectory(OutputPath);
    }

    static void CreatePrefab(PrefabData data)
    {
        var go = new GameObject(data.name);

        // Adiciona componentes definidos no JSON
        foreach (var compData in data.components)
        {
            AddComponent(go, compData);
        }

        // Salva como prefab
        string prefabPath = Path.Combine(OutputPath, $"{data.name}.prefab").Replace('\\', '/');
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        UnityEngine.Object.DestroyImmediate(go);

        if (prefab == null)
            Debug.LogError($"[PrefabGenerator] Falha ao salvar prefab: {data.name}");
        else
            Debug.Log($"[PrefabGenerator] Criado: {prefabPath}");
    }

    static void AddComponent(GameObject go, ComponentData compData)
    {
        Type type = GetType(compData.type);
        if (type == null)
        {
            Debug.LogWarning($"[PrefabGenerator] Tipo não encontrado: {compData.type}");
            return;
        }

        var component = go.AddComponent(type);

        // Aplica propriedades do JSON (usa reflection para campos públicos/serialize)
        if (!string.IsNullOrEmpty(compData.properties))
        {
            JsonUtility.FromJsonOverwrite(compData.properties, component);
        }
    }

    static Type GetType(string typeName)
    {
        // Tenta tipos built-in do Unity primeiro
        var builtIn = Type.GetType($"UnityEngine.{typeName}, UnityEngine");
        if (builtIn != null) return builtIn;

        // Tenta no namespace global (seus scripts)
        var global = Type.GetType(typeName);
        if (global != null) return global;

        // Procura em assemblies carregados
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(typeName);
            if (t != null) return t;
        }

        return null;
    }

    // ===== Classes de dados para JSON =====

    [Serializable]
    public class PrefabCollection
    {
        public PrefabData[] prefabs;
    }

    [Serializable]
    public class PrefabData
    {
        public string name;
        public ComponentData[] components;
    }

    [Serializable]
    public class ComponentData
    {
        public string type;           // Ex: "SpriteRenderer", "BoxCollider2D", "EnemyController"
        public string properties;     // JSON com propriedades: {"size":{"x":1,"y":1},"isTrigger":true}
    }
}