using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BrokenVector.PersistentComponents
{
    public partial class PersistentComponents
    {
        public void WatchComponent(Component comp)
        {
            if (comp == null || IsComponentWatched(comp))
                return;

            var componentId = GetComponentId(comp);
            if (string.IsNullOrEmpty(componentId))
                return;

            if (!components.ContainsKey(comp.gameObject))
                components[comp.gameObject] = new List<string>();
            components[comp.gameObject].Add(componentId);

            UpdateComponent(comp);

            if (PersistentComponentsWindow.Instance != null)
                PersistentComponentsWindow.Instance.Repaint();

            EditorApplication.RepaintHierarchyWindow();
        }

        public void ForgetComponent(Component comp)
        {
            if (comp == null || !components.ContainsKey(comp.gameObject))
                return;

            var componentId = GetComponentId(comp);
            if (string.IsNullOrEmpty(componentId))
                return;

            components[comp.gameObject].Remove(componentId);
            if (components[comp.gameObject].Count == 0)
                components.Remove(comp.gameObject);

            serializedObjects.Remove(componentId);
            serializedHashes.Remove(componentId);

            if (PersistentComponentsWindow.Instance != null)
                PersistentComponentsWindow.Instance.Repaint();

            EditorApplication.RepaintHierarchyWindow();
        }

        public void WatchComponents(params Component[] comps)
        {
            foreach (var c in comps)
                WatchComponent(c);
        }
        public void ForgetComponents(params Component[] comps)
        {
            foreach (var c in comps)
                ForgetComponent(c);
        }
        public void ForgetEveryComponent()
        {
            List<Component> toForget = new List<Component>();
            foreach (var pair in components)
                foreach (var comp in pair.Value)
                {
                    var component = GetComponentById(comp);
                    if (component != null)
                        toForget.Add(component);
                }

            ForgetComponents(toForget.ToArray());
        }

        public bool IsComponentWatched(Component comp)
        {
            if (comp == null)
                return false;

            var componentId = GetComponentId(comp);
            return !string.IsNullOrEmpty(componentId)
                && components.ContainsKey(comp.gameObject)
                && components[comp.gameObject].Contains(componentId);
        }
    }
}