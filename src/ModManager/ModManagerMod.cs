using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GTWM.ModManager
{
    [ModInfo("gtwm.modmanager", "Mod Manager", "1.1.0",
        Author = "GTWM",
        Description = "Enable, disable and hot-reload mods, and browse logs. Default key: F8.",
        Priority = -100,
        CanDisable = false)]
    public sealed class ModManagerMod : Mod
    {
        private static readonly string[] Tabs = { "Mods", "Logs", "Settings" };
        private static readonly string[] LogLevels = { "All", "Warnings", "Errors" };

        private readonly Queue<Action> _pending = new Queue<Action>();
        private readonly Dictionary<ModEntry, ModState> _lastStates = new Dictionary<ModEntry, ModState>();

        private KeyCode _toggleKey;
        private bool _open;
        private int _tab;
        private Rect _window = new Rect(-1f, -1f, 720f, 600f);
        private Vector2 _modScroll;
        private Vector2 _logScroll;
        private Vector2 _settingsScroll;
        private string _status = "";
        private float _statusUntil;
        private string _toast = "";
        private float _toastUntil;

        private int _logLevel;
        private string _logFilter = "";
        private bool _logFollow = true;
        private int _logVersionSeen = -1;
        private int _logLevelSeen = -1;
        private string _logFilterSeen;
        private List<LogLine> _logView = new List<LogLine>();
        private GUIStyle _mono;

        public override void OnLoad()
        {
            _toggleKey = Config.Get("toggle_key", KeyCode.F8);
            ModLoader.ModsChanged += OnModsChanged;
            OnModsChanged();

            int enabled = ModLoader.Mods.Count(m => m.IsEnabled);
            ShowToast($"{enabled} of {ModLoader.Mods.Count} mods running  ·  {_toggleKey} to manage", 7f);
        }

        public override void OnUnload()
        {
            ModLoader.ModsChanged -= OnModsChanged;
        }

        public override void OnUpdate()
        {
            // Mod actions run here rather than inside OnGUI: reloading this very mod tears down
            // the instance whose GUI code would still be on the stack.
            while (_pending.Count > 0)
            {
                _pending.Dequeue()();
            }

            if (ModUI.KeyDown(_toggleKey))
            {
                SetOpen(!_open);
            }
        }

        public override void OnGUI()
        {
            DrawToast();
            if (!_open)
            {
                return;
            }
            if (ModUI.Panel("gtwm.modmanager", ref _window, "Mods", $"GTWM {ModLoader.Version}", DrawContent))
            {
                SetOpen(false);
            }
        }

        private void SetOpen(bool open)
        {
            _open = open;
            ModUI.SetMenuOpen(this, open);
        }

        private void DrawContent()
        {
            _tab = ModUI.Segmented(_tab, Tabs);
            ModUI.Space(12f);
            switch (_tab)
            {
                case 0:
                    DrawModsTab();
                    break;
                case 1:
                    DrawLogsTab();
                    break;
                default:
                    DrawSettingsTab();
                    break;
            }
            if (Time.unscaledTime < _statusUntil)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(_status, ModUI.Caption);
            }
        }

        // ───────────────────────────── Mods ─────────────────────────────

        private void DrawModsTab()
        {
            GUILayout.BeginHorizontal();
            if (ModUI.Button("Scan for mods"))
            {
                Queue(() =>
                {
                    int found = ModLoader.Scan();
                    SetStatus(found == 0 ? "No new mods found." : $"Found {found} new mod(s).");
                });
            }
            if (ModUI.Button("Reload all"))
            {
                Queue(() =>
                {
                    ModLoader.ReloadAll();
                    SetStatus("Reloaded every mod from disk.");
                });
            }
            if (ModUI.Button("Open folder"))
            {
                OpenPath(ModLoader.ModsDir);
            }
            GUILayout.FlexibleSpace();
            int running = ModLoader.Mods.Count(m => m.IsEnabled);
            GUILayout.Label($"{running} of {ModLoader.Mods.Count} running", new GUIStyle(ModUI.Caption) { alignment = TextAnchor.MiddleRight }, GUILayout.Height(30f));
            GUILayout.EndHorizontal();
            ModUI.Space(8f);

            _modScroll = ModUI.BeginScroll(_modScroll);
            if (ModLoader.Mods.Count == 0)
            {
                ModUI.BeginGroup();
                GUILayout.Label("No mods installed", ModUI.Headline);
                GUILayout.Label("Drop mod DLLs into GTWM/mods, then choose Scan for mods.", ModUI.Caption);
                ModUI.EndGroup();
            }
            foreach (ModEntry entry in ModLoader.Mods.OrderBy(m => m.Info.Priority).ThenBy(m => m.Info.Name))
            {
                DrawModCard(entry);
            }
            ModUI.EndScroll();
        }

        private void DrawModCard(ModEntry entry)
        {
            ModInfoAttribute info = entry.Info;
            ModUI.BeginGroup();
            ModUI.Space(4f);

            GUILayout.BeginHorizontal();
            GUILayout.Label(info.Name, ModUI.Headline, GUILayout.ExpandWidth(false));
            GUILayout.Label($"  {info.Version}", ModUI.Caption, GUILayout.ExpandWidth(false));
            GUILayout.Space(8f);
            switch (entry.State)
            {
                case ModState.Enabled:
                    ModUI.Badge("Running", ModUI.Good);
                    break;
                case ModState.Error:
                    ModUI.Badge("Error", ModUI.Bad);
                    break;
                default:
                    ModUI.Badge("Off", ModUI.TextSecondary);
                    break;
            }
            GUILayout.FlexibleSpace();

            if (ModUI.Button("Config", ButtonKind.Plain))
            {
                string cfg = Path.Combine(ModLoader.ConfigDir, info.Id + ".cfg");
                if (File.Exists(cfg))
                {
                    OpenPath(cfg);
                }
                else
                {
                    SetStatus($"{info.Name} has no config file yet.");
                }
            }
            if (ModUI.Button("Reload"))
            {
                Queue(() => SetStatus(ModLoader.Reload(entry) ? $"Reloaded {entry.Info.Name}." : $"Reload of {entry.Info.Name} failed."));
            }
            if (entry.IsEnabled)
            {
                GUI.enabled = info.CanDisable;
                if (ModUI.Button(info.CanDisable ? "Disable" : "Required", ButtonKind.Destructive, GUILayout.Width(86f)))
                {
                    Queue(() =>
                    {
                        ModLoader.Disable(entry);
                        SetStatus($"Disabled {info.Name}.");
                    });
                }
                GUI.enabled = true;
            }
            else if (ModUI.Button("Enable", ButtonKind.Primary, GUILayout.Width(86f)))
            {
                Queue(() => SetStatus(ModLoader.Enable(entry) ? $"Enabled {info.Name}." : $"{info.Name} failed to start."));
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(info.Description))
            {
                ModUI.Space(2f);
                GUILayout.Label(info.Description, new GUIStyle(ModUI.Body) { normal = { textColor = ModUI.TextSecondary } });
            }

            var details = new StringBuilder();
            details.Append(info.Author).Append("  ·  ").Append(Path.GetFileName(entry.FilePath));
            if (entry.ReloadCount > 0)
            {
                details.Append("  ·  reloaded ").Append(entry.ReloadCount).Append('×');
            }
            if (entry.IsEnabled && entry.Instance?.Harmony != null)
            {
                int patched = entry.Instance.Harmony.GetPatchedMethods().Count();
                if (patched > 0)
                {
                    details.Append("  ·  ").Append(patched).Append(" patch").Append(patched == 1 ? "" : "es");
                }
            }
            if (entry.RuntimeErrorCount > 0)
            {
                details.Append("  ·  ").Append(entry.RuntimeErrorCount).Append(" error").Append(entry.RuntimeErrorCount == 1 ? "" : "s");
            }
            GUILayout.Label(details.ToString(), new GUIStyle(ModUI.Caption) { normal = { textColor = ModUI.TextTertiary } });

            if (!string.IsNullOrEmpty(entry.Error))
            {
                GUILayout.Label(ModUI.Colored(entry.Error, ModUI.Bad), ModUI.Caption);
            }
            ModUI.Space(4f);
            ModUI.EndGroup();
            ModUI.Space(4f);
        }

        // ───────────────────────────── Logs ─────────────────────────────

        private void DrawLogsTab()
        {
            GUILayout.BeginHorizontal();
            _logLevel = ModUI.Segmented(_logLevel, LogLevels, GUILayout.Width(250f));
            GUILayout.Space(8f);
            _logFilter = ModUI.Field(_logFilter, 80, "Search", "gtwm_log_search", GUILayout.ExpandWidth(true));
            GUILayout.Space(8f);
            if (ModUI.Button("Clear"))
            {
                LogBuffer.Clear();
            }
            if (ModUI.Button("Open file"))
            {
                OpenPath(Path.Combine(ModLoader.LogsDir, "latest.log"));
            }
            GUILayout.EndHorizontal();

            if (_logLevel != _logLevelSeen || _logFilter != _logFilterSeen || LogBuffer.Version != _logVersionSeen)
            {
                RebuildLogView();
            }

            ModUI.Space(8f);
            if (_mono == null)
            {
                Font consolas = Font.CreateDynamicFontFromOSFont(new[] { "Cascadia Mono", "Consolas", "Courier New" }, 12);
                _mono = new GUIStyle(ModUI.Caption) { font = consolas, fontSize = 12, richText = true, wordWrap = true };
            }
            ModUI.BeginGroup();
            _logScroll = ModUI.BeginScroll(_logScroll);
            if (_logView.Count == 0)
            {
                GUILayout.Label("Nothing to show.", ModUI.Caption);
            }
            foreach (LogLine line in _logView)
            {
                Color c = line.Level == LogLevel.Error ? ModUI.Bad : line.Level == LogLevel.Warning ? ModUI.Warning : ModUI.TextPrimary;
                string msg = line.Message.Length > 2000 ? line.Message.Substring(0, 2000) + "…" : line.Message;
                GUILayout.Label(
                    $"{ModUI.Colored(line.Time.ToString("HH:mm:ss"), ModUI.TextTertiary)}  {ModUI.Colored(line.Source, ModUI.Accent)}  {ModUI.Colored(Escape(msg), c)}",
                    _mono);
            }
            ModUI.EndScroll();
            ModUI.EndGroup();

            _logFollow = ModUI.Switch("Follow new messages", _logFollow);
        }

        private void RebuildLogView()
        {
            _logVersionSeen = LogBuffer.Version;
            _logLevelSeen = _logLevel;
            _logFilterSeen = _logFilter;
            LogLevel minimum = _logLevel == 2 ? LogLevel.Error : _logLevel == 1 ? LogLevel.Warning : LogLevel.Info;
            _logView = LogBuffer.Snapshot()
                .Where(l => l.Level >= minimum)
                .Where(l => string.IsNullOrEmpty(_logFilter)
                    || l.Message.IndexOf(_logFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || l.Source.IndexOf(_logFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Reverse().Take(300).Reverse()
                .ToList();
            if (_logFollow)
            {
                _logScroll.y = float.MaxValue;
            }
        }

        // ───────────────────────────── Settings ─────────────────────────────

        private void DrawSettingsTab()
        {
            _settingsScroll = ModUI.BeginScroll(_settingsScroll);

            ModUI.BeginGroup("Appearance");
            bool blur = ModLoader.Config.Get("ui_blur", true);
            bool newBlur = ModUI.Switch("Frosted glass", blur, ModUI.GlassActive || !blur ? "Blurs the game behind menus." : "Blur isn't available right now, so a solid background is used.");
            if (newBlur != blur)
            {
                ModLoader.Config.Set("ui_blur", newBlur);
            }
            ModUI.Separator();
            float configured = ModLoader.Config.Get("ui_scale", 0f);
            bool auto = ModUI.Switch("Automatic size", configured <= 0.1f, "Scales the interface to your resolution.");
            if (auto && configured > 0.1f)
            {
                ModLoader.Config.Set("ui_scale", 0f);
            }
            else if (!auto)
            {
                ModUI.Separator();
                float current = configured > 0.1f ? configured : ModUI.Scale;
                float scale = Mathf.Round(ModUI.Slider("Interface size", current, 0.5f, 3f, "0.00×") * 20f) / 20f;
                if (!Mathf.Approximately(scale, configured))
                {
                    ModLoader.Config.Set("ui_scale", scale);
                }
            }
            ModUI.EndGroup();

            ModUI.BeginGroup("Mods");
            bool autoReload = ModUI.Switch("Auto-reload changed mods", ModLoader.AutoReload, "Reloads a mod as soon as its DLL changes on disk.");
            if (autoReload != ModLoader.AutoReload)
            {
                ModLoader.AutoReload = autoReload;
            }
            ModUI.Separator();
            ModUI.Row("Mod Manager shortcut", _toggleKey.ToString());
            ModUI.EndGroup($"Change toggle_key in config/gtwm.modmanager.cfg, then reload this mod.");

            ModUI.BeginGroup("Session");
            GUILayout.BeginHorizontal(GUILayout.Height(34f));
            GUILayout.Label("Leaderboard uploads", ModUI.Body, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            if (GameSession.CheatsUsed)
            {
                ModUI.Badge("Blocked", ModUI.Bad);
            }
            else
            {
                ModUI.Badge("Allowed", ModUI.Good);
            }
            GUILayout.EndHorizontal();
            ModUI.EndGroup(GameSession.CheatsUsed
                ? $"Cheats were used this session ({GameSession.FirstCheatUsed}). Restart the game to upload scores again."
                : "Using any cheat blocks uploads until the game restarts.");

            ModUI.BeginGroup("Folders");
            GUILayout.BeginHorizontal(GUILayout.Height(40f));
            if (ModUI.Button("GTWM"))
            {
                OpenPath(ModLoader.RootDir);
            }
            if (ModUI.Button("Config"))
            {
                OpenPath(ModLoader.ConfigDir);
            }
            if (ModUI.Button("Logs"))
            {
                OpenPath(ModLoader.LogsDir);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            ModUI.EndGroup("Write your own: reference GTWModLoader.dll, subclass GTWM.Mod, add [ModInfo], and build into GTWM/mods. See README.md.");

            ModUI.EndScroll();
        }

        // ───────────────────────────── misc ─────────────────────────────

        private void OnModsChanged()
        {
            foreach (ModEntry entry in ModLoader.Mods)
            {
                if (_lastStates.TryGetValue(entry, out ModState before) && before != ModState.Error && entry.State == ModState.Error)
                {
                    ShowToast($"{entry.Info.Name} stopped after an error  ·  {_toggleKey} for details", 6f);
                }
                _lastStates[entry] = entry.State;
            }
        }

        private void DrawToast()
        {
            if (Time.unscaledTime >= _toastUntil || string.IsNullOrEmpty(_toast))
            {
                return;
            }
            float alpha = Mathf.Clamp01((_toastUntil - Time.unscaledTime) * 2f);
            var style = new GUIStyle(ModUI.HudText) { fontSize = 13 };
            float w = style.CalcSize(new GUIContent(_toast)).x + 40f;
            var r = new Rect((ModUI.ScreenWidth - w) * 0.5f, ModUI.ScreenHeight - 84f, w, 38f);
            ModUI.HudCard(r, 19f, alpha);
            ModUI.ShadowLabel(r, _toast, new Color(1f, 1f, 1f, alpha), style);
        }

        private void ShowToast(string text, float seconds)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void SetStatus(string text)
        {
            _status = text;
            _statusUntil = Time.unscaledTime + 5f;
            Log.Info(text);
        }

        private void Queue(Action action) => _pending.Enqueue(action);

        private void OpenPath(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(path);
            }
            catch
            {
                Application.OpenURL("file:///" + path.Replace('\\', '/'));
            }
        }

        // IMGUI rich text would otherwise interpret <...> inside log messages.
        private static string Escape(string s) => s.Replace("<", "<​");
    }
}
