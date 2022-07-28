using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class EditorCodeHelpers
{
    enum ECreationMode
    {
        ChildClass,
        CustomInspector
    }

    class EndNameEditHandler : UnityEditor.ProjectWindowCallback.EndNameEditAction
    {
        ECreationMode Mode;
        string ReferenceFilePath;
        System.Type ReferenceType;

        public void Configure(ECreationMode mode, string referenceFilePath, System.Type referenceType)
        {
            Mode = mode;
            ReferenceFilePath = referenceFilePath;
            ReferenceType = referenceType;
        }

        string TemplateFileForMode(ECreationMode mode) => mode switch
        {
            ECreationMode.ChildClass => "CodeTemplate_ChildClass",
            ECreationMode.CustomInspector => "CodeTemplate_CustomInspector",
            _ => throw new System.ArgumentOutOfRangeException($"Unknown mode {mode}")
        };

        string GetTemplateForCurrentMode()
        {
            string[] templateGUIDs = AssetDatabase.FindAssets($"{TemplateFileForMode(Mode)} t:TextAsset");

            if (templateGUIDs.Length != 1)
            {
                Debug.LogError($"Found {templateGUIDs.Length} templates for {Mode}. Expected 1");
                return string.Empty;
            }

            return File.ReadAllText(AssetDatabase.GUIDToAssetPath(templateGUIDs[0]));
        }

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string newFileContent = GetTemplateForCurrentMode();

            if (newFileContent.Length == 0)
            {
                Debug.Log($"Failed to find valid template or template was empty");
                return;
            }

            var newClassName = Path.GetFileNameWithoutExtension(pathName);

            // update the template
            newFileContent = newFileContent.Replace("#CLASSNAME#", newClassName);
            newFileContent = newFileContent.Replace("#PARENTCLASSNAME#", ReferenceType.Name);

            // is this an editor class?
            if (Mode == ECreationMode.CustomInspector)
            {
                var pathElements = pathName.Split(Path.DirectorySeparatorChar);

                // are we in an editor folder already
                bool isInEditorFolder = false;
                foreach(var element in pathElements)
                {
                    if (element.ToLower() == "editor")
                    {
                        isInEditorFolder = true;
                        break;
                    }
                }

                // not in editor folder
                if (!isInEditorFolder)
                {
                    var basePath = Path.GetDirectoryName(pathName);
                    var fileName = Path.GetFileName(pathName);

                    var newPath = Path.Combine(basePath, "Editor");
                    Directory.CreateDirectory(newPath);

                    pathName = Path.Combine(newPath, fileName);
                }
            }

            File.WriteAllText(pathName, newFileContent);

            // update the asset database
            AssetDatabase.ImportAsset(pathName);
            var newScript = AssetDatabase.LoadAssetAtPath<MonoScript>(pathName);
            ProjectWindowUtil.ShowCreatedAsset(newScript);
        }
    }

    [MenuItem("Assets/Code Helpers/Create Child Class")]
    private static void AddChildClass()
    {
        PerformCreation(ECreationMode.ChildClass, "#REFCLASSNAME#Child");
    }

    [MenuItem("Assets/Code Helpers/Create Child Class", true)]
    private static bool AddChildClassValidation()
    {
        return Selection.activeObject && Selection.activeObject is MonoScript && Selection.assetGUIDs.Length == 1;
    }

    [MenuItem("Assets/Code Helpers/Create Custom Inspector")]
    private static void AddCustomInspector()
    {
        PerformCreation(ECreationMode.CustomInspector, "#REFCLASSNAME#Editor");
    }

    [MenuItem("Assets/Code Helpers/Create Custom Inspector", true)]
    private static bool AddCustomInspectorValidation()
    {
        return Selection.activeObject && Selection.activeObject is MonoScript && Selection.assetGUIDs.Length == 1;
    }

    private static void PerformCreation(ECreationMode mode, string initialFilename)
    {
        var assetPath = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
        var scriptAsset = Selection.activeObject as MonoScript;
        var filename = initialFilename.Replace("#REFCLASSNAME#", scriptAsset.GetClass().Name);

        var endNameEditHandler = ScriptableObject.CreateInstance<EndNameEditHandler>();
        endNameEditHandler.Configure(mode, assetPath, scriptAsset.GetClass());

        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, endNameEditHandler, $"{filename}.cs", null, null);
    }
}
