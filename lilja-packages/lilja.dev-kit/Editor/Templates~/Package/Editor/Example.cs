using UnityEngine;
using UnityEditor;

namespace #DISPLAY_NAME#.Editor
{
    public static class Example
    {
        [MenuItem("Lilja/Example Code (#DISPLAY_NAME#)")]
        private static void Menu()
        {
             Debug.Log("Hello from #DISPLAY_NAME# Editor!");
        }
    }
}
