using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    static class SwitcherMenuItems
    {
        const string k_SwitcherRootMenu = "GameObject/Switcher/";

        [MenuItem(k_SwitcherRootMenu + "Global Switcher", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateGlobalSwitcher(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("Global Switcher", menuCommand.context);
            var volume = go.AddComponent<Switcher>();
            volume.isGlobal = true;
        }

        [MenuItem(k_SwitcherRootMenu + "Box Switcher", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateBoxSwitcher(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("Box Switcher", menuCommand.context);
            var collider = go.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            var volume = go.AddComponent<Switcher>();
            volume.isGlobal = false;
            volume.blendDistance = 1f;
        }

        [MenuItem(k_SwitcherRootMenu + "Sphere Switcher", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateSphereSwitcher(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("Sphere Switcher", menuCommand.context);
            var collider = go.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            var volume = go.AddComponent<Switcher>();
            volume.isGlobal = false;
            volume.blendDistance = 1f;
        }

        [MenuItem(k_SwitcherRootMenu + "Convex Mesh Switcher", priority = CoreUtils.gameObjectMenuPriority)]
        static void CreateConvexMeshSwitcher(MenuCommand menuCommand)
        {
            var go = CoreEditorUtils.CreateGameObject("Convex Mesh Switcher", menuCommand.context);
            var collider = go.AddComponent<MeshCollider>();
            collider.convex = true;
            collider.isTrigger = true;
            var volume = go.AddComponent<Switcher>();
            volume.isGlobal = false;
            volume.blendDistance = 1f;
        }
    }
}