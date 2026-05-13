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
        private Dictionary<string, int> serializedHashes = new Dictionary<string, int>();
        private Dictionary<int, string> componentIdCache = new Dictionary<int, string>();
        private double nextPollingTime;

        private const double POLLING_INTERVAL = 1.0d;

        public PersistentComponents()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.hierarchyWindowItemOnGUI += HierarchyItemCallback;
            EditorApplication.update += OnEditorUpdate;

            RecallComponents();
        }

        public void OnPlayModeChanged(PlayModeStateChange state)
        {
            componentIdCache.Clear();

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                UpdateAllComponents(false);
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                UpdateAllComponents(false);
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
                serializedHashes.Remove(k);
            }
        }

        public void UpdateComponent(Component comp)
        {
            if (comp == null)
                return;

            var objectId = GetCachedComponentId(comp);
            if (string.IsNullOrEmpty(objectId) || !IsComponentWatched(comp, objectId))
                return;

            SaveComponentSnapshot(comp, objectId, null, 0, false);
        }

        private void SaveComponentSnapshot(Component comp, string objectId, SerializedObject sourceSerializedObject, int currentHash, bool hasCurrentHash)
        {
            if (comp == null || string.IsNullOrEmpty(objectId))
                return;

            var source = sourceSerializedObject ?? new SerializedObject(comp);
            if (!serializedObjects.TryGetValue(objectId, out var snapshot) || snapshot == null || snapshot.targetObject == null)
            {
                serializedObjects[objectId] = source;
            }
            else
            {
                CopySerializedProperties(source, snapshot);
            }

            serializedHashes[objectId] = hasCurrentHash ? currentHash : BuildSerializedHash(source);
        }

        private void SaveComponentSnapshotWithoutHash(Component comp, string objectId)
        {
            if (comp == null || string.IsNullOrEmpty(objectId))
                return;

            serializedObjects[objectId] = new SerializedObject(comp);
            serializedHashes.Remove(objectId);
        }

        private static void CopySerializedProperties(SerializedObject source, SerializedObject target)
        {
            SerializedProperty sp = source.GetIterator();
            while (sp.NextVisible(true))
            {
                target.CopyFromSerializedProperty(sp);
            }
            sp.Reset();
        }

        private static int GetPropertyValueHash(SerializedProperty property)
        {
            unchecked
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        return property.intValue;
                    case SerializedPropertyType.Boolean:
                        return property.boolValue ? 1 : 0;
                    case SerializedPropertyType.Float:
                        return property.floatValue.GetHashCode();
                    case SerializedPropertyType.String:
                        return property.stringValue != null ? property.stringValue.GetHashCode() : 0;
                    case SerializedPropertyType.Enum:
                        return property.enumValueIndex;
                    case SerializedPropertyType.ObjectReference:
                        return property.objectReferenceValue != null ? property.objectReferenceValue.GetInstanceID() : 0;
                    case SerializedPropertyType.Vector2:
                        return property.vector2Value.GetHashCode();
                    case SerializedPropertyType.Vector3:
                        return property.vector3Value.GetHashCode();
                    case SerializedPropertyType.Vector4:
                        return property.vector4Value.GetHashCode();
                    case SerializedPropertyType.Color:
                        return property.colorValue.GetHashCode();
                    case SerializedPropertyType.Rect:
                        return property.rectValue.GetHashCode();
                    case SerializedPropertyType.Bounds:
                        return property.boundsValue.GetHashCode();
                    case SerializedPropertyType.Quaternion:
                        return property.quaternionValue.eulerAngles.GetHashCode();
                    case SerializedPropertyType.AnimationCurve:
                        return property.animationCurveValue != null ? property.animationCurveValue.length : 0;
                    case SerializedPropertyType.ExposedReference:
                        return property.exposedReferenceValue != null ? property.exposedReferenceValue.GetInstanceID() : 0;
                    case SerializedPropertyType.Vector2Int:
                        return property.vector2IntValue.GetHashCode();
                    case SerializedPropertyType.Vector3Int:
                        return property.vector3IntValue.GetHashCode();
                    case SerializedPropertyType.RectInt:
                        return property.rectIntValue.GetHashCode();
                    case SerializedPropertyType.BoundsInt:
                        return property.boundsIntValue.GetHashCode();
                    case SerializedPropertyType.Hash128:
                        return property.hash128Value.GetHashCode();
                    default:
                        return (int)property.propertyType;
                }
            }
        }

        private string GetCachedComponentId(Component comp)
        {
            if (comp == null)
                return null;

            int instanceId = comp.GetInstanceID();
            if (componentIdCache.TryGetValue(instanceId, out var componentId))
                return componentId;

            componentId = GetComponentId(comp);
            if (!string.IsNullOrEmpty(componentId))
            {
                componentIdCache[instanceId] = componentId;
            }

            return componentId;
        }

        private void ClearCachedComponentId(Component comp)
        {
            if (comp == null)
                return;

            componentIdCache.Remove(comp.GetInstanceID());
        }

        private bool IsComponentWatched(Component comp, string componentId)
        {
            return comp != null
                && !string.IsNullOrEmpty(componentId)
                && components.ContainsKey(comp.gameObject)
                && components[comp.gameObject].Contains(componentId);
        }
        public void UpdateComponents(params Component[] comps)
        {
            foreach (var c in comps)
                UpdateComponent(c);
        }
        public void UpdateAllComponents()
        {
            UpdateAllComponents(true);
        }

        private void UpdateAllComponents(bool updateHash)
        {
            foreach (var go in components)
                foreach(var componentId in go.Value)
                {
                    var component = GetComponentById(componentId);
                    if (component == null)
                        continue;

                    if (updateHash)
                    {
                        UpdateComponent(component);
                    }
                    else
                    {
                        SaveComponentSnapshotWithoutHash(component, componentId);
                    }
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

                    if (!serializedHashes.TryGetValue(componentId, out var previousHash))
                        continue;

                    var currentSerializedObject = new SerializedObject(component);
                    var currentHash = BuildSerializedHash(currentSerializedObject);

                    if (currentHash != previousHash)
                    {
                        SaveComponentSnapshot(component, componentId, currentSerializedObject, currentHash, true);
                    }
                }
            }
        }

        private static int BuildSerializedHash(SerializedObject serializedObject)
        {
            if (serializedObject == null)
                return 0;

            var iterator = serializedObject.GetIterator();
            unchecked
            {
                int hash = 17;
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyPath == "m_Script")
                        continue;

                    hash = hash * 31 + iterator.propertyPath.GetHashCode();
                    hash = hash * 31 + GetPropertyValueHash(iterator);
                }

                return hash;
            }
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
