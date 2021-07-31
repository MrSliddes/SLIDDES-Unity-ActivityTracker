using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;

namespace SLIDDES.ActivityTracker
{
    public class ActivityTracker : EditorWindow
    {
        // A session starts when the Unity is loaded and stops when Unity is quit (x)

        /// <summary>
        /// Does the user want to include non preset window titles?
        /// </summary>
        private bool includeNonPresetActivities;
        /// <summary>
        /// If the user is working outside the unity window
        /// </summary>
        private bool isOutsideUnityFlag;
        /// <summary>
        /// The current unique session ID. every time when the editor closes this id gets incremented by 1
        /// </summary>
        private uint sessionID;
        /// <summary>
        /// The last known session id to the editor window. Only gets updated when it isnt equal to sessionID
        /// </summary>
        private uint lastKnownSessionID;
        /// <summary>
        /// The name of the window selected when outside Unity
        /// </summary>
        private string currentOutsideUnityWindow = "";
        /// <summary>
        /// The total time spend in this project
        /// </summary>
        private double totalProjectTime;
        /// <summary>
        /// The total global time of working in Unity projects
        /// </summary>
        private double totalGlobalTime;
        /// <summary>
        /// The editor window scroll position
        /// </summary>
        private Vector2 windowScrollPosition;
        /// <summary>
        /// The current editor window being selected
        /// </summary>
        private EditorWindow currentEditorWindow;
        /// <summary>
        /// Contains the name and time of the current session activities
        /// </summary>
        private Dictionary<string, double> sessionActivities = new Dictionary<string, double>();
        /// <summary>
        /// Contains the name and time of all sessions activities
        /// </summary>
        private Dictionary<string, double> allSessionsActivities = new Dictionary<string, double>();

        /// <summary>
        /// Keeps track of the current editor window selection time
        /// </summary>
        private Stopwatch stopwatchCurrentEditorWindow;
        /// <summary>
        /// Timespan multipurose var
        /// </summary>
        private TimeSpan ts;

        // Editor
        private bool foldoutOverviewCurrentSession;
        private bool foldoutOverviewTotalSession;
        private bool foldoutSettings;
        private GUIStyle styleLabelRight;
        private GUIStyle styleLabelHeader;

        [MenuItem("Window/SLIDDES/Activity Tracker", false)]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            EditorWindow window = GetWindow(typeof(ActivityTracker), false, "Activity Tracker", true);
            window.minSize = new Vector2(320, 160);            
        }

        private void OnEnable()
        {
            EditorApplication.update += CheckFocusedWindow;
            EditorApplication.quitting += OnEditorApplicationQuit; // Dont remove it in ondestroy
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            stopwatchCurrentEditorWindow = new Stopwatch();
            stopwatchCurrentEditorWindow.Start();
            currentEditorWindow = this;
            sessionActivities.Add("Activity Tracker", 0);

            // Check session

            // Load ids
            lastKnownSessionID = 0;
            uint.TryParse(EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_lastKnownSessionID", "0"), out lastKnownSessionID);
            sessionID = 1;
            uint.TryParse(EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_sessionID", "1"), out sessionID);

            // Check if session ID has changed
            if(lastKnownSessionID != sessionID)
            {
                // New session
                lastKnownSessionID = sessionID;
            }
            else
            {
                // Continue with previous session
            }

            // Load total project time
            totalProjectTime = 0;
            string totalTimeString = EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_totalProjectTime", "0");
            double.TryParse(totalTimeString, out totalProjectTime);

            Load();
        }

        private void OnDestroy()
        {
            EditorApplication.update -= CheckFocusedWindow;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            Save();
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            // Styles
            styleLabelRight = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight };
            styleLabelHeader = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            string s;

            EditorGUILayout.BeginVertical(EditorStyles.inspectorFullWidthMargins);
            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);

            // Session id
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Session: ", "The current session id"), styleLabelHeader, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent("Session: ")).x) });
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(sessionID.ToString(), styleLabelRight, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent(sessionID.ToString())).x) });
            EditorGUILayout.EndHorizontal();

            // Current activity
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Current Activity: ", "The current window selected"), styleLabelHeader, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent("Current Activity: ")).x) });
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(UnityEditorInternal.InternalEditorUtility.isApplicationActive ? currentEditorWindow.titleContent.text : currentOutsideUnityWindow, styleLabelRight, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent(UnityEditorInternal.InternalEditorUtility.isApplicationActive ? currentEditorWindow.titleContent.text : currentOutsideUnityWindow)).x) });
            EditorGUILayout.EndHorizontal();

            // Session time
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Current Session Time", "The time since the Activity Tracker window was opend"), styleLabelHeader, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent("Current Session Time ")).x) });
            ts = TimeSpan.FromSeconds(EditorApplication.timeSinceStartup);
            s = string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(s, styleLabelRight, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent(s)).x) });
            EditorGUILayout.EndHorizontal();

            // Total Session time
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("All Sessions Time", "The total time of the Activity Tracker window being opend"), styleLabelHeader, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent("Total Session Time ")).x) });
            ts = TimeSpan.FromSeconds(totalProjectTime);
            s = string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(s, styleLabelRight, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent(s)).x) });
            EditorGUILayout.EndHorizontal();

            // Unity Global time
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Unity Global Time", "The total time of working in Unity Projects"), styleLabelHeader, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent("Unity Global Time ")).x) });
            ts = TimeSpan.FromSeconds(totalGlobalTime);
            s = string.Format("{0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(s, styleLabelRight, new GUILayoutOption[] { GUILayout.Width(EditorStyles.boldLabel.CalcSize(new GUIContent(s)).x) });
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Overview current session
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);           
            foldoutOverviewCurrentSession = EditorGUILayout.Foldout(foldoutOverviewCurrentSession, " Overview Current Session", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutOverviewCurrentSession)
            {
                // Display dictionary editorWindowsAccessed                
                foreach(var item in sessionActivities.OrderByDescending(x => x.Value))
                {
                    ts = TimeSpan.FromSeconds(item.Value);
                    float percentage = (Mathf.Round((int)ts.TotalSeconds) / ((float)TimeSpan.FromSeconds(EditorApplication.timeSinceStartup).TotalSeconds) * 100);
                    percentage = Mathf.Clamp(Mathf.Round(percentage), 0.01f, 100);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent(item.Key, "Name of activity"));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(new GUIContent(string.Format("{0:00}:{1:00}:{2:00}  {3}%", ts.Hours, ts.Minutes, ts.Seconds, percentage.ToString("0.00")), "Time active / Percentage from Current Session Time"), styleLabelRight);                    
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            // Overview total sessions
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutOverviewTotalSession = EditorGUILayout.Foldout(foldoutOverviewTotalSession, " Overview Total Sessions", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutOverviewTotalSession)
            {
                // Display dictionary editorWindowsAccessed                
                foreach(var item in allSessionsActivities.OrderByDescending(x => x.Value))
                {
                    ts = TimeSpan.FromSeconds(item.Value);
                    float percentage = (Mathf.Round((int)ts.TotalMinutes) / (int)(TimeSpan.FromSeconds(totalProjectTime).TotalMinutes) * 100);
                    percentage = Mathf.Clamp(Mathf.Round(percentage), 0.01f, 100);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(new GUIContent(item.Key, "Name of activity"));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(new GUIContent(string.Format("{0:00}:{1:00}:{2:00}  {3}%", ts.Hours, ts.Minutes, ts.Seconds, percentage.ToString("0.00")), "Time active / Percentage from Current Session Time"), styleLabelRight);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            // Settings
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutSettings = EditorGUILayout.Foldout(foldoutSettings, " Settings", true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            if(foldoutSettings)
            {
                // Include non preset windows toggle
                EditorGUIUtility.labelWidth = 170;
                includeNonPresetActivities = EditorGUILayout.Toggle("Include non preset windows", includeNonPresetActivities);
                EditorGUILayout.Space();
                // Reset button
                if(GUILayout.Button(new GUIContent("Reset Activity Tracker", "Resets all values of Activity Tracker")))
                {
                    if(EditorUtility.DisplayDialog("Reset Activity Tracker", "Are you sure you want to reset all values of Activity Tracker?", "Yes"))
                    {
                        ResetActivityTracker();
                    }
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Checks if the focused window has changed
        /// </summary>
        private void CheckFocusedWindow()
        {
            // Outside unity
            if(!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                if(!isOutsideUnityFlag)
                {
                    // Is outside unity
                    isOutsideUnityFlag = true;
                    // Set previous editor window stopwatch time            
                    stopwatchCurrentEditorWindow.Stop();
                    if(sessionActivities.ContainsKey(currentEditorWindow.titleContent.text)) sessionActivities[currentEditorWindow.titleContent.text] += stopwatchCurrentEditorWindow.Elapsed.TotalSeconds;
                    stopwatchCurrentEditorWindow.Reset();
                    currentOutsideUnityWindow = "";
                }
                HandleWindowOutsideUnity();
            }
            else
            {
                if(isOutsideUnityFlag)
                {
                    // Is inside unity
                    isOutsideUnityFlag = false;
                    // Set previous editor window stopwatch time            
                    stopwatchCurrentEditorWindow.Stop();
                    if(sessionActivities.ContainsKey(currentOutsideUnityWindow)) sessionActivities[currentOutsideUnityWindow] += stopwatchCurrentEditorWindow.Elapsed.TotalSeconds;
                    stopwatchCurrentEditorWindow.Reset();
                    // Start stopwatch again for selected editor window to continue
                    stopwatchCurrentEditorWindow.Start();
                }
            }


            // Inside unity
            // Has window changed
            if(currentEditorWindow == null && focusedWindow != null) currentEditorWindow = focusedWindow;
            if(focusedWindow == null || currentEditorWindow == null) return;
            if(focusedWindow.GetInstanceID() == currentEditorWindow.GetInstanceID() || isOutsideUnityFlag) return;

            // Set previous editor window stopwatch time            
            stopwatchCurrentEditorWindow.Stop();
            // Add to dictionary if not present
            if(!sessionActivities.ContainsKey(currentEditorWindow.titleContent.text))
            {
                sessionActivities.Add(currentEditorWindow.titleContent.text, 0);
            }
            sessionActivities[currentEditorWindow.titleContent.text] += stopwatchCurrentEditorWindow.Elapsed.TotalSeconds;
            stopwatchCurrentEditorWindow.Reset();

            // Focused window changed
            currentEditorWindow = focusedWindow;
            // Add to dictionary if not present
            if(!sessionActivities.ContainsKey(currentEditorWindow.titleContent.text))
            {
                sessionActivities.Add(currentEditorWindow.titleContent.text, 0);
            }
            // Start stopwatch
            stopwatchCurrentEditorWindow.Start();
        }

        /// <summary>
        /// Handle the selected window when the Unity Editor window isn't selected
        /// </summary>
        private void HandleWindowOutsideUnity()
        {
            // Filter the title with regex magic
            string title = GetActiveWindowTitle();
            //UnityEngine.Debug.Log(title);
            if(string.IsNullOrEmpty(title)) return;
            if(Regex.Match(title, @"\bMicrosoft Visual Studio\b", RegexOptions.IgnoreCase).Success) title = "Microsoft Visual Studio";
            else if(Regex.Match(title, @"\bYouTube\b", RegexOptions.IgnoreCase).Success) title = "YouTube";            
            else if(Regex.Match(title, @"\bGitHub\b", RegexOptions.IgnoreCase).Success) title = "GitHub";
            else if(Regex.Match(title, @"\bTwitch\b", RegexOptions.IgnoreCase).Success) title = "Twitch";
            else if(Regex.Match(title, @"\bTwitter\b", RegexOptions.IgnoreCase).Success) title = "Twitter";
            else if(Regex.Match(title, @"\bReddit\b", RegexOptions.IgnoreCase).Success) title = "Reddit";
            else if(Regex.Match(title, @"\bMozilla Firefox\b", RegexOptions.IgnoreCase).Success) title = "Mozilla Firefox";
            else if(Regex.Match(title, @"\bGitHub Desktop\b", RegexOptions.IgnoreCase).Success) title = "GitHub Desktop";
            else if(Regex.Match(title, @"\bNotepad\b", RegexOptions.IgnoreCase).Success) title = "Notepad";
            else if(Regex.Match(title, @"\bDiscord\b", RegexOptions.IgnoreCase).Success) title = "Discord";
            else if(Regex.Match(title, @"\bGoogle Chrome\b", RegexOptions.IgnoreCase).Success) title = "Google Chrome";
            else if(Regex.Match(title, @"\bCalculator\b", RegexOptions.IgnoreCase).Success) title = "Calculator";
            else if(Regex.Match(title, @"\bNetflix\b", RegexOptions.IgnoreCase).Success) title = "Netflix";
            else if(Regex.Match(title, @"\bSLIDDES.ActivityTracker.ActivityTracker\b", RegexOptions.IgnoreCase).Success) title = "Activity Tracker";
            else
            {
                // Match not found
                if(!includeNonPresetActivities) return;
            }

            // If window name hasnt changed return
            if(currentOutsideUnityWindow == title) return;

            // If currentOutSideUnityWindow is "" skip this
            if(currentOutsideUnityWindow != "")
            {
                // New window
                // Set previous window stopwatch time            
                stopwatchCurrentEditorWindow.Stop();
                // Add to dictionary if not present
                if(!sessionActivities.ContainsKey(currentOutsideUnityWindow)) sessionActivities.Add(currentOutsideUnityWindow, 0);
                sessionActivities[currentOutsideUnityWindow] += stopwatchCurrentEditorWindow.Elapsed.TotalSeconds;
                stopwatchCurrentEditorWindow.Reset();
            }

            // Set new window
            currentOutsideUnityWindow = title;
            // Add to dictionary if not present
            if(!sessionActivities.ContainsKey(currentOutsideUnityWindow)) sessionActivities.Add(currentOutsideUnityWindow, 0);            
            // Start stopwatch
            stopwatchCurrentEditorWindow.Start();
        }

        /// <summary>
        /// Triggerd before scripts are recompiled
        /// </summary>
        private void OnBeforeAssemblyReload()
        {
            Save();
        }

        private void OnEditorApplicationQuit()
        {
            // Save the session id
            UnityEngine.Debug.Log("[Activity Tracker] Session " + sessionID + " ended.");
            sessionID++;
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_sessionID", sessionID.ToString());
            // Save to total time
            totalProjectTime += EditorApplication.timeSinceStartup;
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_totalProjectTime", totalProjectTime.ToString());
            // Save total time to global time
            totalGlobalTime += EditorApplication.timeSinceStartup;
            EditorPrefs.SetString("_sliddes_at_totalGlobalTime", totalGlobalTime.ToString());
            // Save dict to total dict
            // Merge editorWindowsAccesed to totalEditorWindowsAccesed
            foreach(var item in sessionActivities)
            {
                // CHeck if total already has it
                if(allSessionsActivities.ContainsKey(item.Key))
                {
                    // Add value to existing
                    allSessionsActivities[item.Key] += item.Value;
                }
                else
                {
                    // Create new
                    allSessionsActivities.Add(item.Key, item.Value);
                }
            }
            // Save dict values
            string s = "";
            foreach(var item in allSessionsActivities)
            {
                s += item.Key + "#";
                EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities_" + item.Key, item.Value.ToString());
            }
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities", s);
            // Reset current session dict values
            foreach(var item in sessionActivities)
            {
                if(EditorPrefs.HasKey(Application.productName.ToLower() + "_sliddes_at_sessionActivities_" + item.Key)) EditorPrefs.DeleteKey(Application.productName.ToLower() + "_sliddes_at_sessionActivities_" + item.Key);
            }
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_sessionActivities", "");
        }

        /// <summary>
        /// Save values to EditorPrefs
        /// </summary>
        private void Save()
        {
            // Save lastKnownSessionID
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_lastKnownSessionID", lastKnownSessionID.ToString());
            // Save sessionID
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_sessionID", sessionID.ToString());
            // Save editor window dict values
            string s = "";
            foreach(var item in sessionActivities)
            {
                s += item.Key + "#";
                EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_sessionActivities_" + item.Key, item.Value.ToString());
            }
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_sessionActivities", s);
            // Save include non preset windows
            EditorPrefs.SetBool("sliddes_at_includeNonPresetActivities", includeNonPresetActivities);
        }

        /// <summary>
        /// Load values from editorprefs
        /// </summary>
        private void Load()
        {
            // Load any saved dictionary values
            string[] names = { "" };
            if(EditorPrefs.HasKey(Application.productName.ToLower() + "_sliddes_at_sessionActivities")) names = EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_sessionActivities").Split('#');
            // For every name get the time value
            for(int i = 0; i < names.Length; i++)
            {
                string key = names[i].Replace("#", "");
                if(string.IsNullOrEmpty(key)) continue;
                double value = 0;
                string timeInString = EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_sessionActivities_" + key, "0");
                double.TryParse(timeInString, out value);
                // Add to dict
                if(!sessionActivities.ContainsKey(key)) sessionActivities.Add(key, value);
                else sessionActivities[key] = value;
            }
            // Load total saved dictionary values
            names = EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities").Split('#');
            foreach(var item in names)
            {
                string key = item.Replace("#", "");
                if(string.IsNullOrEmpty(key)) continue;
                double value = 0;
                string timeInString = EditorPrefs.GetString(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities_" + key, "0");
                double.TryParse(timeInString, out value);
                // Add to dict
                if(!allSessionsActivities.ContainsKey(key)) allSessionsActivities.Add(key, value);
                else allSessionsActivities[key] = value;
            }
            // Load total global time
            string s = EditorPrefs.GetString("_sliddes_at_totalGlobalTime", "0");
            double.TryParse(s, out totalGlobalTime);
            // Load include non preset windows
            includeNonPresetActivities = EditorPrefs.GetBool("sliddes_at_includeNonPresetActivities");
        }

        /// <summary>
        /// Reset all activity tracker values
        /// </summary>
        private void ResetActivityTracker()
        {
            UnityEngine.Debug.Log("[Activity Tracker] Resetted");
            lastKnownSessionID = 0;
            sessionID = 1;
            totalProjectTime = 0;
            totalGlobalTime = 0;
            includeNonPresetActivities = false;
            // Delete all editorprefs values
            // Delete current session activities
            foreach(var item in sessionActivities)
            {
                if(EditorPrefs.HasKey(Application.productName.ToLower() + "_sliddes_at_sessionActivities_" + item.Key)) EditorPrefs.DeleteKey(Application.productName.ToLower() + "_sliddes_at_sessionActivities_" + item.Key);
            }
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_sessionActivities", "");
            sessionActivities = new Dictionary<string, double>();
            // Delete all sessions activities
            foreach(var item in allSessionsActivities)
            {
                if(EditorPrefs.HasKey(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities_" + item.Key)) EditorPrefs.DeleteKey(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities_" + item.Key);
            }
            EditorPrefs.SetString(Application.productName.ToLower() + "_sliddes_at_allSessionsActivities", "");
            allSessionsActivities = new Dictionary<string, double>();

            Save();
            Close();
        }

#if UNITY_EDITOR_WIN

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if(GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }

#endif
    }
}