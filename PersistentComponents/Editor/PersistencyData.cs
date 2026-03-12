using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BrokenVector.PersistentComponents
{
    public class PersistencyData : ScriptableObject
    {
        public string[] persistentComponents;

        public static PersistencyData CreateInstance(string[] c)
        {
            var instance = ScriptableObject.CreateInstance<PersistencyData>();
            instance.persistentComponents = c;

            AssetDatabase.CreateAsset(instance, GetAssetLocation());
            AssetDatabase.SaveAssets();

            return instance;
        }

        public void Save()
        {
            AssetDatabase.SaveAssets();
        }

        public static string GetAssetLocation()
        {
            var matches = AssetDatabase.FindAssets("PersistencyData");
            if (matches == null || matches.Length == 0)
                return "Assets/plugins/PersistentComponents/Editor/" + Constants.ASSET_SAVEDATA_NAME;

            var myPath = AssetDatabase.GUIDToAssetPath(matches[0]);
            if (string.IsNullOrEmpty(myPath) || myPath.Length < 19)
                return "Assets/plugins/PersistentComponents/Editor/" + Constants.ASSET_SAVEDATA_NAME;

            myPath = myPath.Remove(myPath.Length - 19);
            return myPath + "/" + Constants.ASSET_SAVEDATA_NAME;
        }

    }
}
