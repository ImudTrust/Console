using BepInEx;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Console
{
    [BepInIncompatibility("org.iidk.gorillatag.iimenu")] // Does not work together
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        // Put this snippet of code in your BaseUnityPlugin
        void Start() => Console.LoadConsole();
    }
}
