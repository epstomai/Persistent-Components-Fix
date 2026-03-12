using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BrokenVector.PersistentComponents
{

    public partial class PersistentComponents
    {
        private static PersistentComponents instance;
        public static PersistentComponents Instance
        {
            get
            {
                if (instance == null)
                    instance = new PersistentComponents();
                return instance;
            }
        }

        public Dictionary<GameObject, List<string>> WatchedComponents { get { return components; } }

        private Dictionary<GameObject, List<string>> components = new Dictionary<GameObject, List<string>>();
        private Dictionary<string, SerializedObject> serializedObjects = new Dictionary<string, SerializedObject>();
        private Dictionary<string, string> serializedHashes = new Dictionary<string, string>();
        private double nextPollingTime;

        private const double POLLING_INTERVAL = 0.25d;

        public PersistentComponents()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyItemCallback;
            EditorApplication.update += OnEditorUpdate;

            RecallComponents();
        }

        public void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                UpdateAllComponents();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                OnExitPlayMode();
            }

            RememberComponents();
        }

        private void OnExitPlayMode()
        {
            Instance.ApplyModifiedProperties();
        }

        public void ApplyModifiedProperties()
        {
            List<string> toRemove = new List<string>();

            foreach (var pair in serializedObjects)
            {
                if (pair.Value != null && pair.Value.targetObject != null)
                {
                    var targetObject = pair.Value.targetObject;
                    var targetComponent = targetObject as Component;
                    var targetSerializedObject = new SerializedObject(targetObject);
                    var snapshotIterator = pair.Value.GetIterator();

                    while (snapshotIterator.NextVisible(true))
                    {
                        var targetProperty = targetSerializedObject.FindProperty(snapshotIterator.propertyPath);
                        if (targetProperty == null)
                            continue;

                        targetSerializedObject.CopyFromSerializedProperty(snapshotIterator);
                    }

                    targetSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                    targetSerializedObject.UpdateIfRequiredOrScript();

                    if (targetComponent != null)
                    {
                        EditorUtility.SetDirty(targetComponent);
                    }
                }
                else
                {
                    toRemove.Add(pair.Key);
                }
            }
            foreach(var k in toRemove)
            {
                serializedObjects.Remove(k);
            }
        }

        public void UpdateComponent(Component comp)
        {
            if (!IsComponentWatched(comp))
                return;

            var objectId = GetComponentId(comp);
            if (string.IsNullOrEmpty(objectId))
                return;

            if (!serializedObjects.ContainsKey(objectId))
            {
                serializedObjects.Add(objectId, new SerializedObject(comp));
            }

            var clone = new SerializedObject(comp);
            var original = serializedObjects[objectId];

            SerializedProperty sp = clone.GetIterator();
            while (sp.NextVisible(true))
            {
                original.CopyFromSerializedProperty(sp);
            }
            sp.Reset();

            serializedHashes[objectId] = BuildSerializedHash(clone);
        }

        private static string GetPropertyValueForHash(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return property.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return property.floatValue.ToString();
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? property.objectReferenceValue.GetInstanceID().ToString() : "null";
                case SerializedPropertyType.Vector2:
                    return property.vector2Value.ToString();
                case SerializedPropertyType.Vector3:
                    return property.vector3Value.ToString();
                case SerializedPropertyType.Vector4:
                    return property.vector4Value.ToString();
                case SerializedPropertyType.Color:
                    return property.colorValue.ToString();
                case SerializedPropertyType.Rect:
                    return property.rectValue.ToString();
                case SerializedPropertyType.Bounds:
                    return property.boundsValue.ToString();
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue.eulerAngles.ToString();
                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue != null ? property.animationCurveValue.length.ToString() : "null";
                case SerializedPropertyType.ExposedReference:
                    return property.exposedReferenceValue != null ? property.exposedReferenceValue.GetInstanceID().ToString() : "null";
                case SerializedPropertyType.Vector2Int:
                    return property.vector2IntValue.ToString();
                case SerializedPropertyType.Vector3Int:
                    return property.vector3IntValue.ToString();
                case SerializedPropertyType.RectInt:
                    return property.rectIntValue.ToString();
                case SerializedPropertyType.BoundsInt:
                    return property.boundsIntValue.ToString();
                case SerializedPropertyType.Hash128:
                    return property.hash128Value.ToString();
                default:
                    return property.propertyType.ToString();
            }
        }
        public void UpdateComponents(params Component[] comps)
        {
            foreach (var c in comps)
                UpdateComponent(c);
        }
        public void UpdateAllComponents()
        {
            foreach (var go in components)
                foreach(var componentId in go.Value)
                {
                    UpdateComponent(GetComponentById(componentId));
                }
        }

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
                return;

            if (EditorApplication.timeSinceStartup < nextPollingTime)
                return;

            nextPollingTime = EditorApplication.timeSinceStartup + POLLING_INTERVAL;
            PollWatchedComponents();
        }

        private void PollWatchedComponents()
        {
            foreach (var go in components)
            {
                foreach (var componentId in go.Value)
                {
                    var component = GetComponentById(componentId);
                    if (component == null)
                        continue;

                    var currentSerializedObject = new SerializedObject(component);
                    var currentHash = BuildSerializedHash(currentSerializedObject);

                    if (!serializedHashes.TryGetValue(componentId, out var previousHash))
                    {
                        serializedHashes[componentId] = currentHash;
                        continue;
                    }

                    if (currentHash != previousHash)
                    {
                        UpdateComponent(component);
                    }
                }
            }
        }

        private static string BuildSerializedHash(SerializedObject serializedObject)
        {
            if (serializedObject == null)
                return string.Empty;

            var iterator = serializedObject.GetIterator();
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyPath == "m_Script")
                    continue;

                builder.Append(iterator.propertyPath);
                builder.Append('=');
                builder.Append(GetPropertyValueForHash(iterator));
                builder.Append(';');
            }

            return builder.ToString();
        }

        internal static string GetComponentId(Component comp)
        {
            if (comp == null)
                return null;

            return GlobalObjectId.GetGlobalObjectIdSlow(comp).ToString();
        }

        internal static Component GetComponentById(string componentId)
        {
            if (string.IsNullOrEmpty(componentId))
                return null;

            if (!GlobalObjectId.TryParse(componentId, out var globalObjectId))
                return null;

            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as Component;
        }

        private static void HierarchyItemCallback(int instanceID, Rect selectionRect)
        {
            bool persistent = false;
            Transform objTransform = null;
            foreach(var pair in Instance.components)
            {
                if (pair.Key == null)
                    continue;

                if(pair.Key.GetInstanceID() == instanceID)
                {
                    persistent = true;
                    objTransform = pair.Key.transform;
                    break;
                }
            }
            if (!persistent || objTransform == null)
                return;

            int numParents = 0;
            while(objTransform.parent != null)
            {
                numParents++;
                objTransform = objTransform.parent;
            }

            Rect r = new Rect(selectionRect);
            r.x = selectionRect.x - numParents * 14 - 25;
            r.width = 18;

            GUI.Label(r, "P");
        }

    }

}
