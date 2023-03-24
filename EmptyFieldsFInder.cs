
using UnityEngine;

using System.Collections.Generic;
using System.Linq;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
public class EmptyFieldsFinder : EditorWindow
{
    private Dictionary<GameObject, EmptyFieldsData> emptyFields = new Dictionary<GameObject, EmptyFieldsData>();
    private Vector2 scrollPos;
    private string selectedObjectType = "All";

    private List<string> _exceptionsList = new List<string>();

    private bool _isMissingReferenceMode = false;

    [MenuItem("Tools/Find Empty Fields")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(EmptyFieldsFinder));
    }

    void OnGUI()
    {
        List<string> objectTypes = new List<string> { "All", "GameObject", "Transform", "MeshRenderer", "AudioSource", "Sprite", "Image", "Material", "RawImage", "Text" };
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search:");
        searchText = GUILayout.TextField(searchText);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Object Type:");
        selectedObjectType = objectTypes[EditorGUILayout.Popup(objectTypes.IndexOf(selectedObjectType), objectTypes.ToArray())];

        if (GUILayout.Button("Find only missing references"))
        {
            FindMissingReferences();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Load exception List"))
        {
            LoadExceptionList();
        }

        if (GUILayout.Button("Unload exception List"))
        {
            UnloadExceptionList();
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Find Empty Fields"))
        {
            FindEmptyFields();
        }

        if (GUILayout.Button("Save Exception List"))
        {
            SaveExceptionList();
        }
        if (emptyFields.Count > 0)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Game Objects with Empty Fields", EditorStyles.boldLabel);
            foreach (KeyValuePair<GameObject, EmptyFieldsData> pair in emptyFields.OrderBy(pair => pair.Key.name))
            {
                EditorGUILayout.BeginHorizontal();

                pair.Value.ShowFields = EditorGUILayout.ToggleLeft("Show Child Fields", pair.Value.ShowFields);
                EditorGUILayout.ObjectField(pair.Key, typeof(GameObject), true);
                if (GUILayout.Button("Add exception"))
                {
                    AppendToExceptionFile(pair.Key.name);
                }

                EditorGUILayout.EndHorizontal();
                if (pair.Value.ShowFields)
                {
                    EditorGUI.indentLevel++;
                    foreach (Component component in pair.Value.Components)
                    {
                        if (selectedObjectType != "All" && pair.Key.GetComponent(selectedObjectType) == null)
                        {
                            continue;
                        }
                        EditorGUILayout.ObjectField(component, typeof(Component), true);
                        SerializedObject serializedObject = new SerializedObject(component);
                        SerializedProperty prop = serializedObject.GetIterator();
                        while (prop.NextVisible(true))
                        {
                            if ((prop.propertyType == SerializedPropertyType.ObjectReference) &&
                                ((prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0) || prop.objectReferenceValue == null))
                            {
                                EditorGUI.BeginChangeCheck();
                                EditorGUILayout.PropertyField(prop, true);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    serializedObject.ApplyModifiedProperties();
                                    EditorUtility.SetDirty(component);
                                }
                            }
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("No Empty Fields Found", EditorStyles.boldLabel);
        }
    }

    private string searchText = "";

    private void FindEmptyFields(bool isOnlyMissing = false)
    {
        emptyFields.Clear();
        _isMissingReferenceMode = isOnlyMissing;

        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            if (_exceptionsList.Contains(obj.name)) continue;

            if (!string.IsNullOrEmpty(searchText) && !obj.name.Contains(searchText))
            {
                continue;
            }

            Component[] components = obj.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (selectedObjectType != "All" && obj.GetComponent(selectedObjectType) == null)
                {
                    continue;
                }
                SerializedObject serializedObject = new SerializedObject(component);
                SerializedProperty prop = serializedObject.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (CheckForNoneAndMissing(prop, isOnlyMissing))
                    {
                        if (!emptyFields.ContainsKey(obj))
                        {
                            emptyFields[obj] = new EmptyFieldsData();
                        }
                        emptyFields[obj].Components.Add(component);
                        break;
                    }
                }
            }
        }
        Repaint();
    }

    private void FindMissingReferences()
    {
        FindEmptyFields(true);
    }

    private bool CheckForNoneAndMissing(SerializedProperty prop, bool isOnlyMissing = false)
    {
        if ((prop.propertyType == SerializedPropertyType.ObjectReference) &&
            ((prop.objectReferenceValue == null && prop.objectReferenceInstanceIDValue != 0) || !isOnlyMissing && prop.objectReferenceValue == null))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    private void SaveExceptionList()
    {
        string filePath = $"{Application.dataPath}/StreamingAssets/Exceptions/Exceptions.txt";

        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            foreach (GameObject obj in emptyFields.Keys)
            {
                writer.WriteLine(obj.name);
            }
        }
        LoadExceptionList();
        if (_isMissingReferenceMode)
        {
            FindMissingReferences();
            return;
        }

        FindEmptyFields();
    }
    private void AppendToExceptionFile(string objectName)
    {
        if (_exceptionsList.Contains(objectName))
            return;

        _exceptionsList.Add(objectName);
        string filePath = $"{Application.dataPath}/StreamingAssets/Exceptions/Exceptions.txt";

        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            writer.WriteLine(objectName);
        }
        if (_isMissingReferenceMode)
        {
            FindMissingReferences();
            return;
        }

        FindEmptyFields();
    }
    private void LoadExceptionList()
    {
        _exceptionsList.Clear();
        string filePath = $"{Application.dataPath}/StreamingAssets/Exceptions/Exceptions.txt";

        if (File.Exists(filePath))
        {
            using (StreamReader sr = new StreamReader(filePath))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    _exceptionsList.Add(line);
                }
            }
        }
        else
        {
            Debug.LogError("Could not find exceptions file at path: " + filePath);
        }
    }
    private void UnloadExceptionList()
    {
        _exceptionsList.Clear();
    }
}
#endif

class EmptyFieldsData
{
    public List<Component> Components = new List<Component>();
    public bool ShowFields = false;
}
