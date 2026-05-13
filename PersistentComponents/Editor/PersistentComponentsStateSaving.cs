using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace BrokenVector.PersistentComponents
{
    public partial class PersistentComponents
    {
        public void WatchComponent(Component comp)
        {
            if (WatchComponentInternal(comp))
            {
                RepaintPersistentComponentViews();
            }
        }

        private bool WatchComponentInternal(Component comp)
        {
            if (comp == null)
                return false;

            var componentId = GetCachedComponentId(comp);
            if (string.IsNullOrEmpty(componentId) || IsComponentWatched(comp, componentId))
                return false;

            if (!components.ContainsKey(comp.gameObject))
                components[comp.gameObject] = new List<string>();
            components[comp.gameObject].Add(componentId);

            SaveComponentSnapshotWithoutHash(comp, componentId);

            return true;
        }

        private void RepaintPersistentComponentViews()
        {
            if (PersistentComponentsWindow.Instance != null)
                PersistentComponentsWindow.Instance.Repaint();

            EditorApplication.RepaintHierarchyWindow();
        }

        public void ForgetComponent(Component comp)
        {
            if (ForgetComponentInternal(comp))
            {
                RepaintPersistentComponentViews();
            }
        }

        private bool ForgetComponentInternal(Component comp)
        {
            if (comp == null)
                return false;

            var componentId = GetCachedComponentId(comp);
            if (string.IsNullOrEmpty(componentId) || !components.ContainsKey(comp.gameObject))
                return false;

            components[comp.gameObject].Remove(componentId);
            if (components[comp.gameObject].Count == 0)
                components.Remove(comp.gameObject);

            serializedObjects.Remove(componentId);
            serializedHashes.Remove(componentId);
            ClearCachedComponentId(comp);

            return true;
        }

        public void WatchComponents(params Component[] comps)
        {
            bool changed = false;
            foreach (var c in comps)
                changed |= WatchComponentInternal(c);

            if (changed)
                RepaintPersistentComponentViews();
        }
        public void ForgetComponents(params Component[] comps)
        {
            bool changed = false;
            foreach (var c in comps)
                changed |= ForgetComponentInternal(c);

            if (changed)
                RepaintPersistentComponentViews();
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

            var componentId = GetCachedComponentId(comp);
            return IsComponentWatched(comp, componentId);
        }
    }
}
