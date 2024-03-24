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
    
    [HarmonyPatch(typeName:"YARG.Core.Engine.Guitar.GuitarEngine", methodName: "MissNote")]
    public class FiveFretMissNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.DrainHealth(__instance, "Missed Note");
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Guitar.GuitarEngine", methodName: "HitNote")]
    public class FiveFretHitNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.AddHealth(__instance);
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Guitar.GuitarEngine", methodName: "Overstrum")]
    public class FiveFretOverstrumHandler
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.DrainHealth(__instance, "Overstrum");
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.Player.DrumsPlayer", methodName: "OnNoteMissed")]
    public class DrumMissNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.DrainHealth(__instance, "Missed Note");
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.Player.DrumsPlayer", methodName: "OnNoteHit")]
    public class DrumHitNoteHandler
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.AddHealth(__instance);
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Drums.DrumsEngine", methodName: "Overhit")]
    public class DrumOverhitHandler
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.DrainHealth(__instance, "Overhit");
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.GameManager", methodName: "CreatePlayers")]
    public class OnGameLoad
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.InitHealthMeter();
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.GameManager", methodName: "Resume")]
    public class OnUnpause
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.UnpauseHandler();
        }
    }
    [HarmonyPatch(typeName: "YARG.Menu.Persistent.DialogManager", methodName: "ClearDialog")]
    public class OnDialogClose
    {
        [HarmonyPostfix]
        static void Postfix(ref object __instance)
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
        private const string VersionString = "0.5.0";

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
        public bool showCombo;
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

        public Type gmType;             // Game manager
        public Type basePlayerType;     // Base Player
        public Type guitarType;         // Five Fret Player
        public Type drumsType;          // Drums Player
        public Type trackPlayerType;    // TrackPlayer
        public Type tmType;             // Toast notification manager
        public Type pmmType;            // Pause menu manager (for restarting song) (soon™)
        public Type dmType;             // Dialog manager

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
        public T GetGameManagerProperty<T>(string property) where T : Type
        {
            if (gmo != null)
            {
                object obj = gmType.GetProperty(property).GetValue(gmo.GetComponent("YARG.Gameplay.GameManager"));
                if (obj.GetType() == typeof(T))
                {
                    T rex = (T)obj;
                    return rex;
                }
            }
            return null;
        }
        public object GetReflectedProperty(object obj, string property, Type propertyType = null, Type objectType = null)
        {
            try
            {
                if (objectType == null) objectType = obj.GetType();
                var val = (objectType.GetProperty(property).GetValue(obj));
                if (val.GetType() == propertyType) return val;
                else return null;
            }
            catch { return null; }
        }
        public IEnumerable<object> GetGameManagerEnumerable(string listProperty, Type typeCheck = null)
        {
            if (gmo != null)
            {
                IReadOnlyList<object> rawList = gmType.GetProperty(listProperty).GetValue(gmo.GetComponent("YARG.Gameplay.GameManager")) as IReadOnlyList<object>;

                if (rawList != null)
                {
                    if (rawList.Any())
                    {
                        if (rawList.First().GetType() == typeCheck || typeCheck == null) return rawList;
                    }
                }
            }
            return new List<object> { }; ;
        }
        public int GetGameManagerInt(string intProperty)
        {
            if (gmo != null)
            {
                object obj = gmType.GetProperty(intProperty).GetValue(gmo.GetComponent("YARG.Gameplay.GameManager"));
                if (obj.GetType() == typeof(int))
                {
                    return (int)obj;
                }
            }
            return -1;
        }

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
        
        public void FailSong(BaseStats s, string causeOfFail = "")
        {
            statsList = GetAllStats();
            int index = statsList.IndexOf(s);
            
            if (meterEnabled && !practice)
            {
                songFailed = true;
                if (restartOnFail && statsList.Count == 1)
                {
                    try
                    {
                        if (s is GuitarStats || s is DrumsStats)
                        {
                            score = s.CommittedScore + s.PendingScore + s.SoloBonuses;
                            notesHit = s.NotesHit;
                            notesTotal = s.TotalNotes;
                            maxStreak = s.MaxCombo;
                            msgSummary = "<size=24>(" + causeOfFail + ")</size><size=36><br><br><b><u>Stats</u></b></size><br><align=left>" 
                                + "<br><pos=35%>Score:<pos=55%>"
                                + score.ToString("N0")
                                + "<br><pos=35%>Notes Hit:<pos=55%>" + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0")
                                + "<br><pos=35%>Best Streak:<pos=55%>" + maxStreak.ToString("N0")
                                + "<br><pos=35%>SP Phrases:<pos=55%>" + s.StarPowerPhrasesHit + " / " + s.TotalStarPowerPhrases;
                            if (s is GuitarStats)
                            {
                                overstrums = (s as GuitarStats).Overstrums;
                                ghosts = (s as GuitarStats).GhostInputs;
                                msgSummary = msgSummary.Replace("Stats</u>", "Stats (5-Fret Guitar)</u>");
                                msgSummary += "<br><pos=35%>Overstrums:<pos=55%>" + overstrums.ToString("N0")
                                   + "<br><pos=35%>Ghost Inputs:<pos=55%>" + ghosts.ToString("N0");
                            }
                            else if (s.GetType().ToString() == "YARG.Core.Engine.Drums.DrumsStats")
                            {
                                overhits = (s as DrumsStats).Overhits;
                                msgSummary = msgSummary.Replace("Stats</u>", "Stats (Drums)</u>");
                                msgSummary += "<br><pos=35%>Overhits:<pos=55%>" + overhits.ToString("N0");
                            }
                            msgSummary += "</align><br><br><size=36>Press <b>PAUSE</b> or click the button below to restart.</size>";
                        }

                        RestartSong(true, causeOfFail);
                    } catch(Exception e) { LogMsg(e); }
                }
                else
                {
                    Destroy(rockMeterObj);
                    ToastInfo("Player " + (index+1).ToString() + " failed!<br>(" + causeOfFail + ")");
                }
            }
        }
        public void UpdateHealthMeter(double health)
        {
            SetHealthNeedle((float)health);
            // LogMsg("Health updated: " + health);
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

        public void RestartSong(bool failed = false, string causeOfFail = "")
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
            if (songFailed && restartOnFail && statsList.Count == 1)
            {
                RestartSong(false);
            }
        }
        
        public void DrainHealth(object invoker, string cause = "")
        {
            SetComboMeter(GetBandCombo());
            double oldHealth = currentHealth;
            if (!practice && !replay)
            {
                BaseStats stats = null;
                if (invoker.GetType().Name.EndsWith("Player"))
                {
                    stats = (BaseStats)basePlayerType.GetProperty("BaseStats").GetValue(invoker);
                }
                else if (invoker.GetType().Name.EndsWith("Engine"))
                {
                    stats = (BaseStats)(typeof(BaseEngine).GetProperty("BaseStats").GetValue(invoker));
                }

                currentHealth -= missDrainAmount;
                if (currentHealth < 0) { currentHealth = 0; }
                if (currentHealth != oldHealth)
                {
                    UpdateHealthMeter(currentHealth);

                    if (currentHealth <= 0f && !songFailed && stats != null)
                    {
                        FailSong(stats, cause);
                    }
                }
            }
        }
        public void AddHealth(object invoker)
        {
            SetComboMeter(GetBandCombo());
            // LogMsg(invoker);
            double oldHealth = currentHealth;
            if (!practice && !replay && currentHealth < 1)
            {
                BaseStats stats = null;
                if (invoker.GetType().Name.EndsWith("Player"))
                {
                    stats = (BaseStats)basePlayerType.GetProperty("BaseStats").GetValue(invoker);
                }
                else if (invoker.GetType().Name.EndsWith("Engine"))
                {
                    stats = (BaseStats)(typeof(BaseEngine).GetProperty("BaseStats").GetValue(invoker));
                }
                if (stats != null)
                {
                    if (stats.IsStarPowerActive) { currentHealth += hitSPGainAmount; }
                    else { currentHealth += hitHealthAmount; }
                }
                if (currentHealth > 1) { currentHealth = 1; }
                if (currentHealth != oldHealth) { UpdateHealthMeter(currentHealth); }
            }
        }
        public List<object> GetBasePlayers()
        {
            List<GameObject> gotPlayers = GetPlayerObjects();

            List<object> ret = new List<object>();
            if (gotPlayers.Any())
            {
                foreach (GameObject obj in gotPlayers)
                {
                    var guitarComp = obj.GetComponent(guitarType);
                    var drumComp = obj.GetComponent(drumsType);
                    if (guitarComp != null) ret.Add(guitarComp);
                    else if (drumComp != null) ret.Add(drumComp);
                }
            }
            return ret;
        }
        public List<BaseStats> GetAllStats()
        {
            List<BaseStats> ret = new List<BaseStats>();
            if (gmo != null)
            {
                IEnumerable<UnityEngine.Object> players = GetPlayerObjects();
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
        public List<GameObject> GetPlayerObjects()
        {
            // To do: come up with a better way of finding all players
            IEnumerable<UnityEngine.Object> rawPlayers = Resources.FindObjectsOfTypeAll(typeof(GameObject)).Where(obj => obj.name.EndsWith("Visual(Clone)"));
            List<GameObject> ret = new List<GameObject>();
            foreach(UnityEngine.Object rawPlayer in rawPlayers)
            {
                ret.Add(rawPlayer as GameObject);
            }
            ret.Reverse();
            return ret;
        }
        public int GetBandCombo()
        {
            int r = 0;
            foreach (var player in statsList)
            {
                r += player.Combo;
            }
            return r;
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

        public void SetComboMeter(int streak = 0)
        {
            if (comboTxt != null)
            {
                string comboStr = streak.ToString("000000");
                int streakThreshold = 30;
                if (statsList.Count > 1)
                {
                    streakThreshold = 1;
                }
                if (streak >= streakThreshold) comboStr = comboStr.Replace(streak.ToString(), "</color>" + streak);
                else if (streak > 999999) comboStr = "</color><color=#DDDD99>??????</color>";
                comboTxt.text = "<size=20><br><br><align=center><mspace=0.55em><color=#00000000>" + comboStr;
            }
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
            if (hudObj != null && meterContainer == null)
            {
                string meterAsset = Path.Combine(Paths.PluginPath, "assets", "meter.png");
                string needleAsset = Path.Combine(Paths.PluginPath, "assets", "needle.png");
                string comboAsset = Path.Combine(Paths.PluginPath, "assets", "combometer.png");
                
                meterContainer = new GameObject("Health Meter Container");
                meterContainer.transform.SetParent(hudObj.transform, false);
                meterContainer.transform.localScale = new Vector3(2, 2, 1);

                if (meterEnabled && !practice && !replay)
                {
                    rockMeterObj = new GameObject("Health Meter");
                    rockMeterObj.transform.SetParent(meterContainer.transform, false);
                    needleObj = new GameObject("Health Needle");
                    needleObj.transform.SetParent(meterContainer.transform, false);
                    if (File.Exists(meterAsset) && File.Exists(needleAsset))
                    {
                        rockMeterImg = rockMeterObj.AddComponent<RawImage>();
                        needleImg = needleObj.AddComponent<RawImage>();
                        rockMeterImg.texture = LoadPNG(meterAsset);
                        needleImg.texture = LoadPNG(needleAsset);
                    }
                    else
                    {
                        ToastWarning("Couldn't load assets for the rock meter.");
                    }

                    needleObj.transform.eulerAngles = new Vector3(0, 0, -90);
                }

                SetMeterPos(cfgMeterX.Value, cfgMeterY.Value);
                
                // Initialize combo meter, if enabled
                if (File.Exists(comboAsset) && showCombo)
                {
                    comboMeterObj = new GameObject("Combo Meter");
                    comboMeterObj.transform.SetParent(meterContainer.transform, false);
                    comboTxtObj = new GameObject("Combo Text");
                    comboTxtObj.transform.SetParent(meterContainer.transform, false);

                    comboMeterImg = comboMeterObj.AddComponent<RawImage>();
                    comboMeterImg.texture = LoadPNG(comboAsset);
                    comboTxt = comboTxtObj.AddComponent<TextMeshProUGUI>();
                }
            }
        }

        // rock meter objects
        public GameObject hudObj;
        public GameObject meterContainer;
        public GameObject rockMeterObj;
        public GameObject needleObj;
        public GameObject comboMeterObj;
        public GameObject comboTxtObj;
        public TextMeshProUGUI comboTxt;
        public RawImage rockMeterImg;
        public RawImage needleImg;
        public RawImage comboMeterImg;

        public bool meterEnabled;

        #region Mouse Handlers
        // Handlers for rock meter dragging with mouse
        public void MouseDownHandle()
        {
            mousePosOnDown = Mouse.current.position.ReadValue();
            if (inGame && meterContainer != null)
            {
                RectTransform rect = meterContainer.GetComponentInChildren<RectTransform>();
                // LogMsg("Rect  =" + rect.ToString());
                // LogMsg("Mouse =" + mousePosOnDown.ToString());
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
        public ConfigEntry<bool> cfgShowComboMeter;

        #region Unity Methods
        private void Start()
        {
            // Load assemblies and methods from them
            yargMainAsm = Assembly.Load("Assembly-CSharp.dll");
            yargCoreAsm = Assembly.Load("YARG.Core.dll");
            gmType = yargMainAsm.GetType("YARG.Gameplay.GameManager");
            basePlayerType = yargMainAsm.GetType("YARG.Gameplay.Player.BasePlayer");
            guitarType = yargMainAsm.GetType("YARG.Gameplay.Player.FiveFretPlayer");
            drumsType = yargMainAsm.GetType("YARG.Gameplay.Player.DrumsPlayer");
            trackPlayerType = yargMainAsm.GetType("YARG.Gameplay.Player.TrackPlayer");
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

            cfgEnableMeter = Config.Bind("Rock Meter", "EnableRockMeter", true,
                "Enable or disable the Rock Meter entirely");
            meterEnabled = cfgEnableMeter.Value;

            cfgRestartFail = Config.Bind("Rock Meter", "RestartOnFail", true,
                "Control whether to force restart on fail.  If false, then gameplay will not be interrupted on fail.");
            restartOnFail = cfgRestartFail.Value;

            cfgShowComboMeter = Config.Bind("Combo Meter", "EnableComboMeter", true,
                "Enable or disable the combo meter");
            showCombo = cfgShowComboMeter.Value;
            
            // TO DO: Maybe add config options for these values?
            missDrainAmount = 0.0277;
            hitHealthAmount = 0.0069;  // Nice
            hitSPGainAmount = 0.045;

            meterPosX = Screen.width * 0.875f;
            meterPosY = Screen.height * 0.6f;
            cfgMeterX = Config.Bind("Rock Meter", "MeterPosition_X", (int)meterPosX);
            cfgMeterY = Config.Bind("Rock Meter", "MeterPosition_Y", (int)meterPosY);
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
                    showCombo = cfgShowComboMeter.Value;
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
                    replay = false;
                }
            };

            Harmony.PatchAll();
        }
        private void LateUpdate()
        {

            if (inGame)
            {
#if DEBUG
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
#endif
                if (comboTxt != null)
                {
                    if (comboTxt.text == null) SetComboMeter(0);    // Initialize streak counter if it isn't already
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
