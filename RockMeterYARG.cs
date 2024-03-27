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

namespace RockMeterYARG         // TO DO:  Implement configurable combo meter colors
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
            RockMeterYARG.Instance.ClearDialogHandler();
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
        private const string VersionString = "0.6.0";

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
        public GameObject gvo;      // Global Variables object
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
        public float comboPosX;
        public float comboPosY;
        public Vector2 dragDiff_Health;
        public Vector2 dragDiff_Combo;
        public bool isDraggingHealth;
        public bool isDraggingCombo;
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

        public Type gvType;             // Global Variables
        public Type gmType;             // Game manager
        public Type basePlayerType;     // Base Player
        public Type guitarType;         // Five Fret Player
        public Type drumsType;          // Drums Player
        public Type trackPlayerType;    // TrackPlayer
        public Type tmType;             // Toast notification manager
        public Type pmmType;            // Pause menu manager (for restarting song)
        public Type dmType;             // Dialog manager

        public MethodInfo quitMeth; // Don't do drugs, folks! LMAO
        public MethodInfo restartMethod;
        public MethodInfo pauseMethod;
        public MethodInfo dialogShowMethod;
        public MethodInfo dialogClearMethod;
        public MethodInfo dialogColorMethod;
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

        public void Dbg_ShowColorDialog()
        {
            
        }
#endif
        public Color ChangeColor(string colorSetting, Color value)
        {
            switch(colorSetting)
            {
                case "ComboTextColor":
                    comboTextColor = value;
                    comboTextRGB = ToHexString_NoTag(value);
                    cfgComboTextColorHex.SetSerializedValue(comboTextRGB);
                    break;
                case "ComboEdgeColor":
                    comboEdgeColor = value;
                    comboEdgeRGB = ToHexString_NoTag(value);
                    cfgComboEdgeColorHex.SetSerializedValue(comboEdgeRGB);
                    break;
                case "ComboMeterColor":
                    comboMeterColor = value;
                    comboMeterRGB = ToHexString_NoTag(value);
                    cfgComboMeterColorHex.SetSerializedValue(comboMeterRGB);
                    break;
                default:
                    break;
            }
            UpdateConfig();
            doOpenConfig = true;
            return value;
        }
        public void ToggleBoolConfig(string param)
        {
            bool val;
            switch(param)
            {
                case "EnableRockMeter":
                    val = !cfgEnableMeter.Value;
                    meterEnabled = val;
                    if (val) cfgEnableMeter.SetSerializedValue("true");
                    else cfgEnableMeter.SetSerializedValue("false");
                    break;
                case "RestartOnFail":
                    val = !cfgRestartFail.Value;
                    restartOnFail = val;
                    if (val) cfgRestartFail.SetSerializedValue("true");
                    else cfgRestartFail.SetSerializedValue("false");
                    break;
                case "EnableComboMeter":
                    val = !cfgShowComboMeter.Value;
                    cfgShowComboMeter.Value = val;
                    if (val) cfgShowComboMeter.SetSerializedValue("true");
                    else cfgShowComboMeter.SetSerializedValue("false");
                    break;
                default:
                    break;
            }
            UpdateConfig();
        }
        public object ShowColorPicker(string colorSetting, Color c)
        {
            dmo = FindAndSetActive("Dialog Container");
            object ret = null;

            if (dmo != null)
            {
                dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null);
                Action<Color> action = delegate (Color _)
                {
                    ChangeColor(colorSetting, _);
                };
                List<object> argz = new List<object> { c, action };
                ret = dialogColorMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), argz.ToArray());
                return ret;
            }
            else
            {
                return null;
            }
        }
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
                if (!inGame) dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null);
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
            restartOnFail = cfgRestartFail.Value;
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
            statsList = GetAllStats();
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
        public void ClearDialogHandler()
        {
            if (songFailed && restartOnFail && statsList.Count == 1)
            {
                RestartSong(false);
            }
            if (!inGame && doOpenConfig)
            {
                // configMenuObj = OpenConfigMenu();
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

        public void SetHealthMeterPos(float x, float y)
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
                if (streak >= streakThreshold) comboStr = comboStr.Replace(streak.ToString(), "</color><color=#" + ToHexString_NoTag(comboTextColor) + ">" + streak);
                else if (streak > 999999) comboStr = "</color>??????";
                if (isLegacyComboMeter) comboTxt.text = "<size=20><br><br><align=center><b><mspace=0.55em><color=#00000000>" + comboStr;
                else comboTxt.text = "<size=20><align=center><b><mspace=0.55em><color=#00000000>" + comboStr;

                if (comboTxt.verticalAlignment != VerticalAlignmentOptions.Middle && !isLegacyComboMeter) comboTxt.verticalAlignment = VerticalAlignmentOptions.Middle;
            }
        }
        public void SetComboPos(float x, float y)
        {
            if (x > Screen.width || x < 0 || y > Screen.height || y < 0)
            { // Check if the meter is off-screen
                x = Screen.width * 0.875f;
                y = Screen.height * 0.53f;
            }
            comboContainer.transform.position = new Vector3(x, y, 0);
            comboPosX = x;
            comboPosY = y;
        }
        public void RefreshColors()
        {
            comboMeterRGB = cfgComboMeterColorHex.GetSerializedValue().ToUpper();
            comboEdgeRGB = cfgComboEdgeColorHex.GetSerializedValue().ToUpper();
            comboTextRGB = cfgComboTextColorHex.GetSerializedValue().ToUpper();

            comboMeterColor = FromHexString_NoTag(comboMeterRGB);
            comboEdgeColor = FromHexString_NoTag(comboEdgeRGB);
            comboTextColor = FromHexString_NoTag(comboTextRGB);
        }

        public void UpdateConfig()
        {
            cfgMeterX.SetSerializedValue(((int)meterPosX).ToString());
            cfgMeterY.SetSerializedValue(((int)meterPosY).ToString());
            cfgEnableMeter.SetSerializedValue(meterEnabled.ToString());
            cfgRestartFail.SetSerializedValue(restartOnFail.ToString());
            cfgComboX.SetSerializedValue(((int)comboPosX).ToString());
            cfgComboY.SetSerializedValue(((int)comboPosY).ToString());
            cfgComboTextColorHex.SetSerializedValue(comboTextRGB);
            cfgComboEdgeColorHex.SetSerializedValue(comboEdgeRGB);
            cfgComboMeterColorHex.SetSerializedValue(comboMeterRGB);
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
            RefreshColors();
            
            // get hud container
            hudObj = GameObject.Find("Canvas/Main HUD Container");

            if (hudObj != null && meterContainer == null)
            {
                string meterAsset = Path.Combine(Paths.PluginPath, "assets", "meter.png");
                string needleAsset = Path.Combine(Paths.PluginPath, "assets", "needle.png");
                string comboOldAsset = Path.Combine(Paths.PluginPath, "assets", "combometer.png");
                string comboBgAsset = Path.Combine(Paths.PluginPath, "assets", "combo_bg.png");
                string comboEdgeAsset = Path.Combine(Paths.PluginPath, "assets", "combo_edge.png");
                string comboAsset = Path.Combine(Paths.PluginPath, "assets", "combo_meter.png");

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

                SetHealthMeterPos(cfgMeterX.Value, cfgMeterY.Value);
                
                // Initialize combo meter, if enabled
                if (showCombo)
                {
                    comboContainer = new GameObject("Streak Counter Container");
                    comboContainer.transform.SetParent(hudObj.transform, false);
                    comboContainer.transform.localScale = new Vector3(2, 2, 1);
                    isLegacyComboMeter = false;
                    if (File.Exists(comboBgAsset) && File.Exists(comboEdgeAsset))
                    {
                        comboMeterObj = new GameObject("Streak Background");
                        comboMeterObj.transform.SetParent(comboContainer.transform, false);
                        comboEdgeObj = new GameObject("Streak Counter Edge");
                        comboEdgeObj.transform.SetParent(comboContainer.transform, false);
                        comboTxtObj = new GameObject("Streak Text");
                        comboTxtObj.transform.SetParent(comboContainer.transform, false);

                        comboMeterImg = comboMeterObj.AddComponent<RawImage>();
                        comboMeterImg.texture = LoadPNG(comboBgAsset);
                        comboMeterImg.color = comboMeterColor;
                        comboEdgeImg = comboEdgeObj.AddComponent<RawImage>();
                        comboEdgeImg.texture = LoadPNG(comboEdgeAsset);
                        comboEdgeImg.color = comboEdgeColor;
                        comboTxt = comboTxtObj.AddComponent<TextMeshProUGUI>();

                        SetComboPos(comboPosX, comboPosY);
                    }
                    else if (File.Exists(comboAsset))
                    {
                        comboMeterObj = new GameObject("Streak Background");
                        comboMeterObj.transform.SetParent(comboContainer.transform, false);
                        comboTxtObj = new GameObject("Streak Text");
                        comboTxtObj.transform.SetParent(comboContainer.transform, false);

                        comboMeterImg = comboMeterObj.AddComponent<RawImage>();
                        comboMeterImg.texture = LoadPNG(comboAsset);
                        comboMeterImg.color = comboMeterColor;
                        comboTxt = comboTxtObj.AddComponent<TextMeshProUGUI>();

                        SetComboPos(comboPosX, comboPosY);
                    }
                    else if (File.Exists(comboOldAsset))
                    {
                        isLegacyComboMeter = true;
                        Destroy(comboContainer);

                        comboMeterObj = new GameObject("Streak Background");
                        comboMeterObj.transform.SetParent(meterContainer.transform, false);
                        comboTxtObj = new GameObject("Streak Text");
                        comboTxtObj.transform.SetParent(meterContainer.transform, false);

                        comboMeterImg = comboMeterObj.AddComponent<RawImage>();
                        comboMeterImg.texture = LoadPNG(comboOldAsset);
                        comboTxt = comboTxtObj.AddComponent<TextMeshProUGUI>();
                    }
                    else
                    {
                        showCombo = false;  // Don't show combo if assets can't be loaded
                        Destroy(comboContainer);
                    }
                }
            }
        }

        // rock meter objects
        public GameObject hudObj;
        public GameObject meterContainer;
        public GameObject rockMeterObj;
        public GameObject needleObj;
        public GameObject comboContainer;
        public GameObject comboMeterObj;
        public GameObject comboEdgeObj;
        public GameObject comboTxtObj;
        public TextMeshProUGUI comboTxt;
        public RawImage rockMeterImg;
        public RawImage needleImg;
        public RawImage comboMeterImg;
        public RawImage comboEdgeImg;
        public bool isLegacyComboMeter;
        public bool meterEnabled;

        // Config menu stuff
        public TextMeshProUGUI configMenuTMP;
        public bool isConfigShowing;
        public bool doOpenConfig;
        public string OnOffStr(bool boolean, bool colorCodeIt = true)
        {
            string r = "";
            if (colorCodeIt)
            {
                if (boolean) r = "<color=#00FF00>ON</color>";
                else r = "<color=#FF0000>OFF</color>";
            }
            else
            {
                if (boolean) r = "ON";
                else r = "OFF";
            }
            return r;
        }
        public string ParseConfigMenuText()
        {
            string options = "<i>Click an option below to configure it!</i><br><br>";
            // The "<color=#00000000>.</color>" is a transparent dot used for breaking up the underlines, otherwise they connect??
            options += "<b><size=40><u>COMBO METER</u><space=9em><color=#00000000>.</color><u>ROCK METER</u></size></b>";
            options += String.Format("<br><align=left><pos=10%><link=\"ComboMeterToggle\"><b>Enabled:<pos=25%>{0}<b></link>", OnOffStr(cfgShowComboMeter.Value));
            options += String.Format("<pos=60%><link=\"RockMeterToggle\">Enabled:<pos=80%>{0}</link>", OnOffStr(meterEnabled));
            options += String.Format("<br><pos=10%><link=\"ComboMeterColor\"><b>Meter Color<pos=25%>#{0} <pos=45%>(<color=#{0}><mspace=0.5em>██</mspace></color><b>)</link>", comboMeterRGB);
            options += String.Format("<pos=60%><link=\"SongFailToggle\">Restart on Fail:<pos=80%>{0}</link>", OnOffStr(restartOnFail));
            options += String.Format("<br><pos=10%><link=\"ComboTextColor\"><b>Text Color<pos=25%>#{0} <pos=45%>(<color=#{0}><mspace=0.5em>██</mspace></color><b>)</link>", comboTextRGB);
            options += String.Format("<br><pos=10%><link=\"ComboEdgeColor\"><b>Edge Color<pos=25%>#{0} <pos=45%>(<color=#{0}><mspace=0.5em>██</mspace></color><b>)</link>", comboEdgeRGB);
            options += String.Format("<br><br><pos=10%><link=\"ResetComboColors\"><color=#FF6666>RESET COLORS</color></link>");
            return options;
        }
        public object OpenConfigMenu()
        {
            object r;
            RefreshColors();
            r = Dialog("Rock Meter Config : " + VersionString, ParseConfigMenuText());
            configMenuTMP = GameObject.Find("Persistent Canvas/Dialog Container/MessageDialog(Clone)/Base/Content/Message")?.GetComponent<TextMeshProUGUI>();
            
            isConfigShowing = true;
            doOpenConfig = false;
            return r;
        }
        #region Mouse Handlers
        // Handlers for rock meter dragging with mouse
        public string linkID;
        public object colorPicker;
        public void MouseDownHandle()
        {
            mousePosOnDown = Mouse.current.position.ReadValue();
            if (inGame && (meterContainer != null || comboContainer != null))
            {
                RectTransform rectMeter = null;
                RectTransform rectCombo = null;
                isDraggingHealth = false;
                if (meterContainer != null)
                {
                    rectMeter = meterContainer.GetComponentInChildren<RectTransform>();
                    rectCombo = comboContainer.GetComponentInChildren<RectTransform>();

                    isDraggingHealth = rectMeter.rect.Contains(mousePosOnDown - (Vector2)meterContainer.transform.position);
                }
                if (!isDraggingHealth)
                {
                    isDraggingCombo = rectCombo.rect.Contains(mousePosOnDown - (Vector2)comboContainer.transform.position);
                    dragDiff_Combo = mousePosOnDown - (Vector2)comboContainer.transform.position;
                }
                else
                {
                    dragDiff_Health = mousePosOnDown - (Vector2)meterContainer.transform.position;
                }
            }
        }
        public void MouseDragHandle()
        {
            if (meterContainer != null && isDraggingHealth)
            {
                meterContainer.transform.position = (Vector3)Mouse.current.position.value - (Vector3)dragDiff_Health;
            }
            else if (comboContainer != null && isDraggingCombo)
            {
                comboContainer.transform.position = (Vector3)Mouse.current.position.value - (Vector3)dragDiff_Combo;
            }
        }
        public void MouseUpHandle()
        {
            if (inGame && meterContainer != null && isDraggingHealth)
            {
                Vector2 pos = meterContainer.transform.position;
                SetHealthMeterPos(pos.x, pos.y);
                UpdateConfig();
            }
            else if (inGame && comboContainer != null && isDraggingCombo)
            {
                Vector2 pos = comboContainer.transform.position;
                SetComboPos(pos.x, pos.y);
                UpdateConfig();
            }
            else if (!inGame)
            {
                if (!isConfigShowing)
                {
                    if (linkID == "RockMeterConfig" || doOpenConfig)
                    {
                        RefreshColors();
                        configMenuObj = OpenConfigMenu();
                        
                    }
                    
                }
                else if (isConfigShowing)
                {
                    switch(linkID)
                    {
                        case "ComboTextColor":
                            ShowColorPicker("ComboTextColor", comboTextColor);
                            break;
                        case "ComboEdgeColor":
                            ShowColorPicker("ComboEdgeColor", comboEdgeColor);
                            break;
                        case "ComboMeterColor":
                            ShowColorPicker("ComboMeterColor", comboMeterColor);
                            break;
                        case "ComboMeterToggle":
                            ToggleBoolConfig("EnableComboMeter");
                            configMenuTMP.text = ParseConfigMenuText();
                            break;
                        case "RockMeterToggle":
                            ToggleBoolConfig("EnableRockMeter");
                            configMenuTMP.text = ParseConfigMenuText();
                            break;
                        case "SongFailToggle":
                            ToggleBoolConfig("RestartOnFail");
                            configMenuTMP.text = ParseConfigMenuText();
                            break;
                        case "ResetComboColors":
                            comboMeterRGB = "444444";
                            comboMeterColor = FromHexString_NoTag(comboMeterRGB);
                            comboEdgeRGB = "7F7F7F";
                            comboEdgeColor = FromHexString_NoTag(comboEdgeRGB);
                            comboTextRGB = "FFFFFF";
                            comboTextColor = FromHexString_NoTag(comboTextRGB);
                            cfgComboMeterColorHex.SetSerializedValue(comboMeterRGB);
                            cfgComboEdgeColorHex.SetSerializedValue(comboEdgeRGB); 
                            cfgComboTextColorHex.SetSerializedValue(comboTextRGB);
                            RefreshColors();
                            configMenuTMP.text = ParseConfigMenuText();
                            break;
                        default:
                            break;
                    }
                }
            }
            isMouseDown = false;
        }
        #endregion
        // Config entries
        public ConfigEntry<int> cfgMeterX;
        public ConfigEntry<int> cfgMeterY;
        public ConfigEntry<int> cfgComboX;
        public ConfigEntry<int> cfgComboY;
        public ConfigEntry<bool> cfgRestartFail;
        public ConfigEntry<bool> cfgEnableMeter;
        public ConfigEntry<bool> cfgShowComboMeter;
        public ConfigEntry<string> cfgComboMeterColorHex;
        public ConfigEntry<string> cfgComboEdgeColorHex;
        public ConfigEntry<string> cfgComboTextColorHex;
        // public ConfigEntry<string> cfgRockMeterColorHex;    // soon™
        // public Color rockMeterColor;
        // public string rockMeterRGB;
        public Color comboMeterColor;
        public Color comboEdgeColor;
        public Color comboTextColor;
        public string comboMeterRGB;
        public string comboEdgeRGB;
        public string comboTextRGB;

        public TextMeshProUGUI dialogTMP;
        public TextMeshProUGUI watermarkTMP;
        public object configMenuObj;

        public string ToHexString_NoTag(Color c) => String.Format("{0:X2}{1:X2}{2:X2}", (int)(c.r * 255), (int)(c.g * 255), (int)(c.b * 255));
        public Color FromHexString_NoTag(string hex) => new Color(
            int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f);

        #region Unity Methods
        private void Start()
        {
            // Load assemblies and methods from them
            yargMainAsm = Assembly.Load("Assembly-CSharp.dll");
            yargCoreAsm = Assembly.Load("YARG.Core.dll");
            gvType = yargMainAsm.GetType("YARG.GlobalVariables");
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
            dialogColorMethod = dmType.GetMethod("ShowColorPickerDialog");
            toastMethod_Info = tmType.GetMethod("ToastInformation");
            toastMethod_Warn = tmType.GetMethod("ToastWarning");

            inGame = false;
            songFailed = false;

            cfgEnableMeter = Config.Bind<bool>("Rock Meter", "EnableRockMeter", true,
                "Enable or disable the Rock Meter entirely");
            meterEnabled = cfgEnableMeter.Value;

            cfgRestartFail = Config.Bind<bool>("Rock Meter", "RestartOnFail", true,
                "Control whether to force restart on fail.  If false, then gameplay will not be interrupted on fail.");
            restartOnFail = cfgRestartFail.Value;

            cfgShowComboMeter = Config.Bind<bool>("Combo Meter", "EnableComboMeter", true,
                "Enable or disable the combo meter");
            showCombo = cfgShowComboMeter.Value;

            // Combo Meter colors
            comboMeterColor = new Color(0.27f, 0.27f, 0.27f);
            comboEdgeColor  = new Color(0.5f, 0.5f, 0.5f);
            comboTextColor  = new Color(1f, 1f, 1f);
            comboMeterRGB = ToHexString_NoTag(comboMeterColor);
            comboEdgeRGB = ToHexString_NoTag(comboEdgeColor);
            comboTextRGB = ToHexString_NoTag(comboTextColor);
            cfgComboMeterColorHex = Config.Bind<string>("Combo Meter", "ComboMeterColor",
                comboMeterRGB, "RGB hex color of the combo meter");
            cfgComboEdgeColorHex  = Config.Bind<string>("Combo Meter", "MeterEdgeColor",
                comboEdgeRGB, "RGB hex color of the edge of the streak counter");
            cfgComboTextColorHex  = Config.Bind<string>("Combo Meter", "ComboTextColor",
                comboTextRGB, "RGB hex color of the note streak text");

            // TO DO: Maybe add config options for these values?
            missDrainAmount = 0.0277;
            hitHealthAmount = 0.0069;  // Nice
            hitSPGainAmount = 0.045;

            meterPosX = Screen.width * 0.875f;
            meterPosY = Screen.height * 0.6f;
            comboPosX = meterPosX;
            comboPosY = Screen.height * 0.53f;
            cfgMeterX = Config.Bind<int>("Rock Meter", "MeterPosition_X", (int)meterPosX);
            cfgMeterY = Config.Bind<int>("Rock Meter", "MeterPosition_Y", (int)meterPosY);
            cfgComboX = Config.Bind<int>("Combo Meter", "ComboPosition_X", (int)comboPosX);
            cfgComboY = Config.Bind<int>("Combo Meter", "ComboPosition_Y", (int)comboPosY);
            // refresh the values
            meterPosX = cfgMeterX.Value;
            meterPosY = cfgMeterY.Value;
            comboPosX = cfgComboX.Value;
            comboPosY = cfgComboY.Value;

            isDraggingHealth = false;
            isMouseDown = false;
            isLegacyComboMeter = false;
            isConfigShowing = false;
            doOpenConfig = false;

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

                    // get meter positions
                    meterPosX = cfgMeterX.Value;
                    meterPosY = cfgMeterY.Value;
                    comboPosX = cfgComboX.Value;
                    comboPosY = cfgComboY.Value;

                    // get meter colors
                    comboMeterColor = FromHexString_NoTag(cfgComboMeterColorHex.GetSerializedValue());
                    comboEdgeColor = FromHexString_NoTag(cfgComboEdgeColorHex.GetSerializedValue());
                    comboTextColor = FromHexString_NoTag(cfgComboTextColorHex.GetSerializedValue());
                }
            };
            SceneManager.sceneUnloaded += delegate (Scene sc)
            {
                if (sc.name == "Gameplay")
                {
                    doOpenConfig = false;
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
            if (watermarkTMP == null)
            {
                watermarkTMP = FindAndSetActive("Watermark Container").GetComponentInChildren<TextMeshProUGUI>();
                if (watermarkTMP.text.Contains("YARG v1.22.33b")) // Detect if we're on the Stable Build
                {
                    gvo = GameObject.Find("Global Variables");
                    string versionTxt = (string)gvType.GetField("CURRENT_VERSION").GetValue(gvo.GetComponent("YARG.GlobalVariables"));
                    watermarkTMP.text = String.Format("<b>YARG {0}</b>", versionTxt);
                }
                watermarkTMP.text = String.Format("<link=\"RockMeterConfig\"><color=#33FFFF>Rock Meter v{0}</color></link>  •  ", VersionString) + watermarkTMP.text;
                
            }
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
                    dbgTxtTMP.text += "<br>Dragging meter: " + isDraggingHealth.ToString();
                    dbgTxtTMP.text += "<br>Rock Meter:   " + Math.Round(currentHealth * 100).ToString() + "%";
                }
#endif
                if (comboTxt != null)
                {
                    if (comboTxt.text == null)  // Initialize streak counter if it isn't already
                    {
                        SetComboMeter(0);
                    }
                }
            }

            if (inGame && Mouse.current.leftButton.isPressed)
            {
                if (!isMouseDown)
                {
                    MouseDownHandle();
                    isMouseDown = true;
                }
                if (isDraggingHealth || isDraggingCombo) MouseDragHandle();
            }
            if (!Mouse.current.leftButton.isPressed && isMouseDown)
            {
                MouseUpHandle();

                isDraggingHealth = false;
                isDraggingCombo = false;
                isMouseDown = false;
            }
            if (inGame && Mouse.current.rightButton.isPressed)
            {
                SetHealthMeterPos(Screen.width * 0.875f, Screen.height * 0.6f);
                SetComboPos(Screen.width * 0.875f, Screen.height * 0.53f);
                isDraggingHealth = false;
                isMouseDown = false;
                UpdateConfig();
            }
            
            if (!inGame && Mouse.current.leftButton.isPressed && !isMouseDown)
            {
                int linkIndex = TMP_TextUtilities.FindIntersectingLink(watermarkTMP, Mouse.current.position.ReadValue(), Camera.main);
                configMenuTMP = GameObject.Find("Persistent Canvas/Dialog Container/MessageDialog(Clone)/Base/Content/Message")?.GetComponent<TextMeshProUGUI>();
                if (isConfigShowing && configMenuTMP != null)
                {
                    linkIndex = TMP_TextUtilities.FindIntersectingLink(configMenuTMP, Mouse.current.position.ReadValue(), Camera.main);
                    if (linkIndex != -1)
                    {
                        linkID = configMenuTMP.textInfo.linkInfo[linkIndex].GetLinkID();
                        isMouseDown = true;
                    }
                }
                else if (linkIndex != -1)
                {
                    linkID = watermarkTMP.textInfo.linkInfo[linkIndex].GetLinkID();
                    isMouseDown = true;
                }
            }
            if (!inGame && !Mouse.current.leftButton.isPressed && isMouseDown && !doOpenConfig)
            {
                MouseUpHandle();
            }
            if (isConfigShowing)
            {
                if (configMenuTMP == null)  // called when config menu is closed
                {
                    isConfigShowing = false;
                }
            }
            else if (doOpenConfig)
            {
                configMenuObj = OpenConfigMenu();
            }
        }
        #endregion
    }
}
