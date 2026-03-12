using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BrokenVector.PersistentComponents
{
    public partial class PersistentComponents
    {
        private PersistencyData persistencyData;

        private void RememberComponents()
        {
            if (Settings.ComponentsStayPersistent)
            {
                List<string> saveData = new List<string>();
                foreach (var pair in components)
                    foreach (var componentId in pair.Value)
                        saveData.Add(componentId);

                persistencyData.persistentComponents = saveData.ToArray();
                persistencyData.Save();
            }
        }
        private void RecallComponents()
        {
#if UNITY_5_4_OR_NEWER
            persistencyData = AssetDatabase.LoadAssetAtPath<PersistencyData>(PersistencyData.GetAssetLocation());
#else
            persistencyData = (PersistencyData) AssetDatabase.LoadAssetAtPath(PersistencyData.GetAssetLocation(), typeof(PersistencyData));
#endif
            if (persistencyData == null)
            {
                persistencyData = PersistencyData.CreateInstance(new string[0]);
            }

            if (persistencyData.persistentComponents == null)
                persistencyData.persistentComponents = new string[0];

            if (Settings.ComponentsStayPersistent)
            {
                foreach (var componentId in persistencyData.persistentComponents)
                {
                    var obj = GetComponentById(componentId);
                    if (obj == null)
                        continue;
                    if (!components.ContainsKey(obj.gameObject))
                        components[obj.gameObject] = new List<string>();

                    if (!components[obj.gameObject].Contains(componentId))
                    {
                        components[obj.gameObject].Add(componentId);
                    }
                }
            }
        }
    }
}