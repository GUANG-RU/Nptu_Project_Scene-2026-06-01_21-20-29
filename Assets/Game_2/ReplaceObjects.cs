#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class ReplaceObjects : EditorWindow
{
    private GameObject newPrefab;

    [MenuItem("Tools/批量替換物件 (Replace Objects)")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceObjects>("Replace Objects");
    }

    private void OnGUI()
    {
        GUILayout.Label("批量替換場景物件工具", EditorStyles.boldLabel);
        newPrefab = (GameObject)EditorGUILayout.ObjectField("新 Prefab 素材", newPrefab, typeof(GameObject), false);

        if (GUILayout.Button("替換目前選取的物件"))
        {
            if (newPrefab == null)
            {
                Debug.LogError("請先拖入要替換的新 Prefab！");
                return;
            }

            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.LogWarning("未選取任何場景物件！");
                return;
            }

            foreach (GameObject go in selectedObjects)
            {
                // 實例化新的 Prefab 並保持連結
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, go.scene);
                
                // 複製 Transform 與 Hierarchy 結構
                newObj.transform.SetParent(go.transform.parent);
                newObj.transform.position = go.transform.position;
                newObj.transform.rotation = go.transform.rotation;
                newObj.transform.localScale = go.transform.localScale;

                // 註冊 Undo 步驟並刪除舊物件
                Undo.RegisterCreatedObjectUndo(newObj, "Replace Object");
                Undo.DestroyObjectImmediate(go);
            }
        }
    }
}
#endif