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
using IniParser.Parser;
using IniParser.Model;
using YARG.Core.Engine.Vocals;
using YARG.Core.Chart;

namespace RockMeterYARG
{
    [HarmonyPatch(typeName: "YARG.Core.Engine.Guitar.GuitarEngine", methodName: "MissNote")]
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
    [HarmonyPatch(typeName: "YARG.Core.Engine.Vocals.VocalsEngine", methodName: "MissNote", methodType: MethodType.Normal, argumentTypes: new Type[] {typeof(VocalNote), typeof(double)})]
    public class VoxMissHandler
    {
        static void Postfix(ref object __instance, ref object __0, ref object __1)
        {
            if (__1 != null)
            {
                if (__0.GetType() == typeof(VocalNote) && __1.GetType() == typeof(double))
                    RockMeterYARG.Instance.DrainHealth(__instance, "Missed Phrase", (double)__1);
            }
        }
    }
    [HarmonyPatch(typeName: "YARG.Core.Engine.Vocals.VocalsEngine", methodName: "HitNote")]
    public class VoxHitHandler
    {
        static void Postfix(ref object __instance)
        {
            RockMeterYARG.Instance.AddHealth(__instance, true);
        }
    }
    [HarmonyPatch(typeName: "YARG.Gameplay.GameManager", methodName: "CreatePlayers")]
    public class OnGameLoad
    {
        [HarmonyPostfix]
        static void Postfix()
        {
            RockMeterYARG.Instance.InitMeters();
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

        private const string MyGUID          = "com.yoshibyl.RockMeterYARG";
        private const string PluginName      = "RockMeterYARG";
        private const string VersionString   = "0.7.0";
        public const string ProperVersion    = "0.7.0.0";

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
        // 5-Fret and Drums values
        public double missDrainAmount;  // Amount to take from health on note miss or overstrum/overhit
        public double hitHealthAmount;  // Amount to add to health on note hit usually
        public double hitSPGainAmount;  // Amount to add to health on note hit during Star Power
        // Vocals values
        public double missDrainAmount_Vox;
        public double hitHealthAmount_Vox;
        public double hitSPGainAmount_Vox;

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
        public bool dragBothMeters;

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
        public Type feType;             // File explorer helper

        public MethodInfo quitMethod;
        public MethodInfo restartMethod;
        public MethodInfo pauseMethod;
        public MethodInfo dialogShowMethod;
        public MethodInfo dialogClearMethod;
        public MethodInfo dialogColorMethod;
        public MethodInfo dialogTextMethod;
        public MethodInfo openFileMethod;
        public MethodInfo openFolderMethod;
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
        
        public Color ChangeColor(string colorSetting, Color value)
        {
            switch(colorSetting)
            {
                case "ComboTextColor":
                    comboTextColor = value;
                    comboTextRGB = ColorToHexString_NoTag(value);
                    cfgComboTextColorHex.SetSerializedValue(comboTextRGB);
                    break;
                case "ComboEdgeColor":
                    comboEdgeColor = value;
                    comboEdgeRGB = ColorToHexString_NoTag(value);
                    cfgComboEdgeColorHex.SetSerializedValue(comboEdgeRGB);
                    break;
                case "ComboMeterColor":
                    comboMeterColor = value;
                    comboMeterRGB = ColorToHexString_NoTag(value);
                    cfgComboMeterColorHex.SetSerializedValue(comboMeterRGB);
                    break;
                case "RockMeterBaseColor":
                    rockMeterColor = value;
                    rockMeterRGB = ColorToHexString_NoTag(value);
                    cfgRockMeterColorHex.SetSerializedValue(rockMeterRGB);
                    break;
                case "RockMeterOverlayColor":
                    rockOverlayColor = value;
                    rockOverlayRGB = ColorToHexString_NoTag(value);
                    cfgRockMeterColorHex.SetSerializedValue(rockOverlayRGB);
                    break;
                default:
                    break;
            }
            UpdateConfig();
            return value;
        }
        public string ChangeText(string textSetting, string value)
        {
            switch (textSetting)
            {
                case "Theme":
                    if (value.IsNullOrWhiteSpace() && value != null)
                    {
                        value = "default";
                    }
                    selectedTheme = value;
                    break;
                default:
                    break;
            }
            UpdateConfig();
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
            configInputShowing = true;
            if (dmo != null)
            {
                dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null);
                Action<Color> action = delegate (Color _)
                {
                    ChangeColor(colorSetting, _);
                };
                List<object> argz = new List<object> { c, action };
                ret = dialogColorMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), argz.ToArray());
                currentDialog = ret;
                return ret;
            }
            else
            {
                return null;
            }
        }
        public object ShowTextInputDialog(string textSetting, string t)
        {
            dmo = FindAndSetActive("Dialog Container");
            object ret = null;
            configInputShowing = true;
            if (dmo != null)
            {
                dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null);
                Action<string> action = delegate (string _)
                {
                    ChangeText(textSetting, _);
                };
                List<object> argz = new List<object> { t, action };
                ret = dialogTextMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), argz.ToArray());
                currentDialog = ret;
                return ret;
            }
            else
            {
                return null;
            }
        }
        public string GetThemePath(string dir)
        {
            if (File.Exists(Path.Combine(dir, "theme.ini")))
            {
                selectedTheme = dir;
                selectedThemeName = new DirectoryInfo(dir).Name;

                try 
                {
                    IniData themeData = ParseThemeIni(Path.Combine(selectedTheme, "theme.ini"));
                    if (themeData["Meta"].ContainsKey("theme_name"))
                        selectedThemeName = themeData["Meta"]["theme_name"];
                    if (themeData["Meta"].ContainsKey("creator"))
                        themeCreator = themeData["Meta"]["creator"];
                } catch(Exception e) { LogMsg(e); }

                if (dialogBoxTMP != null) dialogBoxTMP.text = RefreshConfigMenuText();
                UpdateConfig();
                return dir;
            }
            else
            {
                selectedTheme = Path.Combine(Paths.PluginPath, "assets", "themes", "default");
                if (!Directory.Exists(selectedTheme)) selectedTheme = Path.Combine(Paths.PluginPath, "assets");
                selectedThemeName = "Default";
                themeCreator = "";
                UpdateConfig();
                return selectedTheme;
            }
        }
        public void OpenThemeFolder(string startPath = "DEFAULT_PATH")
        {
            if (startPath == "DEFAULT_PATH")
                startPath = Path.Combine(Paths.PluginPath, "assets", "themes");
            Action<string> action = delegate (string _)
            {
                GetThemePath(_);
            };
            openFolderMethod.Invoke(null, new object[] { startPath, action });
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
                IReadOnlyList<object> rawList = (IReadOnlyList<object>)gmType.GetProperty(listProperty).GetValue(gmo.GetComponent("YARG.Gameplay.GameManager"));

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
            if (dmo != null)
            {
                if (!inGame) dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null);
                List<string> argz = new List<string> { title, msg };
                object ret = dialogShowMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), argz.ToArray());
                return ret;
            }
            else
            {
                return null;
            }
        }
        
        public void FailSong(BaseStats s, string causeOfFail = "", object extraDetails = null, int index = -1)
        {
            statsList = GetAllStats();
            index = GetPlayerIndexFromStats(s);
            restartOnFail = cfgRestartFail.Value;
            List<string> validCauses = new List<string> { "Missed Note", "Overstrum", "Overhit", "Missed Phrase"};
            if (meterEnabled && !practice)
            {
                songFailed = true;
                if (restartOnFail && statsList.Count == 1 && validCauses.Contains(causeOfFail))
                {
                    try
                    {
                        if (s is GuitarStats || s is DrumsStats)
                        {
                            score = s.CommittedScore + s.PendingScore + s.SoloBonuses;
                            notesHit = s.NotesHit;
                            notesTotal = s.TotalNotes;
                            maxStreak = s.MaxCombo;
                            msgSummary = "<size=24>(" + causeOfFail + ")</size><br><size=36><br><b><u>Stats</u></b></size><br><align=left>"
                                + "<br><pos=35%>Score:<pos=55%>"
                                + score.ToString("N0")
                                + "<br><pos=35%>Notes Hit:<pos=55%>" + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0")
                                + "<br><pos=35%>Best Streak:<pos=55%>" + maxStreak.ToString("N0")
                                + "<br><pos=35%>SP Phrases:<pos=55%>" + s.StarPowerPhrasesHit + " / " + s.TotalStarPowerPhrases;
                            if (s is GuitarStats)
                            {
                                overstrums = (s as GuitarStats).Overstrums;
                                ghosts = (s as GuitarStats).GhostInputs;
                                msgSummary = msgSummary.Replace("<u>Stats</u>", "<u>Stats (Guitar)</u>");
                                msgSummary += "<br><pos=35%>Overstrums:<pos=55%>" + overstrums.ToString("N0")
                                   + "<br><pos=35%>Ghost Inputs:<pos=55%>" + ghosts.ToString("N0");
                            }
                            else if (s.GetType().ToString() == "YARG.Core.Engine.Drums.DrumsStats")
                            {
                                overhits = (s as DrumsStats).Overhits;
                                msgSummary = msgSummary.Replace("<u>Stats</u>", "<u>Stats (Drums)</u>");
                                msgSummary += "<br><pos=35%>Overhits:<pos=55%>" + overhits.ToString("N0") + "<br>";
                            }
                            msgSummary += "<br></align><br><size=36><b>Press PAUSE/Esc to restart.</b></size>";
                        }
                        else if (s is VocalsStats)
                        {
                            VocalsStats voxStats = s as VocalsStats;
                            score = voxStats.CommittedScore + voxStats.PendingScore + voxStats.SoloBonuses;
                            notesHit = voxStats.NotesHit;
                            notesTotal = voxStats.TotalNotes;
                            maxStreak = voxStats.MaxCombo;
                            msgSummary = "<size=24>(" + causeOfFail + ")</size><br><size=36><br><b><u>Stats (Vocals)</u></b></size><br><align=left>"
                                + "<br><pos=35%>Score:<pos=55%>"
                                + score.ToString("N0")
                                + "<br><pos=35%>Phrases Hit:<pos=55%>" + notesHit.ToString("N0") + " / " + notesTotal.ToString("N0")
                                + "<br><pos=35%>Best Streak:<pos=55%>" + maxStreak.ToString("N0")
                                + "<br><pos=35%>SP Phrases:<pos=55%>" + voxStats.StarPowerPhrasesHit + " / " + voxStats.TotalStarPowerPhrases;

                            msgSummary += "</align><br><br><br><br><size=36><b>Press PAUSE/Esc to restart.</b></size>";
                        }
                        else if (s == null)
                        {
                            msgSummary = "Error";
                            RestartSong(true);
                        }
                        RestartSong(true, causeOfFail);
                    } catch(Exception e) { LogMsg(e); }
                }
                else if (restartOnFail && statsList.Count == 1)
                {
                    msgSummary = "";
                    if (causeOfFail == "")
                    {
                        msgSummary = "<size=24>(Unknown cause, may be a bug)</size><br><br>This is a bug.  Let the Rock Meter mod dev(s) know, <i>NOT YARC!!</i>";
                    }
                    else
                    {
                        msgSummary += String.Format("<size=24>(Custom Failure: {0})</size><br><size=36><br>", causeOfFail);
                        msgSummary += extraDetails;
                    }
                    RestartSong(true, causeOfFail);
                }
                else
                {
                    // Make rock meter darker instead of destroying the entire container
                    Destroy(needleObj);
                    if (rockMeterImg != null) rockMeterImg.color = new Color(0.05f, 0.05f, 0.05f, 1);
                    if (rockRygImg != null) rockRygImg.color = new Color(0.2f, 0.2f, 0.2f, 1);
                    if (rockOverlayImg != null) rockOverlayImg.color = new Color(0.05f, 0.05f, 0.05f, 1);
                    
                    if (extraDetails == null) extraDetails = "";
                    if (index != -1)
                        ToastInfo(String.Format("Player {0} failed!<br>({1})", index + 1, causeOfFail));
                    else ToastInfo(String.Format("Song failed!<br>({0}){1}", causeOfFail, extraDetails.ToString()));
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
                inGame = true;
                
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
            UnityEngine.Object[] objs = Resources.FindObjectsOfTypeAll(typeof(GameObject));
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
                    GameObject button = GameObject.Find("Persistent Canvas/Dialog Container/MessageDialog(Clone)/Base/Dialog Button Container");
                    button.SetActive(false);

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
            isMouseDown = false;
            currentDialog = null;
        }
        
        public void DrainHealth(object invoker, string cause = "", double drainAmount = -1, string extra = "")
        {
            SetComboMeter(GetBandCombo());
            double oldHealth = currentHealth;
            if (!practice && !replay)
            {
                BaseStats stats = null;
                if (invoker != null)
                {
                    if (invoker.GetType().Name.EndsWith("Player"))
                    {
                        stats = (BaseStats)basePlayerType.GetProperty("BaseStats").GetValue(invoker);
                    }
                    else if (invoker.GetType().Name.EndsWith("Engine"))
                    {
                        stats = (BaseStats)(typeof(BaseEngine).GetProperty("BaseStats").GetValue(invoker));
                    }
                }
                
                if (cause == "Missed Phrase" && drainAmount >= 0)
                    currentHealth -= (1 - drainAmount) * missDrainAmount_Vox;
                else if (cause != "Missed Phrase" && drainAmount >= 0)
                {
                    currentHealth -= drainAmount;
                    if (cause == "") cause = "DEBUG";
                }
                else currentHealth -= missDrainAmount;
                if (currentHealth < 0) { currentHealth = 0; }
                if (currentHealth != oldHealth)
                {
                    UpdateHealthMeter(currentHealth);

                    if (currentHealth <= 0 && !songFailed)
                    {
                        if (cause == "DEBUG") FailSong(stats, cause, extra.ToString());
                        else FailSong(stats, cause);
                    }
                }
            }
        }
        public void AddHealth(object invoker, bool vox = false)
        {
            SetComboMeter(GetBandCombo());
            // LogMsg(invoker);
            double oldHealth = currentHealth;
            if (!practice && !replay && currentHealth < 1 && invoker != null)
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
                    if (stats.IsStarPowerActive)
                    {
                        if (vox) currentHealth += hitSPGainAmount_Vox;
                        else currentHealth += hitSPGainAmount;
                    }
                    else
                    {
                        if (vox) currentHealth += hitHealthAmount_Vox;
                        else currentHealth += hitHealthAmount;
                    }
                }
            }
            if (currentHealth > 1) { currentHealth = 1; }
            if (currentHealth != oldHealth) { UpdateHealthMeter(currentHealth); }
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
                IEnumerable<UnityEngine.GameObject> players = GetPlayerObjects();
                foreach (GameObject player in players)
                {
                    var guitarComp = player.GetComponent("YARG.Gameplay.Player.FiveFretPlayer");
                    var drumComp = player.GetComponent("YARG.Gameplay.Player.DrumsPlayer");
                    var voxComp = player.GetComponent("YARG.Gameplay.Player.VocalsPlayer");
                    if (guitarComp != null)
                        ret.Add(basePlayerType.GetProperty("BaseStats").GetValue(guitarComp) as BaseStats);
                    else if (drumComp != null)
                    {
                        ret.Add(basePlayerType.GetProperty("BaseStats").GetValue(drumComp) as BaseStats);
                    }
                    else if (voxComp != null)
                        ret.Add(basePlayerType.GetProperty("BaseStats").GetValue(voxComp) as BaseStats);
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
        public int GetPlayerIndexFromStats(BaseStats s)
        {
            if (statsList == null) statsList = GetAllStats();
            if (statsList != null)
            {
                int i = 0;
                foreach (BaseStats bs in statsList)
                {
                    if (bs == s)
                    {
                        return i;
                    }
                    i++;
                }
            }
            return -1;
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
            try
            {
                Texture2D textur = new Texture2D(512, 512);
                if (File.Exists(filePath))
                {
                    byte[] array = File.ReadAllBytes(filePath);
                    textur.LoadImage(array);
                    return textur;
                }
            }
            catch { }
            return null;
        }

        public void SetHealthNeedle(float health)
        {
            if (needleObj != null)
            {
                float kurtAngle = -2f * health * needleAngle + needleAngle + 270f;  // quick maths
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
            if (rockContainer != null)
                rockContainer.transform.position = new Vector3(x,y,0);
            meterPosX = x;
            meterPosY = y;
        }

        public string SetComboMeter(int streak = 0)
        {
            if (maxDigitsCombo > 9) maxDigitsCombo = 9;
            else if (maxDigitsCombo < 4) maxDigitsCombo = 4;
            int maxStreak = (int)Math.Pow(10, maxDigitsCombo);
            if (streak >= maxStreak) streak = maxStreak - 1;
            string comboColor = comboTextRGB;
            if (!enableComboColors) comboColor = defaultComboTxtRGB;
            if (comboTxt != null)
            {
                string comboStr = streak.ToString(new string('0', maxDigitsCombo)); // Repeat '0' max_digits number of times
                int streakThreshold = 30;

                if (statsList.Count > 0)
                {
                    if (statsList[0] is VocalsStats) streakThreshold = 1;
                }
                if (statsList.Count > 1) streakThreshold = 1;

                if (streak >= streakThreshold) comboStr = comboStr.Replace(streak.ToString(), "</color><color=#" + comboColor + ">" + streak);
                
                comboTxt.text = "<size=20><align=center><mspace=0.62em><color=#00000000>" + comboStr;

                if (comboTxt.verticalAlignment != VerticalAlignmentOptions.Middle) comboTxt.verticalAlignment = VerticalAlignmentOptions.Middle;
                return comboTxt.text;
            }
            else return null;
        }
        public void SetComboPos(float x, float y)
        {
            if (x > Screen.width || x < 0 || y > Screen.height || y < 0)
            { // Check if the meter is off-screen
                x = Screen.width * 0.875f;
                y = Screen.height * 0.53f;
            }
            if (comboContainer != null)
                comboContainer.transform.position = new Vector3(x, y, 0);
            comboPosX = x;
            comboPosY = y;
        }
        public void RefreshColors()
        {
            rockMeterRGB = cfgRockMeterColorHex.GetSerializedValue().ToUpper();
            rockOverlayRGB = cfgRockOverlayColorHex.GetSerializedValue().ToUpper();
            comboMeterRGB = cfgComboMeterColorHex.GetSerializedValue().ToUpper();
            comboEdgeRGB = cfgComboEdgeColorHex.GetSerializedValue().ToUpper();
            comboTextRGB = cfgComboTextColorHex.GetSerializedValue().ToUpper();

            rockMeterColor = ColorFromHexString_NoTag(rockMeterRGB);
            rockOverlayColor = ColorFromHexString_NoTag(rockOverlayRGB);
            comboMeterColor = ColorFromHexString_NoTag(comboMeterRGB);
            comboEdgeColor = ColorFromHexString_NoTag(comboEdgeRGB);
            comboTextColor = ColorFromHexString_NoTag(comboTextRGB);
        }

        public void UpdateConfig()
        {
            cfgMeterX.SetSerializedValue(((int)meterPosX).ToString());
            cfgMeterY.SetSerializedValue(((int)meterPosY).ToString());
            cfgEnableMeter.SetSerializedValue(meterEnabled.ToString());
            cfgRestartFail.SetSerializedValue(restartOnFail.ToString());
            cfgComboX.SetSerializedValue(((int)comboPosX).ToString());
            cfgComboY.SetSerializedValue(((int)comboPosY).ToString());
            cfgRockMeterColorHex.SetSerializedValue(rockMeterRGB);
            cfgRockOverlayColorHex.SetSerializedValue(rockOverlayRGB);
            cfgComboTextColorHex.SetSerializedValue(comboTextRGB);
            cfgComboEdgeColorHex.SetSerializedValue(comboEdgeRGB);
            cfgComboMeterColorHex.SetSerializedValue(comboMeterRGB);
            cfgThemePath.SetSerializedValue(selectedTheme);
        }

        public string selectedTheme;        // default is "default"
        public string selectedThemeName;    // [Meta] theme_name
        public string themeCreator;         // [Meta] creator
        public string themeDescription;     // [Meta] description
        public float needleAngle;           // [Rock Meter] max_needle_angle (88 degrees)
        public float healthMeterScale;      // [Rock Meter] health_scale (2.0f)
        public float comboScale;            // [Combo Meter] combo_scale (2.0f)
        public int maxDigitsCombo;
        public bool enableComboColors;
        public string defaultComboTxtRGB;

        public IniData ParseThemeIni(string path)
        {
            IniData r = null;
            if (File.Exists(path))
            {
                var parser = new IniDataParser();
                StreamReader iniFile = File.OpenText(path);
                string iniFileData = iniFile.ReadToEnd();
                iniFile.Close();
                r = parser.Parse(iniFileData);
            }
            return r;
        }


        public void InitMeters()
        {
            IniData ini = ParseThemeIni(Path.Combine(selectedTheme, "theme.ini"));
            bool forceBasicCombo = false;
            bool forceBasicHealth = false;
            bool enableHealthColors = false;    // Implement Rock Meter colors later?
            if (ini != null)
            {
                float.TryParse(ini["Rock Meter"]?.GetKeyData("health_scale")?.Value, out healthMeterScale);
                float.TryParse(ini["Rock Meter"]?.GetKeyData("max_needle_angle")?.Value, out needleAngle);
                float.TryParse(ini["Combo Meter"]?.GetKeyData("combo_scale")?.Value, out comboScale);
                int.TryParse(ini["Combo Meter"]?.GetKeyData("max_digits")?.Value, out maxDigitsCombo);
                forceBasicCombo = ini["Combo Meter"]?.GetKeyData("force_basic_combometer")?.Value.ToLower() == "true";
                forceBasicHealth = ini["Rock Meter"]?.GetKeyData("force_basic_healthmeter")?.Value.ToLower() == "true";
                bool.TryParse(ini["Colors"]?.GetKeyData("enable_combo_color")?.Value, out enableComboColors);
                bool.TryParse(ini["Colors"]?.GetKeyData("enable_health_color")?.Value, out enableHealthColors);
                defaultComboTxtRGB = ini["Colors"]?.GetKeyData("default_combo_text")?.Value;
            }

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

            if (hudObj != null && rockContainer == null)
            {
                bool findingAssets = true;
                int iterations = 1;

                string meterAsset       = Path.Combine(Paths.PluginPath, "assets/meter.png");
                string rockBaseAsset	= Path.Combine(Paths.PluginPath, "assets/health_meter_base.png");
                string rockRygAsset     = Path.Combine(Paths.PluginPath, "assets/health_meter_ryg.png");
                string rockOverlayAsset	= Path.Combine(Paths.PluginPath, "assets/health_meter_overlay.png");
                string needleAsset      = Path.Combine(Paths.PluginPath, "assets/needle.png");
                string comboBgAsset     = Path.Combine(Paths.PluginPath, "assets/combo_bg.png");
                string comboEdgeAsset   = Path.Combine(Paths.PluginPath, "assets/combo_edge.png");
                string comboAsset       = Path.Combine(Paths.PluginPath, "assets/combo_meter.png");

                string themed_meterAsset		 = meterAsset		;
                string themed_rockBaseAsset		 = rockBaseAsset	;
                string themed_rockRygAsset       = rockRygAsset     ;
                string themed_rockOverlayAsset	 = rockOverlayAsset ;
                string themed_needleAsset		 = needleAsset      ;
                string themed_comboBgAsset		 = comboBgAsset     ;
                string themed_comboEdgeAsset	 = comboEdgeAsset   ;
                string themed_comboAsset		 = comboAsset       ;

                if (Directory.Exists(selectedTheme))
                {
                    themed_meterAsset = Path.Combine(selectedTheme, "meter.png");
                    themed_needleAsset = Path.Combine(selectedTheme, "needle.png");
                    themed_rockBaseAsset = Path.Combine(selectedTheme, "health_meter_base.png");
                    themed_rockRygAsset = Path.Combine(selectedTheme, "health_meter_ryg.png");
                    themed_rockOverlayAsset = Path.Combine(selectedTheme, "health_meter_overlay.png");
                    themed_comboBgAsset = Path.Combine(selectedTheme, "combo_bg.png");
                    themed_comboEdgeAsset = Path.Combine(selectedTheme, "combo_edge.png");
                    themed_comboAsset = Path.Combine(selectedTheme, "combo_meter.png");
                }

                rockContainer = new GameObject("Health Meter Container");
                rockContainer.transform.SetParent(hudObj.transform, false);
                rockContainer.transform.localScale = new Vector3(healthMeterScale * 2, healthMeterScale * 2, 1);
                
                if (meterEnabled && !practice && !replay)
                {
                    rockMeterObj = new GameObject("Health Meter");
                    rockMeterObj.transform.SetParent(rockContainer.transform, false);
                    while (findingAssets)
                    {
                        if (!File.Exists(themed_needleAsset))
                            themed_needleAsset = needleAsset;
                        if (!File.Exists(themed_rockBaseAsset))
                            themed_rockBaseAsset = rockBaseAsset;
                        if (File.Exists(themed_rockBaseAsset) && File.Exists(themed_needleAsset))
                        {
                            rockMeterImg = rockMeterObj.AddComponent<RawImage>();
                            rockMeterImg.texture = LoadPNG(themed_rockBaseAsset);
                            if (enableHealthColors) rockMeterImg.color = rockMeterColor;
                            if (!File.Exists(themed_rockOverlayAsset))
                                themed_rockOverlayAsset = rockOverlayAsset;
                            if (!File.Exists(themed_rockRygAsset))
                                themed_rockRygAsset = rockRygAsset;
                            if (File.Exists(themed_rockRygAsset) && !forceBasicHealth)
                            {
                                rockRygObj = new GameObject("Health Zones");
                                rockRygObj.transform.SetParent(rockContainer.transform, false);
                                rockRygImg = rockRygObj.AddComponent<RawImage>();
                                rockRygImg.texture = LoadPNG(themed_rockRygAsset);
                            }
                            needleObj = new GameObject("Health Needle");
                            needleObj.transform.SetParent(rockContainer.transform, false);
                            needleImg = needleObj.AddComponent<RawImage>();
                            needleImg.texture = LoadPNG(themed_needleAsset);
                            if (File.Exists(themed_rockOverlayAsset))
                            {
                                rockOverlayObj = new GameObject("Health Overlay");
                                rockOverlayObj.transform.SetParent(rockContainer.transform, false);
                                rockOverlayImg = rockOverlayObj.AddComponent<RawImage>();
                                rockOverlayImg.texture = LoadPNG(themed_rockOverlayAsset);
                                if (enableHealthColors) rockOverlayImg.color = rockOverlayColor;
                            }
                            if (rockMeterImg.texture == null || needleImg.texture == null)
                            {
                                ToastWarning("Couldn't properly load assets for the rock meter.");
                            }
                            findingAssets = false;
                        }
                        else if (File.Exists(themed_meterAsset) && File.Exists(themed_needleAsset))
                        {
                            rockMeterImg = rockMeterObj.AddComponent<RawImage>();
                            rockMeterImg.texture = LoadPNG(themed_meterAsset);
                            needleObj = new GameObject("Health Needle");
                            needleObj.transform.SetParent(rockContainer.transform, false);
                            needleImg = needleObj.AddComponent<RawImage>();
                            needleImg.texture = LoadPNG(themed_needleAsset);
                            if (File.Exists(themed_rockOverlayAsset))
                            {
                                rockOverlayObj = new GameObject("Health Overlay");
                                rockOverlayObj.transform.SetParent(rockContainer.transform, false);
                                rockOverlayImg = rockOverlayObj.AddComponent<RawImage>();
                                rockOverlayImg.texture = LoadPNG(themed_rockOverlayAsset);
                                if (enableHealthColors) rockOverlayImg.color = rockOverlayColor;
                            }
                            if (rockMeterImg.texture == null || needleImg.texture == null)
                            {
                                ToastWarning("Couldn't properly load assets for the rock meter.");
                            }
                            findingAssets = false;
                        }
                        else if (File.Exists(meterAsset) && File.Exists(needleAsset))
                        {
                            rockMeterImg = rockMeterObj.AddComponent<RawImage>();
                            needleImg = needleObj.AddComponent<RawImage>();
                            rockMeterImg.texture = LoadPNG(themed_meterAsset);
                            needleImg.texture = LoadPNG(themed_needleAsset);
                            if (rockMeterImg.texture == null || needleImg.texture == null)
                            {
                                ToastWarning("Couldn't properly load assets for the rock meter.");
                            }
                            findingAssets = false;
                        }
                        else if (iterations >= 3 && findingAssets)
                        {
                            ToastWarning("Couldn't find assets for the rock meter.");
                            findingAssets = false;
                        }
                        if (iterations >= 2 || forceBasicHealth)
                        {
                            if (!File.Exists(themed_meterAsset))
                            {
                                themed_meterAsset = meterAsset;
                                forceBasicHealth = true;
                            }
                        }
                        iterations++;
                    }
                    if (needleObj != null) needleObj.transform.eulerAngles = new Vector3(0, 0, -90);
                }

                SetHealthMeterPos(cfgMeterX.Value, cfgMeterY.Value);

                // Initialize combo meter, if enabled
                if (showCombo)
                {
                    comboContainer = new GameObject("Streak Counter Container");
                    comboContainer.transform.SetParent(hudObj.transform, false);
                    comboContainer.transform.localScale = new Vector3(comboScale * 2, comboScale * 2, 1);

                    findingAssets = true;
                    iterations = 1;
                    while (findingAssets)
                    {
                        LogMsg(themed_comboBgAsset);
                        LogMsg(themed_comboEdgeAsset);
                        LogMsg(themed_comboAsset);
                        if (forceBasicCombo)
                        {
                            comboMeterObj = new GameObject("Streak Background");
                            comboMeterObj.transform.SetParent(comboContainer.transform, false);
                            comboTxtObj = new GameObject("Streak Text");
                            comboTxtObj.transform.SetParent(comboContainer.transform, false);
                            if (!File.Exists(themed_comboAsset))
                                themed_comboAsset = comboAsset;
                            
                            comboMeterImg = comboMeterObj.AddComponent<RawImage>();
                            comboMeterImg.texture = LoadPNG(themed_comboAsset);
                            if (enableComboColors)
                                comboMeterImg.color = comboMeterColor;
                            comboTxt = comboTxtObj.AddComponent<TextMeshProUGUI>();

                            SetComboPos(comboPosX, comboPosY);
                            findingAssets = false;
                        }
                        else if (File.Exists(themed_comboBgAsset) && File.Exists(themed_comboEdgeAsset))
                        {
                            comboMeterObj = new GameObject("Streak Background");
                            comboMeterObj.transform.SetParent(comboContainer.transform, false);
                            comboEdgeObj = new GameObject("Streak Counter Edge");
                            comboEdgeObj.transform.SetParent(comboContainer.transform, false);
                            comboTxtObj = new GameObject("Streak Text");
                            comboTxtObj.transform.SetParent(comboContainer.transform, false);

                            comboMeterImg = comboMeterObj.AddComponent<RawImage>();
                            comboMeterImg.texture = LoadPNG(themed_comboBgAsset);
                            comboEdgeImg = comboEdgeObj.AddComponent<RawImage>();
                            comboEdgeImg.texture = LoadPNG(themed_comboEdgeAsset);
                            if (enableComboColors)
                            {
                                comboMeterImg.color = comboMeterColor;
                                comboEdgeImg.color = comboEdgeColor;
                                comboTextColor = ColorFromHexString_NoTag(comboTextRGB);
                            }
                            comboTxt = comboTxtObj.AddComponent<TextMeshProUGUI>();

                            SetComboPos(comboPosX, comboPosY);
                            findingAssets = false;
                        }
                        else if (!File.Exists(comboAsset))
                        {
                            showCombo = false;  // Don't show combo if assets can't be loaded
                            Destroy(comboContainer);
                            findingAssets = false;
                        }
                        themed_comboBgAsset = comboBgAsset;
                        themed_comboEdgeAsset = comboEdgeAsset;
                        themed_comboAsset = comboAsset;

                        if (iterations >= 2)
                            forceBasicCombo = true;
                        if (iterations >= 3)
                        {
                            ToastWarning("Couldn't load assets for streak counter.<br>Please re-download assets from the repo.");
                            findingAssets = false;
                        }

                        iterations++;
                    }
                    
                }
            }
        }

        // rock meter objects
        public GameObject hudObj;
        public GameObject rockContainer;
        public GameObject rockMeterObj;
        public GameObject rockRygObj;
        public GameObject rockOverlayObj;
        public GameObject needleObj;
        public GameObject comboContainer;
        public GameObject comboMeterObj;
        public GameObject comboEdgeObj;
        public GameObject comboTxtObj;
        public TextMeshProUGUI comboTxt;
        public RawImage rockMeterImg;
        public RawImage needleImg;
        public RawImage rockRygImg;
        public RawImage rockOverlayImg;
        public RawImage comboMeterImg;
        public RawImage comboEdgeImg;
        public RectTransform rectMeter;     // Rock Meter bound box
        public RectTransform rectCombo;     // Combo meter bound box
        public bool meterEnabled;

        // Config menu stuff
        public TextMeshProUGUI dialogBoxTMP;
        public bool isConfigShowing;
        public bool doOpenConfig;
        public string OnOffStr(bool boolean, bool colorCodeIt = true)
        {
            string r;
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
        public string RefreshConfigMenuText()
        {
            string options = "<i>Click an option below to configure it!</i><br><br>";
            // The "<color=#00000000>.</color>" is a transparent dot used for breaking up the underlines, otherwise they connect??
            options += "<b><size=30><u>COMBO METER</u><space=14em><color=#00000000>.</color><u>ROCK METER</u></size></b>";
            // Line 1
            options += String.Format("<br><align=left><link=\"ComboMeterToggle\">Enabled:<pos=20%><b>{0}</b></link>", OnOffStr(cfgShowComboMeter.Value));
            options += String.Format("<pos=60%><link=\"RockMeterToggle\">Enabled:<pos=80%><b>{0}</b></link>", OnOffStr(meterEnabled));
            // Line 2
            options += String.Format("<br><link=\"ComboMeterColor\">Meter Color:<pos=20%><b>#{0}<pos=40%>(<color=#{0}><mspace=0.5em>██</mspace></color>)</b></link>", comboMeterRGB);
            options += String.Format("<pos=60%><link=\"SongFailToggle\">Restart on Fail:<pos=80%><b>{0}</b></link>", OnOffStr(restartOnFail));
            // Line 3
            options += String.Format("<br><link=\"ComboTextColor\">Text Color:<pos=20%><b>#{0}<pos=40%>(<color=#{0}><mspace=0.5em>██</mspace></color>)</b></link>", comboTextRGB);
            options += String.Format("<pos=60%><link=\"RockMeterBaseColor\">Meter Color:<pos=77%><b>#{0}<pos=90%>(<color=#{0}><mspace=0.5em>██</mspace></color>)</b></link>", rockMeterRGB);
            // Line 4
            options += String.Format("<br><link=\"ComboEdgeColor\">Edge Color:<pos=20%><b>#{0}<pos=40%>(<color=#{0}><mspace=0.5em>██</mspace></color>)</b></link>", comboEdgeRGB);
            options += String.Format("<pos=60%><link=\"RockMeterOverlayColor\">Overlay Color:<pos=77%><b>#{0}<pos=90%>(<color=#{0}><mspace=0.5em>██</mspace></color>)</b></link>", rockOverlayRGB);
            // Line 5, etc.
            options += String.Format("<br><br><link=\"ResetComboColors\"><color=#FF6666><b>RESET COMBO METER COLORS</b></color></link>");
            options += String.Format("<pos=60%><link=\"ResetHealthColors\"><color=#FF6666><b>RESET ROCK METER COLOR</b></color></link>");
            options += String.Format("<br><br><size=24><link=\"SelectTheme\"><align=center>Selected Theme: {0} <size=21>by {1}</size><br><b><u>BROWSE FOR THEME</u></b></link></size>", selectedThemeName, themeCreator);
            return options;
        }
        public object configSubmenu;
        public object OpenConfigMenu()
        {
            object r = null;
            RefreshColors();
            r = Dialog("Rock Meter Config : " + VersionString, RefreshConfigMenuText());
            dialogBoxTMP = GameObject.Find("Persistent Canvas/Dialog Container/MessageDialog(Clone)/Base/Content/Message")?.GetComponent<TextMeshProUGUI>();
            currentDialog = r;
            isConfigShowing = true;
            doOpenConfig = false;
            return r;
        }
        public void HandleLinkClick()
        {
            switch (linkID)
            {
                case "ComboTextColor":
                    configSubmenu = ShowColorPicker("ComboTextColor", comboTextColor);
                    currentDialog = configSubmenu;
                    break;
                case "ComboEdgeColor":
                    configSubmenu = ShowColorPicker("ComboEdgeColor", comboEdgeColor);
                    currentDialog = configSubmenu;
                    break;
                case "ComboMeterColor":
                    configSubmenu = ShowColorPicker("ComboMeterColor", comboMeterColor);
                    currentDialog = configSubmenu;
                    break;
                case "RockMeterBaseColor":
                    configSubmenu = ShowColorPicker("RockMeterBaseColor", rockMeterColor);
                    currentDialog = configSubmenu;
                    break;
                case "RockMeterOverlayColor":
                    configSubmenu = ShowColorPicker("RockMeterOverlayColor", rockOverlayColor);
                    currentDialog = configSubmenu;
                    break;
                case "ComboMeterToggle":
                    ToggleBoolConfig("EnableComboMeter");
                    dialogBoxTMP.text = RefreshConfigMenuText();
                    break;
                case "RockMeterToggle":
                    ToggleBoolConfig("EnableRockMeter");
                    dialogBoxTMP.text = RefreshConfigMenuText();
                    break;
                case "SongFailToggle":
                    ToggleBoolConfig("RestartOnFail");
                    dialogBoxTMP.text = RefreshConfigMenuText();
                    break;
                case "ResetComboColors":
                    comboMeterRGB = "444444";
                    comboEdgeRGB = "7F7F7F";
                    comboTextRGB = "FFFFFF";
                    if (File.Exists(Path.Combine(selectedTheme, "theme.ini")))
                    {
                        IniData ini = ParseThemeIni(Path.Combine(selectedTheme, "theme.ini"));

                        if (ini.Sections.ContainsSection("Colors"))
                        {
                            if (ini["Colors"]?.GetKeyData("default_combo_bg")?.Value != null)
                                comboMeterRGB = ini["Colors"].GetKeyData("default_combo_bg").Value;
                            if (ini["Colors"]?.GetKeyData("default_combo_text")?.Value != null)
                                comboTextRGB = ini["Colors"].GetKeyData("default_combo_text").Value;
                            if (ini["Colors"]?.GetKeyData("default_combo_edge")?.Value != null)
                                comboEdgeRGB = ini["Colors"].GetKeyData("default_combo_edge").Value;
                        }
                    }
                    comboMeterColor = ColorFromHexString_NoTag(comboMeterRGB);
                    comboEdgeColor = ColorFromHexString_NoTag(comboEdgeRGB);
                    comboTextColor = ColorFromHexString_NoTag(comboTextRGB);
                    cfgComboMeterColorHex.SetSerializedValue(comboMeterRGB);
                    cfgComboEdgeColorHex.SetSerializedValue(comboEdgeRGB);
                    cfgComboTextColorHex.SetSerializedValue(comboTextRGB);
                    RefreshColors();
                    dialogBoxTMP.text = RefreshConfigMenuText();
                    break;
                case "ResetHealthColors":
                    rockMeterRGB = "F9BC75";
                    rockOverlayRGB = "FFFFFF";
                    if (File.Exists(Path.Combine(selectedTheme, "theme.ini")))
                    {
                        IniData ini = ParseThemeIni(Path.Combine(selectedTheme, "theme.ini"));

                        if (ini.Sections.ContainsSection("Colors"))
                        {
                            if (ini["Colors"]?.GetKeyData("default_health_meter")?.Value != null)
                                rockMeterRGB = ini["Colors"].GetKeyData("default_health_meter").Value;
                            if (ini["Colors"]?.GetKeyData("default_health_overlay")?.Value != null)
                                rockOverlayRGB = ini["Colors"].GetKeyData("default_health_overlay").Value;
                        }
                    }
                    rockMeterColor = ColorFromHexString_NoTag(rockMeterRGB);
                    rockOverlayColor = ColorFromHexString_NoTag(rockOverlayRGB);
                    cfgRockMeterColorHex.SetSerializedValue(rockMeterRGB);
                    cfgRockOverlayColorHex.SetSerializedValue(rockOverlayRGB);
                    RefreshColors();
                    dialogBoxTMP.text = RefreshConfigMenuText();
                    break;
                case "SelectTheme":
                    OpenThemeFolder(selectedTheme);
                    break;
                case "OpenThemeFolder":
                    System.Diagnostics.Process.Start(Path.Combine(Paths.PluginPath, "assets", "themes"));
                    break;
                default:
                    break;
            }
            linkID = "";
        }

        public string GetThemeName(string path)
        {
            themeCreator = "";
            if (selectedTheme.EndsWith("/") || selectedTheme.EndsWith("\\"))
                selectedTheme = selectedTheme.Substring(0, selectedTheme.Length - 1);
            if (Directory.Exists(selectedTheme))
            {
                IniData ini = ParseThemeIni(Path.Combine(selectedTheme, "theme.ini"));
                if (ini != null)
                {
                    selectedThemeName = ini["Meta"]?.GetKeyData("theme_name")?.Value;
                    themeCreator = ini["Meta"]?.GetKeyData("creator")?.Value;
                    if (selectedThemeName == null)
                    {
                        selectedThemeName = "Unknown (bad metadata)";
                    }
                }
                else
                {
                    selectedThemeName = "Unknown (invalid theme.ini)";
                }
            }
            else
            {
                selectedThemeName = new DirectoryInfo(selectedTheme).Name;
            }
            return selectedThemeName;
        }
        
        #region Mouse Handlers
        // Handlers for rock meter dragging with mouse
        public string linkID;
        public void MouseDownHandle()
        {
            mousePosOnDown = Mouse.current.position.ReadValue();
            dragDiff_Health = new Vector2(69420, 69420);
            dragDiff_Combo = new Vector2(69420, 69420);
            if (inGame)
            {
                isDraggingHealth = false;
                isDraggingCombo = false;
                if (rockContainer != null)
                {
                    rectMeter = rockContainer?.GetComponentInChildren<RectTransform>();
                    if (rectMeter != null)
                    {
                        dragDiff_Health = mousePosOnDown - (Vector2)rockContainer.transform.position;
                        isDraggingHealth = rectMeter.rect.Contains(dragDiff_Health);
                    }
                }
                if (comboContainer != null)
                {
                    rectCombo = comboContainer?.GetComponentInChildren<RectTransform>();
                    if (rectCombo != null)
                    {
                        dragDiff_Combo = mousePosOnDown - (Vector2)comboContainer.transform.position;
                        isDraggingCombo = rectCombo.rect.Contains(dragDiff_Combo);
                    }
                }
                if (Keyboard.current.ctrlKey.isPressed && !dragBothMeters)
                {
                    dragBothMeters = true;
                }
            }
            dialogBoxTMP = GameObject.Find("Persistent Canvas/Dialog Container/MessageDialog(Clone)/Base/Content/Message")?.GetComponentInChildren<TextMeshProUGUI>();
            if (dialogBoxTMP != null && inGame)
            {
                int linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogBoxTMP, Mouse.current.position.ReadValue(), Camera.main);
                if (linkIndex != -1)
                {
                    linkID = dialogBoxTMP.textInfo.linkInfo[linkIndex].GetLinkID();
                }
            }
        }
        public void MouseDragHandle()
        {
            if ((isDraggingHealth || dragBothMeters) && rockContainer != null && dragDiff_Health.x != 69420)
            {
                rockContainer.transform.position = (Vector3)Mouse.current.position.value - (Vector3)dragDiff_Health;
            }
            if ((isDraggingCombo || dragBothMeters) && comboContainer != null && dragDiff_Combo.x != 69420)
            {
                comboContainer.transform.position = (Vector3)Mouse.current.position.value - (Vector3)dragDiff_Combo;
            }
        }
        public void MouseUpHandle()
        {
            if (inGame && (rockContainer != null || comboContainer != null))
            {
                if (rockContainer != null)
                {
                    Vector2 meterPos = rockContainer.transform.position;
                    SetHealthMeterPos(meterPos.x, meterPos.y);
                }
                if (comboContainer != null)
                {
                    Vector2 comboPos = comboContainer.transform.position;
                    SetComboPos(comboPos.x, comboPos.y);
                }
                UpdateConfig();
            }
            if (linkID == "RockMeterConfig" && !inGame)
            {
                doOpenConfig = true;
                RefreshColors();
                configMenuObj = OpenConfigMenu();
                currentDialog = configMenuObj;
            }
            
            HandleLinkClick();

            dragBothMeters = false;
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
        public ConfigEntry<string> cfgThemePath;
        public ConfigEntry<string> cfgRockMeterColorHex;
        public ConfigEntry<string> cfgRockOverlayColorHex;
        public Color rockMeterColor;
        public Color rockOverlayColor;
        public Color comboMeterColor;
        public Color comboEdgeColor;
        public Color comboTextColor;
        public string comboMeterRGB;
        public string comboEdgeRGB;
        public string comboTextRGB;
        public string rockMeterRGB;
        public string rockOverlayRGB;
        
        public TextMeshProUGUI watermarkTMP;
        public object configMenuObj;
        public bool configInputShowing;

        public string ColorToHexString_NoTag(Color c) => String.Format("{0:X2}{1:X2}{2:X2}", (int)(c.r * 255), (int)(c.g * 255), (int)(c.b * 255));
        public Color ColorFromHexString_NoTag(string hex) => new Color(
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
            feType = yargMainAsm.GetType("YARG.Helpers.FileExplorerHelper");
            
            quitMethod = gmType.GetMethod("ForceQuitSong");
            restartMethod = pmmType.GetMethod("Restart");
            pauseMethod = gmType.GetMethod("Pause");
            dialogShowMethod = dmType.GetMethod("ShowMessage");
            dialogClearMethod = dmType.GetMethod("ClearDialog");
            dialogColorMethod = dmType.GetMethod("ShowColorPickerDialog");
            dialogTextMethod = dmType.GetMethod("ShowRenameDialog");
            openFileMethod = feType.GetMethod("OpenChooseFile");
            openFolderMethod = feType.GetMethod("OpenChooseFolder");

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
            rockMeterColor = ColorFromHexString_NoTag("F9BC75");
            rockOverlayColor = ColorFromHexString_NoTag("85A2AF");
            comboMeterRGB = ColorToHexString_NoTag(comboMeterColor);
            comboEdgeRGB = ColorToHexString_NoTag(comboEdgeColor);
            comboTextRGB = ColorToHexString_NoTag(comboTextColor);
            rockMeterRGB = "F9BC75";
            rockOverlayRGB = "85A2AF";

            cfgComboMeterColorHex = Config.Bind<string>("Combo Meter", "ComboMeterColor",
                comboMeterRGB, "RGB hex color of the combo meter");
            cfgComboEdgeColorHex  = Config.Bind<string>("Combo Meter", "MeterEdgeColor",
                comboEdgeRGB, "RGB hex color of the edge of the streak counter");
            cfgComboTextColorHex  = Config.Bind<string>("Combo Meter", "ComboTextColor",
                comboTextRGB, "RGB hex color of the note streak text");
            cfgRockMeterColorHex = Config.Bind<string>("Rock Meter", "HealthMeterColor",
                rockMeterRGB, "RGB hex color of the Rock Meter base");
            cfgRockOverlayColorHex = Config.Bind<string>("Rock Meter", "OverlayColor",
                rockOverlayRGB, "RGB hex color of the Rock Meter overlay (not visible with all themes)");

            // TO DO: Maybe add config options for these values? (might not be good to add)
            missDrainAmount = 0.0277;
            hitHealthAmount = 0.0069;  // Nice
            hitSPGainAmount = 0.045;

            missDrainAmount_Vox = 0.249f;	// Max drain amount for vocals
            hitHealthAmount_Vox = 1/3f;		// 
            hitSPGainAmount_Vox = 0.5f;

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
            
            isConfigShowing = false;
            doOpenConfig = false;
            configInputShowing = false;

            cfgThemePath = Config.Bind<string>("Themes", "ThemeFolder", Path.Combine(Paths.PluginPath, "assets", "themes", "default"));
            if (!Directory.Exists(cfgThemePath.GetSerializedValue()))
            {
                selectedTheme = Path.Combine(Paths.PluginPath, "assets");
            }
            else
            {
                selectedTheme = cfgThemePath.GetSerializedValue();
            }

            GetThemeName(selectedTheme);
            
            needleAngle = 88f;
            healthMeterScale = 2f;
            comboScale = 2f;
            maxDigitsCombo = 6;

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
                    rockMeterColor = ColorFromHexString_NoTag(cfgRockMeterColorHex.GetSerializedValue());
                    rockOverlayColor = ColorFromHexString_NoTag(cfgRockOverlayColorHex.GetSerializedValue());
                    comboMeterColor = ColorFromHexString_NoTag(cfgComboMeterColorHex.GetSerializedValue());
                    comboEdgeColor = ColorFromHexString_NoTag(cfgComboEdgeColorHex.GetSerializedValue());
                    comboTextColor = ColorFromHexString_NoTag(cfgComboTextColorHex.GetSerializedValue());
                }
                if (sc.name == "MenuScene")
                {
                    inGame = false;
                }
                if (sc.name == "PersistentScene")
                {

                }
            };
            SceneManager.sceneUnloaded += delegate (Scene sc)
            {
                if (sc.name == "Gameplay")
                {
                    dialogClearMethod.Invoke(dmo.GetComponent("YARG.Menu.Persistent.DialogManager"), null);
                    linkID = "";
                    isMouseDown = false;
                    currentHealth = defaultHealth;
                    inGame = false;
                    rockContainer = null;
                    practice = false;
                    replay = false;
                    currentDialog = null;
                }
            };

            Harmony.PatchAll();
        }
        private void LateUpdate()
        {
            if (watermarkTMP == null)
            {
                watermarkTMP = FindAndSetActive("Watermark Container").GetComponentInChildren<TextMeshProUGUI>();
                gvo = GameObject.Find("Global Variables");
                if (watermarkTMP.text.Contains("YARG v1.22.33b") && gvo != null) // Detect if we're on the Stable Build
                {
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
                    dialogBoxTMP = GameObject.Find("Persistent Canvas/Dialog Container/MessageDialog(Clone)/Base/Content/Message")?.GetComponent<TextMeshProUGUI>();
                    MouseDownHandle();
                }
                if (isDraggingHealth || isDraggingCombo || dragBothMeters) MouseDragHandle();
            }
            if (inGame && Mouse.current.rightButton.isPressed)
            {
                SetHealthMeterPos(Screen.width * 0.875f, Screen.height * 0.6f);
                SetComboPos(Screen.width * 0.875f, Screen.height * 0.53f);
                isDraggingHealth = false;
                isDraggingCombo = false;
                isMouseDown = false;
                UpdateConfig();
            }

            // Handle clicking config menu and stuff
            if (Mouse.current.leftButton.isPressed && !isMouseDown)
            {
                int linkIndex = -1;
                if (dialogBoxTMP != null)
                {
                    linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogBoxTMP, Mouse.current.position.ReadValue(), Camera.main);
                    if (linkIndex != -1)
                    {
                        linkID = dialogBoxTMP.textInfo.linkInfo[linkIndex].GetLinkID();
                    }
                }
                else
                {
                    linkIndex = TMP_TextUtilities.FindIntersectingLink(watermarkTMP, Mouse.current.position.ReadValue(), Camera.main);
                    if (linkIndex != -1 && !inGame)
                    {
                        linkID = watermarkTMP.textInfo.linkInfo[linkIndex].GetLinkID();
                    }
                }
                isMouseDown = true;
            }
            if (!Mouse.current.leftButton.isPressed && isMouseDown)
            {
                MouseUpHandle();
            }
            if (isConfigShowing)
            {
                if (dialogBoxTMP == null)  // called when config menu is closed
                {
                    isConfigShowing = false;
                }
            }
            else if (!inGame && configInputShowing)
            {
                if (currentDialog == null)
                {
                    configMenuObj = OpenConfigMenu();
                    currentDialog = configMenuObj;
                    configInputShowing = false;
                }
            }

            if (!Mouse.current.leftButton.isPressed && isMouseDown)
            {
                MouseUpHandle();

                isDraggingHealth = false;
                isDraggingCombo = false;
                isMouseDown = false;
            }
        }
        #endregion
    }
}
