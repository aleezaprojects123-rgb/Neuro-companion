using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class DumpAllObjects
    {
        [MenuItem("NeuroCompanion/Dump Scene Hierarchy")]
        public static void Dump()
        {
            using (var sw = new StreamWriter("D:/scene_objects_dump.txt"))
            {
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (go.transform.parent == null)
                    {
                        DumpGo(go, sw, 0);
                    }
                }
            }
            Debug.Log("Dumped scene hierarchy to D:/scene_objects_dump.txt");
        }

        static void DumpGo(GameObject go, StreamWriter sw, int depth)
        {
            string indent = new string(' ', depth * 2);
            string comps = "";
            foreach (var c in go.GetComponents<Component>())
            {
                if (c != null) comps += c.GetType().Name + ", ";
            }
            sw.WriteLine(indent + "- " + go.name + " (active=" + go.activeSelf + ") pos=" + go.transform.position + " [" + comps + "]");
            for (int i = 0; i < go.transform.childCount; i++)
            {
                DumpGo(go.transform.GetChild(i).gameObject, sw, depth + 1);
            }
        }
    }
}
