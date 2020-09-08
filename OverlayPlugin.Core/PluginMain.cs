using Advanced_Combat_Tracker;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RainbowMage.HtmlRenderer;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RainbowMage.OverlayPlugin.Overlays;
using RainbowMage.OverlayPlugin.EventSources;
using RainbowMage.OverlayPlugin.NetworkProcessors;
using RainbowMage.OverlayPlugin.Integration;

namespace RainbowMage.OverlayPlugin
{
    public class PluginMain
    {
        private TinyIoCContainer _container;
        private ILogger _logger;
        TabPage tabPage;
        Label label;
        ControlPanel controlPanel;

        TabPage wsTabPage;
        WSConfigPanel wsConfigPanel;

        Timer initTimer;
        Timer configSaveTimer;

        internal PluginConfig Config { get; private set; }
        internal List<IOverlay> Overlays { get; private set; }
        internal event EventHandler OverlaysChanged;

        internal string PluginDirectory { get; private set; }

        public PluginMain(string pluginDirectory, Logger logger, TinyIoCContainer container)
        {
            _container = container;
            PluginDirectory = pluginDirectory;
            _logger = logger;

            configSaveTimer = new Timer();
            configSaveTimer.Interval = 300000; // 5 minutes
            configSaveTimer.Tick += (o, e) => SaveConfig();

            _container.Register(this);
        }

        /// <summary>
        /// プラグインが有効化されたときに呼び出されます。
        /// </summary>
        /// <param name="pluginScreenSpace"></param>
        /// <param name="pluginStatusText"></param>
        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            try
            {
                this.tabPage = pluginScreenSpace;
                this.label = pluginStatusText;

#if DEBUG
                _logger.Log(LogLevel.Warning, "##################################");
                _logger.Log(LogLevel.Warning, "    THIS IS THE DEBUG BUILD");
                _logger.Log(LogLevel.Warning, "##################################");
#endif

                _logger.Log(LogLevel.Info, "InitPlugin: PluginDirectory = {0}", PluginDirectory);

#if DEBUG
                Stopwatch watch = new Stopwatch();
                watch.Start();
#endif

                // ** Init phase 1
                // Only init stuff here that works without the FFXIV plugin or addons (event sources, overlays).
                // Everything else should be initialized in the second phase.
                // 1.a Stuff without state
                FFXIVExportVariables.Init();

                // 1.b Stuff with state
                _container.Register(new NativeMethods(_container));
                _container.Register(new EventDispatcher(_container));
                _container.Register(new Registry(_container));
                _container.Register(new KeyboardHook(_container));
                LoadConfig();
                _container.Register(new WSServer(_container));

#if DEBUG
                _logger.Log(LogLevel.Debug, "Component init and config load took {0}s.", watch.Elapsed.TotalSeconds);
                watch.Reset();
#endif

                try
                {
                    Renderer.Initialize(PluginDirectory, ActGlobals.oFormActMain.AppDataFolder.FullName, Config.ErrorReports);
                }
                catch (Exception e)
                {
                    _logger.Log(LogLevel.Error, "InitPlugin: {0}", e);
                }

#if DEBUG
                _logger.Log(LogLevel.Debug, "CEF init took {0}s.", watch.Elapsed.TotalSeconds);
                watch.Reset();
#endif

                // プラグイン間のメッセージ関連
                OverlayApi.BroadcastMessage += (o, e) =>
                {
                    Task.Run(() =>
                    {
                        foreach (var overlay in this.Overlays)
                        {
                            overlay.SendMessage(e.Message);
                        }
                    });
                };
                OverlayApi.SendMessage += (o, e) =>
                {
                    Task.Run(() =>
                    {
                        var targetOverlay = this.Overlays.FirstOrDefault(x => x.Name == e.Target);
                        if (targetOverlay != null)
                        {
                            targetOverlay.SendMessage(e.Message);
                        }
                    });
                };
                OverlayApi.OverlayMessage += (o, e) =>
                {
                    Task.Run(() =>
                    {
                        var targetOverlay = this.Overlays.FirstOrDefault(x => x.Name == e.Target);
                        if (targetOverlay != null)
                        {
                            targetOverlay.OverlayMessage(e.Message);
                        }
                    });
                };

#if DEBUG
                watch.Reset();
#endif
                
                // Setup the UI
                this.controlPanel = new ControlPanel(_container);
                this.controlPanel.Dock = DockStyle.Fill;
                this.tabPage.Controls.Add(this.controlPanel);
                this.tabPage.Name = "OverlayPlugin";

                this.wsConfigPanel = new WSConfigPanel(_container);
                this.wsConfigPanel.Dock = DockStyle.Fill;

                this.wsTabPage = new TabPage("OverlayPlugin WSServer");
                this.wsTabPage.Controls.Add(wsConfigPanel);
                ((TabControl)this.tabPage.Parent).TabPages.Add(this.wsTabPage);
                
                _logger.Log(LogLevel.Info, "InitPlugin: Initialized.");
                this.label.Text = "Initialized.";

                // Fire off the update check (which runs in the background)
                if (Config.UpdateCheck)
                {
                    Updater.Updater.PerformUpdateIfNecessary(PluginDirectory, _container);
                }

                // Load our presets
                try {
#if DEBUG
                    var presetFile = Path.Combine(PluginDirectory, "libs", "resources", "presets.json");
    #else
                    var presetFile = Path.Combine(PluginDirectory, "resources", "presets.json");
    #endif
                    var presetData = "{}";
                
                    try
                    {
                        presetData = File.ReadAllText(presetFile);
                    } catch(Exception ex)
                    {
                        _logger.Log(LogLevel.Error, string.Format(Resources.ErrorCouldNotLoadPresets, ex));
                    }
            
                    var presets = JsonConvert.DeserializeObject<Dictionary<string, OverlayPreset>>(presetData);
                    var registry = _container.Resolve<Registry>();
                    foreach (var pair in presets)
                    {
                        pair.Value.Name = pair.Key;
                        registry.RegisterOverlayPreset2(pair.Value);
                    }
                } catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, string.Format("Failed to load presets: {0}", ex));
                }

                initTimer = new Timer();
                initTimer.Interval = 300;
                initTimer.Tick += async (o, e) =>
                {
                    if (ActGlobals.oFormActMain == null)
                    {
                        // Something went really wrong.
                        initTimer.Stop();
                    } else if (ActGlobals.oFormActMain.InitActDone && ActGlobals.oFormActMain.Handle != IntPtr.Zero)
                    {
                        try
                        {
                            initTimer.Stop();

                            // ** Init phase 2

                            // Initialize the parser in the second phase since it needs the FFXIV plugin.
                            // If OverlayPlugin is placed above the FFXIV plugin, it won't be available in the first
                            // phase but it'll be loaded by the time we enter the second phase.
                            _container.Register(new FFXIVRepository(_container));
                            _container.Register(new NetworkParser(_container));
                            _container.Register(new TriggIntegration(_container));

                            // This timer runs on the UI thread (it has to since we create UI controls) but LoadAddons()
                            // can block for some time. We run it on the background thread to avoid blocking the UI.
                            // We can't run LoadAddons() in the first init phase since it checks other ACT plugins for
                            // addons. Plugins below OverlayPlugin wouldn't have been loaded in the first init phase.
                            // However, in the second phase all plugins have been loaded which means we can look for addons
                            // in that list.
                            await Task.Run(LoadAddons);

#if DEBUG
                            _container.Register(new UnstableNewLogLines(_container));
#endif


                            ActGlobals.oFormActMain.Invoke((Action)(() =>
                            {
                                try
                                {
                                    // Now that addons have been loaded, we can finish the overlay setup.
                                    InitializeOverlays();
                                    controlPanel.InitializeOverlayConfigTabs();
                                
                                    _container.Register(new OverlayHider(_container));
                                    _container.Register(new OverlayZCorrector(_container));

                                    // WSServer has to start after the LoadAddons() call because clients can connect immediately
                                    // after it's initialized and that requires the event sources to be initialized.
                                    if (Config.WSServerRunning)
                                    {
                                        _container.Resolve<WSServer>().Start();
                                    }

                                    configSaveTimer.Start();
                                } catch (Exception ex)
                                {
                                    _logger.Log(LogLevel.Error, "InitPlugin: {0}", ex);
                                }
                            }));
                        }
                        catch (Exception ex)
                        {
                            _logger.Log(LogLevel.Error, "InitPlugin: {0}", ex);
                        }
                    }
                };
                initTimer.Start();
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, "InitPlugin: {0}", e.ToString());
                MessageBox.Show(e.ToString());

                throw;
            }
        }

        /// <summary>
        /// コンフィグのオーバーレイ設定を基に、オーバーレイを初期化・登録します。
        /// </summary>
        private void InitializeOverlays()
        {
            // オーバーレイ初期化
            this.Overlays = new List<IOverlay>();
            foreach (var overlayConfig in this.Config.Overlays)
            {
                var parameters = new NamedParameterOverloads();
                parameters["config"] = overlayConfig;
                parameters["name"] = overlayConfig.Name;

                var overlay = (IOverlay) _container.Resolve(overlayConfig.OverlayType, parameters);
                if (overlay != null)
                {
                    RegisterOverlay(overlay);
                }
                else
                {
                    _logger.Log(LogLevel.Error, "InitPlugin: Could not find addon for {0}.", overlayConfig.Name);
                }
            }
        }

        /// <summary>
        /// オーバーレイを登録します。
        /// </summary>
        /// <param name="overlay"></param>
        internal void RegisterOverlay(IOverlay overlay)
        {
            overlay.OnLog += (o, e) => _logger.Log(e.Level, e.Message);
            overlay.Start();
            this.Overlays.Add(overlay);

            OverlaysChanged?.Invoke(this, null);
        }

        /// <summary>
        /// 登録されているオーバーレイを削除します。
        /// </summary>
        /// <param name="overlay">削除するオーバーレイ。</param>
        internal void RemoveOverlay(IOverlay overlay)
        {
            this.Overlays.Remove(overlay);
            overlay.Dispose();

            OverlaysChanged?.Invoke(this, null);
        }

        /// <summary>
        /// プラグインが無効化されたときに呼び出されます。
        /// </summary>
        public void DeInitPlugin()
        {
            SaveConfig(true);
            _container.Resolve<OverlayZCorrector>().DeInit();

            if (controlPanel != null) controlPanel.Dispose();

            if (Overlays != null)
            {
                foreach (var overlay in this.Overlays)
                {
                    overlay.Dispose();
                }

                this.Overlays.Clear();
            }

            try { _container.Resolve<WSServer>().Stop(); }
            catch { }

            if (this.wsTabPage != null && this.wsTabPage.Parent != null)
                ((TabControl)this.wsTabPage.Parent).TabPages.Remove(this.wsTabPage);

            _logger.Log(LogLevel.Info, "DeInitPlugin: Finalized.");
            if (this.label != null) this.label.Text = "Finalized.";
        }

        private void LoadAddons()
        {
            try
            {
                var registry = _container.Resolve<Registry>();
                _container.Register(BuiltinEventConfig.LoadConfig(Config));

                // Make sure the event sources are ready before we load any overlays.
                registry.StartEventSource(new MiniParseEventSource(_container));
                registry.StartEventSource(new EnmityEventSource(_container));

                registry.RegisterOverlay<MiniParseOverlay>();
                registry.RegisterOverlay<SpellTimerOverlay>();
                registry.RegisterOverlay<LabelOverlay>();

                var version = typeof(PluginMain).Assembly.GetName().Version;
                var Addons = new List<IOverlayAddonV2>();
                var foundCactbot = false;

                foreach (var plugin in ActGlobals.oFormActMain.ActPlugins)
                {
                    if (plugin.pluginObj == null) continue;

                    try
                    {
                        if (plugin.pluginObj.GetType().GetInterface(typeof(IOverlayAddonV2).FullName) != null)
                        {
                            try
                            {
                                var addon = (IOverlayAddonV2)plugin.pluginObj;
                                addon.Init();

                                if (addon.ToString() == "Cactbot.PluginLoader")
                                {
                                    foundCactbot = true;
                                }

                                _logger.Log(LogLevel.Info, "LoadAddons: {0}: Initialized {1}", plugin.lblPluginTitle.Text, addon.ToString());
                            }
                            catch (Exception e)
                            {
                                _logger.Log(LogLevel.Error, "LoadAddons: {0}: {1}", plugin.lblPluginTitle.Text, e);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Log(LogLevel.Error, "LoadAddons: {0}: {1}", plugin.lblPluginTitle.Text, e);
                    }
                }

                // Only enable embedded Cactbot in debug / dev builds until I'm sure it's stable enough
                // for most users.
                #if DEBUG
                if (!foundCactbot)
                {
                    _logger.Log(LogLevel.Info, "LoadAddons: Enabling builtin Cactbot event source.");
                    registry.StartEventSource(new CactbotEventSource(_container));
                }
                #endif

                registry.StartEventSources();
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, "LoadAddons: {0}", e);
                Trace.WriteLine("LoadAddons: " + e.ToString());
            }
        }

        private void LoadConfig()
        {
            if (Config != null)
                return;

            try
            {
                Config = new PluginConfig(GetConfigPath(), _container);
            }
            catch (Exception e)
            {
                Config = null;
                _logger.Log(LogLevel.Error, "LoadConfig: {0}", e);
                return;
            }

            _container.Register(Config);
            _container.Register<IPluginConfig>(Config);
        }

        /// <summary>
        /// 設定を保存します。
        /// </summary>
        private void SaveConfig(bool force = false)
        {
            var registry = _container.Resolve<Registry>();
            if (Config == null || Overlays == null || registry.EventSources == null) return;

            try
            {
                foreach (var overlay in this.Overlays)
                {
                    overlay.SavePositionAndSize();
                }

                foreach (var es in registry.EventSources)
                {
                    if (es != null)
                        es.SaveConfig(Config);
                }

                _container.Resolve<BuiltinEventConfig>().SaveConfig(Config);
                Config.SaveJson(force);
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, "SaveConfig: {0}", e);
                MessageBox.Show(e.ToString());
            }
        }

        /// <summary>
        /// 設定ファイルのパスを取得します。
        /// </summary>
        /// <returns></returns>
        private static string GetConfigPath(bool xml = false)
        {
            var path = Path.Combine(
                ActGlobals.oFormActMain.AppDataFolder.FullName,
                "Config",
                "RainbowMage.OverlayPlugin.config." + (xml ? "xml" : "json"));

            return path;
        }
    }
}
