using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace RockMeterYARG
{
    [HarmonyPatch(typeName:"YARG.Gameplay.Player.FiveFretPlayer", methodName: "OnNoteMissed")]
    public class FiveFretMissNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.DrainHealth_FiveFret();
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Guitar.GuitarEngine", methodName: "Overstrum")]
    public class FiveFretOverstrumHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.DrainHealth_FiveFret();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.Player.FiveFretPlayer", methodName: "OnNoteHit")]
    public class FiveFretHitNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.AddHealth_FiveFret();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.GameManager", methodName: "CreatePlayers")]
    public class OnGameLoad
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.InitHealthMeter_FiveFret();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.GameManager", methodName: "Resume")]
    public class OnUnpause
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.UnpauseHandler();
        }
    }
    [HarmonyPatch(typeName: "YARG.Menu.Persistent.DialogManager", methodName: "ClearDialog")]
    public class OnDialogClose
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.FailConfirmHandler();
        }
    }
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    public class RockMeterYARG : BaseUnityPlugin
    {
        public static RockMeterYARG Instance { get; private set; }
        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log = new ManualLogSource(PluginName);
        public RockMeterYARG()
        {
            Instance = this;
        }

        private const string MyGUID = "com.yoshibyl.RockMeterYARG";
        private const string PluginName = "RockMeterYARG";
        private const string VersionString = "0.1.0";

        public string LogMsg(object obj)
        {
#if DEBUG
            UnityEngine.Debug.Log(obj);
#endif
            return obj.ToString();
        }

        // Assemblies
        Assembly yargMainAsm;   // `Assembly-CSharp.dll` for reflection
        Assembly yargCoreAsm;   // `YARG.Core.dll` just in case

        public List<string> logs;
        public bool didLoad;
        public bool practice;
        public bool replay;
        public GameObject gmo;      // Game Manager object
        public GameObject tmo;      // Toast Manager object
        public GameObject dmo;      // Dialog Manager object
        public object currentDialog;
        public GameObject pmm;
        public float timerDebugText;
        public float timerGameInit;

        public bool inGame;
        public bool restartOnFail;
        public bool songFailed;
        public double defaultHealth = 0.5;
        public double missDrainAmount;  // Amount to take from health on note miss or overstrum
        public double hitHealthAmount;  // Amount to add to health on note hit usually
        public double hitSPGainAmount;  // Amount to add to health on note hit during Star Power
        public double currentHealth;
        
        public YARG.Core.Engine.BaseStats playerStats;
        public int score;
        public int ghosts;
        public int notesHit;
        public int notesTotal;
        public int notesMissed;
        public int overstrums;

        public GameObject dbgTxtObj;
        public TextMeshProUGUI dbgTxtTMP;

        public Type gmType;         // Game manager
        public Type basePlayerType; // Base Player
        public Type tmType;         // Toast notification manager
        public Type pmmType;        // Pause menu manager (for restarting song) (soon™)
        public Type dmType;         // Dialog manager

        public MethodInfo quitMeth; // Don't do drugs, folks! LMAO
        public MethodInfo restartMethod;
        public MethodInfo pauseMethod;
        public MethodInfo dialogShowMethod;
        public MethodInfo dialogClearMethod;
        public MethodInfo toastMethod_Info;
        public MethodInfo toastMethod_Warn;
        // TO DO: Hook into other toast notification types
        // public MethodInfo toastMethod_Err;

        public void Dbg_SetSquare1080p()
        {
            Screen.SetResolution(1080, 1080, false);
        }
        public void Dbg_Set720p()
        {
            Screen.SetResolution(1280, 720, false);
        }
        public void Dbg_Set1080p()
        {
            Screen.SetResolution(1920, 1080, false);
        }

        public object Dialog(string title = "Top text", string msg = "This message should not appear.  I'm telling God!")
        {
            dmo = FindAndSetActive("Dialog Container");
            object ret = null;
            if (dmo != null)
            {
                List<string> argz = new List<string> { title, msg };
                ret = dialogShowMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), argz.ToArray());
                // dmo.SetActive(false);
                return ret;
            }
            else
            {
                // dmo.SetActive(false);
                return null;
            }
        }

        public void QuitSong(bool failed = false)
        {
            // gmo = GameObject.FindObjectOfType(gmType, true) as GameObject;
            if (failed)
            {
                PauseOnFail();
                string msgStats = "Score:  " + score.ToString("N0")
                    + "<br>Notes Hit:  " + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0") + " (-" + notesMissed.ToString("N0")
                    + ")<br>Overstrums:  " + overstrums.ToString("N0")
                    + "<br>Ghost Inputs:  " + ghosts.ToString("N0");
                // dialogClearMethod.Invoke(dmo, null);
                
                List<string> argz = new List<string> { "Song failed!", msgStats };
                Dialog("Song failed!", msgStats);
            }
            quitMeth.Invoke(gmo.GetComponent("YARG.Gameplay.GameManager"), null);
        }

        public void FailSong()
        {
            if (meterEnabled && !practice)
            {
                songFailed = true;
                if (restartOnFail)
                {
                    try
                    {
                        playerStats = GetFiveFretStats();
                        score = playerStats.TotalScore;
                        notesHit = playerStats.NotesHit;
                        notesTotal = playerStats.TotalNotes;
                        overstrums = (playerStats as YARG.Core.Engine.Guitar.GuitarStats).Overstrums;
                        ghosts = (playerStats as YARG.Core.Engine.Guitar.GuitarStats).GhostInputs;

                        RestartSong(true);
                    } catch(Exception e) { LogMsg(e); }
                }
                else
                {
                    Destroy(meterContainer);
                }
                // ToastInfo("Song failed!"); // TO DO: Show % song progress on fail
            }
        }

        public void UpdateHealthMeter_FiveFret(double health)
        {
            SetHealthNeedle_FiveFret((float)health);
            LogMsg("Health updated: " + health);
            if (health <= 0f && !songFailed)
            {
                FailSong();
            }
        }
        // Toast notification methods
        public void ToastInfo(string txt = "")
        {
            if (txt == string.Empty) { txt = "This message should not be able to appear.  I'm telling God!"; }

            tmo = GameObject.FindObjectOfType(tmType, true) as GameObject;
            List<string> argz = new List<string> { txt };
            toastMethod_Info.Invoke(tmo, argz.ToArray());
        }
        public void ToastWarning(string txt = "")
        {
            if (txt == string.Empty) { txt = "This message should not be able to appear.  I'm telling God!"; }

            tmo = GameObject.FindObjectOfType(tmType, true) as GameObject;
            List<string> argz = new List<string> { txt };
            toastMethod_Warn.Invoke(tmo, argz.ToArray());
        }

        public bool IsPractice()
        {
            if (gmo != null)
            {
                object ret = gmType.GetProperty("IsPractice").GetValue(gmo.gameObject.GetComponent("YARG.Gameplay.GameManager"));
                // object ret = gmType.GetProperty("IsPractice").GetValue(gmo);
                
                return (bool)ret;
            }
            return false;
        }
        public bool IsReplay()
        {
            if (gmo != null)
            {
                object ret = gmType.GetProperty("IsReplay").GetValue(gmo.gameObject.GetComponent("YARG.Gameplay.GameManager"));

                return (bool)ret;
            }
            return false;
        }

        public void PauseOnFail()
        {
            if (gmo != null)
            {
                List<object> argz = new List<object> { false };

                pauseMethod.Invoke(gmo.GetComponent("YARG.Gameplay.GameManager"), argz.ToArray());
            }
        }

        public GameObject FindAndSetActive(string name, bool active = true)
        {
            UnityEngine.Object[] objs = { };
            objs = Resources.FindObjectsOfTypeAll(typeof(GameObject));
            GameObject ret = null;
            foreach(var obj in objs)
            {
                if (obj is GameObject && obj.name == name)
                {
                    
                    (obj as GameObject).SetActive(active);
                    ret = obj as GameObject;
                }
            }
            return ret;
        }

        public void RestartSong(bool failed = false)
        {
            pmm = FindAndSetActive("Pause Menu Manager");
            if (pmm != null)
            {
                if (failed)
                {
                    string msgStats = "<align=\"left\"><size=36><b>   <u>Stats</u></b></size><br>Score:  " + score.ToString("N0")
                        + "<br>Notes Hit:  " + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0")
                        + "<br>Overstrums:  " + overstrums.ToString("N0")
                        + "<br>Ghost Inputs:  " + ghosts.ToString("N0")
                        + "</align><br><br><size=36>Press <b>PAUSE</b> or click the button below to restart.</size>";

                    currentDialog = Dialog("Song failed!", msgStats);

                    PauseOnFail();
                }
                else
                {
                    restartMethod.Invoke(pmm.GetComponent("YARG.Gameplay.HUD.PauseMenuManager"), null);
                }
            }
            pmm.SetActive(false);
        }

        public bool UnpauseHandler()
        {
            if (gmo != null)
            {
                object ret = gmType.GetProperty("Paused").GetValue(gmo.GetComponent("YARG.Gameplay.GameManager"));
                if (!(bool)ret && songFailed)
                {
                    if (dmo != null) { dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null); }
                }
                return (bool)ret;
            }
            return false;
        }
        public void FailConfirmHandler()
        {
            if (songFailed && restartOnFail && dmo != null)
            {
                RestartSong(false);
            }
        }

        public void DrainHealth_FiveFret(int playerIndex = 0)
        {
            if (!practice && !replay)
            {
                currentHealth -= missDrainAmount;
                if (currentHealth < 0) { currentHealth = 0; }
                UpdateHealthMeter_FiveFret(currentHealth);
            }
        }
        public void AddHealth_FiveFret()
        {
            if (!practice && !replay && currentHealth < 1)
            {
                YARG.Core.Engine.BaseStats stats = null;
                try { stats = GetFiveFretStats(); } catch { }
                if (stats != null)
                {
                    if (stats.IsStarPowerActive) { currentHealth += hitSPGainAmount; }
                    else { currentHealth += hitHealthAmount; }
                }
                if (currentHealth > 1) { currentHealth = 1; }
                UpdateHealthMeter_FiveFret(currentHealth);
            }
        }

        public YARG.Core.Engine.Guitar.GuitarStats GetFiveFretStats()
        {
            object player = GameObject.Find("FiveFretVisual(Clone)").GetComponent("YARG.Gameplay.Player.FiveFretPlayer"); // gmType.GetProperty("_players").GetValue(manager) as List<object>;
            if (player != null)
            {
                YARG.Core.Engine.Guitar.GuitarStats stats = basePlayerType.GetProperty("BaseStats").GetValue(player) as YARG.Core.Engine.Guitar.GuitarStats;
                return stats;
            }
            else
            {
                return null;
            }
        }

        public Texture2D LoadPNG(string filePath)
        {
            Texture2D textur = new Texture2D(512, 512);
            if (File.Exists(filePath))
            {
                byte[] array = File.ReadAllBytes(filePath);
                textur.LoadImage(array);
                return textur;
            }
            return null;
        }

        public void SetHealthNeedle_FiveFret(float health)
        {
            if (needleObj != null)
            {
                float kurtAngle = health * -176f - 2f + 360f;
                needleObj.transform.eulerAngles = new Vector3(0,0,kurtAngle);
            }
        }

        public void InitHealthMeter_FiveFret()
        {
            // get game manager
            if (gmo == null)
            {
                gmo = GameObject.Find("Game Manager");
            }
            practice = IsPractice();
            replay = IsReplay();
            // get hud container
            hudObj = GameObject.Find("Canvas/Main HUD Container");
            if (hudObj != null && meterObj == null && meterEnabled && !practice && !replay)
            {
                string meterAsset = Path.Combine(Paths.PluginPath, "assets", "meter.png");
                string needleAsset = Path.Combine(Paths.PluginPath, "assets", "needle.png");

                meterContainer = new GameObject("Health Meter Container");
                meterContainer.transform.SetParent(hudObj.transform, false);
                meterObj = new GameObject("Health Meter");
                meterObj.transform.SetParent(meterContainer.transform, false);
                needleObj = new GameObject("Health Needle");
                needleObj.transform.SetParent(meterContainer.transform, false);
                meterContainer.transform.localScale = new Vector3(2,2,2);
                meterContainer.transform.Translate(Screen.width * 0.375f, Screen.height * 0.1f, 0);

                if (File.Exists(meterAsset) && File.Exists(needleAsset))
                {
                    meterImg = meterObj.AddComponent<RawImage>();
                    needleImg = needleObj.AddComponent<RawImage>();
                    meterImg.texture = LoadPNG(meterAsset);
                    needleImg.texture = LoadPNG(needleAsset);
                }
                else
                {
                    ToastWarning("Couldn't load assets for the rock meter.");
                }
                needleObj.transform.eulerAngles = new Vector3(0,0,-90);
            }
        }

        // rock meter objects
        public GameObject hudObj;
        public GameObject meterContainer;
        public GameObject meterObj;
        public GameObject needleObj;
        public RawImage meterImg;
        public RawImage needleImg;

        public bool meterEnabled;

        #region Unity Methods
        public void Start()
        {
            // Load assemblies and methods from them
            yargMainAsm = Assembly.Load("Assembly-CSharp.dll");
            yargCoreAsm = Assembly.Load("YARG.Core.dll");
            gmType = yargMainAsm.GetType("YARG.Gameplay.GameManager");
            basePlayerType = yargMainAsm.GetType("YARG.Gameplay.Player.BasePlayer");
            tmType = yargMainAsm.GetType("YARG.Menu.Persistent.ToastManager");
            pmmType = yargMainAsm.GetType("YARG.Gameplay.HUD.PauseMenuManager");
            dmType = yargMainAsm.GetType("YARG.Menu.Persistent.DialogManager");
            
            quitMeth = gmType.GetMethod("ForceQuitSong");
            restartMethod = pmmType.GetMethod("Restart");
            pauseMethod = gmType.GetMethod("Pause");
            dialogShowMethod = dmType.GetMethod("ShowMessage");
            dialogClearMethod = dmType.GetMethod("ClearDialog");
            toastMethod_Info = tmType.GetMethod("ToastInformation");
            toastMethod_Warn = tmType.GetMethod("ToastWarning");


            // // To do: implement multiple players eventually

            inGame = false;
            songFailed = false;

            meterEnabled = true;
            // meterEnabled = false;     // uncomment to disable the meter entirely
            
            restartOnFail = true;
            // endSongOnFail = false;    // uncomment to disable quitting on song fail
            
            missDrainAmount = 0.0277;
            hitHealthAmount = 0.0069;  // Nice
            hitSPGainAmount = 0.045;

            logs = new List<string>();

            SceneManager.sceneLoaded += delegate (Scene sc, LoadSceneMode __)
            {
                if (sc.name == "Gameplay") {
                    currentHealth = defaultHealth;
                    inGame = true;
                    songFailed = false;
                }
                else if (sc.name == "PersistentScene")
                {

                }
            };
            SceneManager.sceneUnloaded += delegate (Scene sc)
            {
                if (sc.name == "Gameplay")
                {
                    currentHealth = defaultHealth;
                    inGame = false;
                    meterContainer = null;
                    practice = false;
                }
            };

            Harmony.PatchAll();
        }

        public void LateUpdate()
        {
            if (inGame)
            {
                timerDebugText += Time.deltaTime;
                if (dbgTxtObj == null && timerDebugText > 0.1f)
                {
                    dbgTxtObj = GameObject.Find("Debug Text");
                    timerDebugText -= 0.1f;
                }
                if (dbgTxtObj != null)
                {
                    dbgTxtTMP = dbgTxtObj.GetComponent<TextMeshProUGUI>();
                    dbgTxtTMP.text += "<br>Rock Meter:   " + Math.Round(currentHealth * 100).ToString() + "%";
                }
                if (songFailed)
                {

                }
            }
        }
        #endregion
    }
}
