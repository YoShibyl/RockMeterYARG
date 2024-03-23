using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using YARG.Core.Engine;
using System.Linq;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Drums;

namespace RockMeterYARG
{
    
    [HarmonyPatch(typeName:"YARG.Gameplay.Player.FiveFretPlayer", methodName: "OnNoteMissed")]
    public class FiveFretMissNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.DrainHealth();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.Player.FiveFretPlayer", methodName: "OnNoteHit")]
    public class FiveFretHitNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.AddHealth();
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Guitar.GuitarEngine", methodName: "Overstrum")]
    public class FiveFretOverstrumHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.DrainHealth();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.Player.DrumsPlayer", methodName: "OnNoteMissed")]
    public class DrumMissNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.DrainHealth();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.Player.DrumsPlayer", methodName: "OnNoteHit")]
    public class DrumHitNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.AddHealth();
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Drums.DrumsEngine", methodName: "Overhit")]
    public class DrumOverhitHandler
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.DrainHealth();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.GameManager", methodName: "CreatePlayers")]
    public class OnGameLoad
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.InitHealthMeter();
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
        private const string VersionString = "0.3.1";

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
        public double missDrainAmount;  // Amount to take from health on note miss or overstrum/overhit
        public double hitHealthAmount;  // Amount to add to health on note hit usually
        public double hitSPGainAmount;  // Amount to add to health on note hit during Star Power
        public double currentHealth;

        public Vector2 mousePosOnDown;
        public float meterPosX;
        public float meterPosY;
        public Vector2 dragDiff;
        public bool isDragging;
        public bool isMouseDown;

        public List<UnityEngine.Object> players;
        public List<YARG.Core.Engine.BaseStats> statsList;
        public int score;           // Score on fail
        public int maxStreak;       // Note streak on fail
        public int ghosts;          // Ghost inputs on fail
        public int notesHit;        // Notes hit
        public int notesTotal;      // Total notes in song
        public int overstrums;      // Overstrums (guitar)
        public int overhits;        // Overhits (drums)
        public string msgSummary;   // Message to show on fail

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
        // TO DO: Hook into other toast notification types (even tho we might not use them)
        // public MethodInfo toastMethod_Err;
#if DEBUG
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
#endif
        public object Dialog(string title = "Top text", string msg = "This message should not appear.  I'm telling God!")
        {
            dmo = FindAndSetActive("Dialog Container");
            object ret = null;
            if (dmo != null)
            {
                List<string> argz = new List<string> { title, msg };
                ret = dialogShowMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), argz.ToArray());
                return ret;
            }
            else
            {
                return null;
            }
        }
        /* // Quitting not implemented, might delete later
        public void QuitSong(bool failed = false)
        {
            if (failed)
            {
                PauseOnFail();
                msgSummary = "Score:  " + score.ToString("N0")
                    + "<br>Notes Hit:  " + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0") + " (-" + notesMissed.ToString("N0")
                    + ")<br>Overstrums:  " + overstrums.ToString("N0")
                    + "<br>Ghost Inputs:  " + ghosts.ToString("N0"); 
                
                List<string> argz = new List<string> { "Song failed!", msgSummary };
                Dialog("Song failed!", msgSummary);
            }
            quitMeth.Invoke(gmo.GetComponent("YARG.Gameplay.GameManager"), null);
        } // */ // (not using this for now)

        public void FailSong(int whichPlayer = 0)   // We might eventually detect which player failed, idk
        {
            if (meterEnabled && !practice)
            {
                songFailed = true;
                if (restartOnFail)
                {
                    try
                    {
                        BaseStats stats = statsList[whichPlayer];
                        if (stats is GuitarStats || stats is DrumsStats)
                        {
                            score = stats.TotalScore;
                            notesHit = stats.NotesHit;
                            notesTotal = stats.TotalNotes;
                            maxStreak = stats.MaxCombo;
                            msgSummary = "<size=36><b><u>Stats</u></b></size><br><align=\"left\"><pos=35%>Score:<pos=55%>" + score.ToString("N0")
                                + "<br><pos=35%>Notes Hit:<pos=55%>" + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0")
                                + "<br><pos=35%>Best Streak:<pos=55%>" + maxStreak.ToString("N0");
                            if (stats.GetType().ToString() == "YARG.Core.Engine.Guitar.GuitarStats")
                            {
                                overstrums = (stats as YARG.Core.Engine.Guitar.GuitarStats).Overstrums;
                                ghosts = (stats as YARG.Core.Engine.Guitar.GuitarStats).GhostInputs;
                                msgSummary += "<br><pos=35%>Overstrums:<pos=55%>" + overstrums.ToString("N0")
                                   + "<br><pos=35%>Ghost Inputs:<pos=55%>" + ghosts.ToString("N0");
                            }
                            else if (stats.GetType().ToString() == "YARG.Core.Engine.Drums.DrumsStats")
                            {
                                overhits = (stats as YARG.Core.Engine.Drums.DrumsStats).Overhits;
                                msgSummary += "<br><pos=35%>Overhits:<pos=55%>" + overhits.ToString("N0");
                            }
                            msgSummary += "</align><br><br><size=36>Press <b>PAUSE</b> or click the button below to restart.</size>";
                        }

                        RestartSong(true);
                    } catch(Exception e) { LogMsg(e); }
                }
                else
                {
                    Destroy(meterContainer);
                    ToastInfo("Song failed!");
                }
            }
        }
        public void UpdateHealthMeter(double health)
        {
            SetHealthNeedle((float)health);
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

        // Methods to check whether we're in Practice Mode or watching a replay
        // This is so we don't use the Rock Meter in those modes
        public bool IsPractice()
        {
            if (gmo != null)
            {
                object ret = gmType.GetProperty("IsPractice").GetValue(gmo.gameObject.GetComponent("YARG.Gameplay.GameManager"));
                
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
                    currentDialog = Dialog("Song failed!", msgSummary);

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

        public void DrainHealth()
        {
            double oldHealth = currentHealth;
            if (!practice && !replay)
            {
                currentHealth -= missDrainAmount;
                if (currentHealth < 0) { currentHealth = 0; }
                if (currentHealth != oldHealth) { UpdateHealthMeter(currentHealth); }
            }
        }
        public void AddHealth()
        {
            double oldHealth = currentHealth;
            if (!practice && !replay && currentHealth < 1)
            {
                YARG.Core.Engine.BaseStats stats = statsList[0];
                if (stats != null)
                {
                    if (stats.IsStarPowerActive) { currentHealth += hitSPGainAmount; }
                    else { currentHealth += hitHealthAmount; }
                }
                if (currentHealth > 1) { currentHealth = 1; }
                if (currentHealth != oldHealth) { UpdateHealthMeter(currentHealth); }
            }
        }

        public List<BaseStats> GetAllStats()
        {
            List<BaseStats> ret = new List<BaseStats>();
            if (gmo != null)
            {
                IEnumerable<UnityEngine.Object> players = GetPlayers();
                foreach (GameObject player in players)
                {
                    var guitarComp = player.GetComponent("YARG.Gameplay.Player.FiveFretPlayer");
                    var drumComp = player.GetComponent("YARG.Gameplay.Player.DrumsPlayer");
                    if (guitarComp != null)
                        ret.Add(basePlayerType.GetProperty("BaseStats").GetValue(guitarComp) as BaseStats);
                    else if (drumComp != null)
                    {
                        ret.Add(basePlayerType.GetProperty("BaseStats").GetValue(drumComp) as BaseStats);
                    }
                }
            }
            return ret;
        }
        public List<GameObject> GetPlayers()
        {
            // To do: come up with a better way of finding all players
            IEnumerable<UnityEngine.Object> rawPlayers = Resources.FindObjectsOfTypeAll(typeof(GameObject)).Where(obj => obj.name.EndsWith("Visual(Clone)")) as IEnumerable<UnityEngine.Object>;
            List<GameObject> ret = new List<GameObject>();
            foreach(UnityEngine.Object rawPlayer in rawPlayers)
            {
                ret.Add(rawPlayer as GameObject);
            }
            ret.Reverse();
            return ret;
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

        public void SetHealthNeedle(float health)
        {
            if (needleObj != null)
            {
                float kurtAngle = health * -176f - 2f + 360f;
                needleObj.transform.eulerAngles = new Vector3(0,0,kurtAngle);
            }
        }

        public void SetMeterPos(float x, float y)
        {
            if (x > Screen.width || x < 0 || y > Screen.height || y < 0)
            { // Check if the meter is off-screen
                x = Screen.width * 0.875f;
                y = Screen.height * 0.6f;
            }
            meterContainer.transform.position = new Vector3(x,y,0);
            meterPosX = x;
            meterPosY = y;
        }
        public void UpdateConfig()
        {
            cfgMeterX.SetSerializedValue(((int)meterPosX).ToString());
            cfgMeterY.SetSerializedValue(((int)meterPosY).ToString());
            cfgEnableMeter.SetSerializedValue(meterEnabled.ToString());
            cfgRestartFail.SetSerializedValue(restartOnFail.ToString());
        }

        public void InitHealthMeter()
        {
            // get game manager
            if (gmo == null)
            {
                gmo = GameObject.Find("Game Manager");
            }
            practice = IsPractice();
            replay = IsReplay();
            statsList = GetAllStats();
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
                meterContainer.transform.localScale = new Vector3(2,2,1);

                SetMeterPos(cfgMeterX.Value, cfgMeterY.Value);
                
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

        #region Mouse Handlers
        // Handlers for rock meter dragging with mouse
        public void MouseDownHandle()
        {
            mousePosOnDown = Mouse.current.position.ReadValue();
            if (inGame && meterImg != null)
            {
                RectTransform rect = meterObj.GetComponent<RectTransform>();
                LogMsg("Rect  =" + rect.ToString());
                LogMsg("Mouse =" + mousePosOnDown.ToString());
                isDragging = rect.rect.Contains(mousePosOnDown - (Vector2)meterContainer.transform.position);
                dragDiff = mousePosOnDown - (Vector2)meterContainer.transform.position;
            }
        }
        public void MouseDragHandle()
        {
            if (meterContainer != null)
            {
                meterContainer.transform.position = (Vector3)Mouse.current.position.value - (Vector3)dragDiff;
            }
        }
        public void MouseUpHandle()
        {
            if (inGame && meterContainer != null)
            {
                Vector2 pos = meterContainer.transform.position;
                SetMeterPos(pos.x, pos.y);
                UpdateConfig();
            }
        }
        #endregion
        // Config entries
        public ConfigEntry<int> cfgMeterX;
        public ConfigEntry<int> cfgMeterY;
        public ConfigEntry<bool> cfgRestartFail;
        public ConfigEntry<bool> cfgEnableMeter;

        #region Unity Methods
        private void Start()
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

            cfgEnableMeter = Config.Bind<bool>("Rock Meter", "EnableRockMeter", true,
                "Enable or disable the Rock Meter entirely");
            meterEnabled = cfgEnableMeter.Value;

            cfgRestartFail = Config.Bind<bool>("Rock Meter", "RestartOnFail", true,
                "Control whether to force restart on fail.  If false, then gameplay will not be interrupted on fail.");
            restartOnFail = cfgRestartFail.Value;
            
            // TO DO: Maybe add config options for these values?
            missDrainAmount = 0.0277;
            hitHealthAmount = 0.0069;  // Nice
            hitSPGainAmount = 0.045;

            meterPosX = Screen.width * 0.875f;
            meterPosY = Screen.height * 0.6f;
            cfgMeterX = Config.Bind<int>("Rock Meter", "MeterPosition_X", (int)meterPosX);
            cfgMeterY = Config.Bind<int>("Rock Meter", "MeterPosition_Y", (int)meterPosY);
            isDragging = false;
            isMouseDown = false;

            SceneManager.sceneLoaded += delegate (Scene sc, LoadSceneMode __)
            {
                if (sc.name == "Gameplay") {
                    msgSummary = "";
                    currentHealth = defaultHealth;
                    inGame = true;
                    songFailed = false;
                    restartOnFail = cfgRestartFail.Value;
                    meterEnabled = cfgEnableMeter.Value;
                } /*
                else if (sc.name == "PersistentScene")
                {
                    // currently not used for this mod ig
                } // */
            };
            SceneManager.sceneUnloaded += delegate (Scene sc)
            {
                if (sc.name == "Gameplay")
                {
                    currentHealth = defaultHealth;
                    inGame = false;
                    meterContainer = null;
                    practice = false;
                    replay = false;
                }
            };

            Harmony.PatchAll();
        }
        private void LateUpdate()
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
                    dbgTxtTMP.text += "<br>Mouse position: " + Mouse.current.position.ReadValue().ToString();
                    dbgTxtTMP.text += "<br>Dragging meter: " + isDragging.ToString();
                    dbgTxtTMP.text += "<br>Rock Meter:   " + Math.Round(currentHealth * 100).ToString() + "%";
                }
                
            }
            if (inGame && Mouse.current.leftButton.isPressed)
            {
                if (!isMouseDown)
                {
                    MouseDownHandle();
                    isMouseDown = true;
                }
                if (isDragging) MouseDragHandle();
            }
            if (!Mouse.current.leftButton.isPressed && isMouseDown)
            {
                MouseUpHandle();

                isDragging = false;
                isMouseDown = false;
            }
            if (inGame && Mouse.current.rightButton.isPressed)
            {
                SetMeterPos(Screen.width * 0.875f, Screen.height * 0.6f);
                isDragging = false;
                isMouseDown = false;
                UpdateConfig();
            }
        }
        #endregion
    }
}
