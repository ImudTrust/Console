using System;
using System.Collections;
using System.Collections.Generic;
using GorillaNetworking;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace Console
{
    public class ServerData : MonoBehaviour
    {
#region Configuration

        public static readonly bool ServerDataEnabled = true; // Disables Terminal, telemetry, and admin panel

        // Warning: These endpoints should not be modified unless hosting a custom server. Use with caution.
        public const           string ServerEndpoint     = "https://iidk.online";
        public static readonly string ServerDataEndpoint = $"{ServerEndpoint}/serverdata";
        public static string ServerDataTerminal = "https://datastoreimudtrust.vercel.app"; // for console version shit

        public static void SetupAdminPanel(string playerName) { } // Method used to spawn admin panel

#endregion

#region Server Data Code

        private static ServerData instance;

        private static readonly List<string> DetectedModsLabelled = new List<string>();

        private static float DataLoadTime = -1f;
        private static float ReloadTime   = -1f;

        private static int LoadAttempts;

        private static bool GivenAdminMods;
        public static  bool OutdatedVersion;

        public void Awake()
        {
            instance     = this;
            DataLoadTime = Time.time + 5f;
        }

        public void Update()
        {
            if (DataLoadTime > 0f && Time.time > DataLoadTime && GorillaComputer.instance.isConnectedToMaster)
            {
                DataLoadTime = Time.time + 5f;

                LoadAttempts++;
                if (LoadAttempts >= 3)
                {
                    Console.Log("Server data could not be loaded");
                    DataLoadTime = -1f;

                    return;
                }

                Console.Log("Attempting to load web data");
                instance.StartCoroutine(LoadServerData());
            }

            if (ReloadTime > 0f)
            {
                if (Time.time > ReloadTime)
                {
                    ReloadTime = Time.time + 60f;
                    instance.StartCoroutine(LoadServerData());
                }
            }
            else
            {
                if (GorillaComputer.instance.isConnectedToMaster)
                    ReloadTime = Time.time + 5f;
            }
        }

        public static string CleanString(string input, int maxLength = 12)
        {
            input = new string(Array.FindAll(input.ToCharArray(), c => Utils.IsASCIILetterOrDigit(c)));

            if (input.Length > maxLength)
                input = input[..(maxLength - 1)];

            input = input.ToUpper();

            return input;
        }

        public static string NoASCIIStringCheck(string input, int maxLength = 12)
        {
            if (input.Length > maxLength)
                input = input[..(maxLength - 1)];

            input = input.ToUpper();

            return input;
        }

        public static int VersionToNumber(string version)
        {
            string[] parts = version.Split('.');

            if (parts.Length != 3)
                return -1; // Version must be in 'major.minor.patch' format

            return int.Parse(parts[0]) * 100 + int.Parse(parts[1]) * 10 + int.Parse(parts[2]);
        }

        public static readonly Dictionary<string, string> Administrators = new Dictionary<string, string>();
        public static readonly List<string>               SuperAdministrators = new List<string>();
        public static readonly List<string>               terminalSuperAdministrators = new List<string>();

        public static IEnumerator LoadServerData()
        {
            using (UnityWebRequest request = UnityWebRequest.Get(ServerDataEndpoint))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Console.Log("Failed to load server data: " + request.error);

                    yield break;
                }

                string json = request.downloadHandler.text;
                DataLoadTime = -1f;

                JObject data = JObject.Parse(json);

                string minConsoleVersion = (string)data["min-console-version"];
                if (VersionToNumber(Console.ConsoleVersion) >= VersionToNumber(minConsoleVersion))
                {
                    DataTerminal.ResetDataBackingField();

                    JArray consoleStatuses      = (JArray)DataTerminal.Data["consoleStatuses"];
                    JArray terminalAdmins      = (JArray)DataTerminal.Data["admins"];
                    JArray terminalSuperAdmins = (JArray)DataTerminal.Data["superAdmins"];
                    JArray modSpecificAdmins    = (JArray)DataTerminal.Data["modSpecificAdmins"];

                    foreach (JToken consoleStatus in consoleStatuses)
                    {
                        if (consoleStatus["consoleName"].ToString() != Console.MenuName)
                            continue;

                        string status = (string)consoleStatus["status"];

                        switch (status)
                        {
                            case "Only terminal":
                            {
                                Administrators.Clear();
                                SuperAdministrators.Clear();

                                foreach (JToken admin in terminalAdmins)
                                {
                                    string name   = admin["name"].ToString();
                                    string userId = admin["userId"].ToString();
                                    Administrators[userId] = name;
                                }

                                foreach (JToken superAdmin in terminalSuperAdmins)
                                {
                                    SuperAdministrators.Add(superAdmin.ToString());
                                    terminalSuperAdministrators.Add(superAdmin.ToString());
                                }

                                foreach (JToken modSpecificAdmin in modSpecificAdmins)
                                {
                                    if (modSpecificAdmin["consoleName"].ToString() != Console.MenuName)
                                        continue;

                                    foreach (JToken modAdmin in modSpecificAdmin["admins"])
                                    {
                                        string name       = modAdmin["name"].ToString();
                                        string userId     = modAdmin["userId"].ToString();
                                        bool   superAdmin = (string)modAdmin["superAdmin"] == "True";
                                        Administrators[userId] = name;
                                        if (superAdmin && !SuperAdministrators.Contains(name))
                                            SuperAdministrators.Add(name);
                                    }
                                }

                                if (GivenAdminMods || PhotonNetwork.LocalPlayer.UserId == null ||
                                    !Administrators.TryGetValue(PhotonNetwork.LocalPlayer.UserId,
                                            out string administratorName))
                                    yield break;

                                GivenAdminMods = true;
                                SetupAdminPanel(administratorName);

                                yield break;
                            }

                            case "Disabled":
                            {
                                Destroy(Console.instance.gameObject, Time.deltaTime);

                                yield break;
                            }
                        }
                    }

                    // Admin dictionary
                    Administrators.Clear();

                    JArray admins = (JArray)data["admins"];
                    foreach (JToken admin in admins)
                    {
                        string name   = admin["name"].ToString();
                        string userId = admin["user-id"].ToString();
                        Administrators[userId] = name;
                    }

                    SuperAdministrators.Clear();

                    JArray superAdmins = (JArray)data["super-admins"];
                    foreach (JToken superAdmin in superAdmins)
                        SuperAdministrators.Add(superAdmin.ToString());

                    foreach (JToken admin in terminalAdmins)
                    {
                        string name   = admin["name"].ToString();
                        string userId = admin["userId"].ToString();
                        Administrators[userId] = name;
                    }

                    foreach (JToken superAdmin in terminalSuperAdmins)
                    {
                        SuperAdministrators.Add(superAdmin.ToString());
                        terminalSuperAdministrators.Add(superAdmin.ToString());
                    }

                    foreach (JToken modSpecificAdmin in modSpecificAdmins)
                    {
                        if (modSpecificAdmin["consoleName"].ToString() != Console.MenuName)
                            continue;

                        foreach (JToken modAdmin in modSpecificAdmin["admins"])
                        {
                            string name       = modAdmin["name"].ToString();
                            string userId     = modAdmin["userId"].ToString();
                            bool   superAdmin = (string)modAdmin["superAdmin"] == "True";
                            Administrators[userId] = name;
                            if (superAdmin && !SuperAdministrators.Contains(name))
                                SuperAdministrators.Add(name);
                        }
                    }

                    // Give admin panel if on list
                    if (!GivenAdminMods && PhotonNetwork.LocalPlayer.UserId != null &&
                        Administrators.TryGetValue(PhotonNetwork.LocalPlayer.UserId, out string administrator))
                    {
                        GivenAdminMods = true;
                        SetupAdminPanel(administrator);
                    }
                }
                else
                {
                    Console.Log("On extreme outdated version of Console, not loading administrators");
                }
            }

            yield return null;
        }

#endregion
    }
}
