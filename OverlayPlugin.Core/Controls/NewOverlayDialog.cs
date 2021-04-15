using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;

namespace RainbowMage.OverlayPlugin
{
    public partial class NewOverlayDialog : Form
    {
        public delegate bool ValidateNameDelegate(string name);

        public ValidateNameDelegate NameValidator { get; set; }
        public IOverlay SelectedOverlay { get; private set; }

        private PluginMain pluginMain;
        private Registry registry;
        private ILogger logger;
        private TinyIoCContainer container;
        private IOverlay preview;

        static Dictionary<string, string> overlayNames = new Dictionary<string, string>
        {
            { "Label", Resources.MapOverlayShortLabel },
            { "MiniParse", Resources.MapOverlayShortMiniParse },
            { "SpellTimer", Resources.MapOverlayShortSpellTimer },
        };

        Dictionary<string, OverlayPreset> presets = null;

        public NewOverlayDialog(TinyIoCContainer container)
        {
            InitializeComponent();

            pluginMain = container.Resolve<PluginMain>();
            registry = container.Resolve<Registry>();
            logger = container.Resolve<ILogger>();
            this.container = container;

            // Default validator
            NameValidator = (name) => { return name != null; };

            foreach (var overlayType in registry.Overlays)
            {
                var name = overlayType.Name;
                if (name.EndsWith("Overlay"))
                {
                    name = name.Substring(0, name.Length - 7);
                }

                if (overlayNames.ContainsKey(name)) {
                    name = overlayNames[name];
                }

                cbType.Items.Add(new KeyValuePair<string, Type>(name, overlayType));
            }

            cbType.DisplayMember = "Key";
            // Workaround for the special case where no overlay type has been registered.
            // That still indicates a bug but showing an empty combo box is better than crashing.
            if (cbType.Items.Count > 0)
                cbType.SelectedIndex = 0;

            presets = PreparePresetCombo(cbPreset);

            lblType.Visible = false;
            cbType.Visible = false;
            lblTypeDesc.Visible = false;

            textBox1.Focus();
        }

        private Dictionary<string, OverlayPreset> PreparePresetCombo(ComboBox cbPreset)
        {
            cbPreset.Items.Clear();

            foreach (var item in registry.OverlayPresets)
            {
                if (!item.IsOverlayPreset)
                    continue;
                cbPreset.Items.Add(item);
            }

            cbPreset.Items.Add(new OverlayPreset
            {
                Name = Resources.CustomPresetLabel,
                Url = "special:custom",
            });

            cbPreset.DisplayMember = "Name";
            return presets;
        }
        
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            preview?.Dispose();
            base.Dispose(disposing);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            var preset = (IOverlayPreset)cbPreset.SelectedItem;
            var name = textBox1.Text;

            if (NameValidator(name))
            {
                if (preset == null)
                {
                    MessageBox.Show(this, Resources.PromptSelectPreset, "OverlayPlugin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return;
                }

                if (preset.Url == "special:custom") 
                {
                    if (cbType.SelectedItem == null)
                    {
                        MessageBox.Show(this, Resources.PromptSelectOverlayType, "OverlayPlugin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                        return;
                    }

                    var overlayType = ((KeyValuePair<string, Type>)cbType.SelectedItem).Value;
                    var parameters = new NamedParameterOverloads();
                    parameters["config"] = null;
                    parameters["name"] = name;

                    SelectedOverlay = (IOverlay)container.Resolve(overlayType, parameters);
                } else
                {
                    // Store the current preview position and size in the config object...
                    preview.SavePositionAndSize();

                    // ... and update the name as well.
                    preview.Config.Name = name;

                    if (preview.GetType() == typeof(Overlays.MiniParseOverlay))
                    {
                        // Reconstruct the overlay config to get rid of any event handlers the previous overlay
                        // registered. I should probably write a proper Dispose() implementation in MiniParseOverlay instead
                        // but this is much shorter and does the job just as well.
                        var config = JsonConvert.DeserializeObject<Overlays.MiniParseOverlayConfig>(JsonConvert.SerializeObject(preview.Config));

                        // Reconstruct the overlay to reset the preview state.
                        SelectedOverlay = new Overlays.MiniParseOverlay(config, name, container);
                        if (config.Url == "")
                        {
                            // If the preview didn't load, we try again here to avoid ending up with an empty overlay.
#if DEBUG
                            var resourcesPath = "file:///" + pluginMain.PluginDirectory.Replace('\\', '/') + "/libs/resources";
#else
                            var resourcesPath = "file:///" + pluginMain.PluginDirectory.Replace('\\', '/') + "/resources";
#endif
                            SelectedOverlay.Navigate(preset.Url.Replace("%%", resourcesPath));
                        }
                    }
                    else
                    {
                        SelectedOverlay = preview;
                    }
                }

                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.None;
            }
        }

        private void cbPreset_SelectedIndexChanged(object sender, EventArgs e)
        {
            var preset = (IOverlayPreset)cbPreset.SelectedItem;

            if (preset.Url == "special:custom")
            {
                lblType.Visible = true;
                cbType.Visible = true;
                lblTypeDesc.Visible = true;
                lblPresetDescription.Visible = false;

                if (preview != null) preview.Visible = false;
            }
            else
            {
                lblType.Visible = false;
                cbType.Visible = false;
                lblTypeDesc.Visible = false;
                lblPresetDescription.Visible = true;

                if (preview != null) preview.Dispose();

                switch (preset.Type)
                {
                    case "MiniParse":
#if DEBUG
                        var resourcesPath = "file:///" + pluginMain.PluginDirectory.Replace('\\', '/') + "/libs/resources";
#else
                        var resourcesPath = "file:///" + pluginMain.PluginDirectory.Replace('\\', '/') + "/resources";
#endif
                        var config = new Overlays.MiniParseOverlayConfig(Resources.OverlayPreviewName)
                        {
                            ActwsCompatibility = preset.Supports.Count == 1 && preset.Supports.Contains("actws"),
                            Size = new Size(preset.Size[0], preset.Size[1]),
                            IsLocked = preset.Locked,
                        };

                        var presetUrl = preset.Url.Replace("%%", resourcesPath);
                        var overlay = new Overlays.MiniParseOverlay(config, config.Name, container);
                        overlay.Preview = true;
                        
                        var first = true;
                        overlay.Overlay.Renderer.BrowserLoad += (o, ev) =>
                        {
                            // Once the placeholder is ready, we load the actual overlay.
                            if (first)
                            {
                                first = false;
                                overlay.Navigate(presetUrl);
                            }
                        };
                        // Show a placeholder while the actual overlay is loading.
                        overlay.Navigate(resourcesPath + "/loading.html");

                        preview = overlay;
                        break;

                    default:
                        logger.Log(LogLevel.Error, string.Format(Resources.PresetUsesUnsupportedType, preset.Name, preset.Type));
                        break;
                }
            }
        }

        private class ComboItem
        {
            public Type OverlayType { get; set; }
            public string FriendlyName { get; set; }

            public ComboItem(Type overlayType, string friendlyName)
            {
                OverlayType = overlayType;
                FriendlyName = friendlyName;
            }
        }
    }

    [JsonObject(NamingStrategyType = typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
    class OverlayPreset : IOverlayPreset
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        [JsonIgnore]
        public int[] Size { get; set; }
        public bool Locked { get; set; }
        public bool IsOverlayPreset { get; set; } = true;
        public bool IsSharedPreset { get; set; } = true;
        public List<string> Supports { get; set; }

        [JsonExtensionData]
        [SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "JsonExtensionData modifies this variable")]
        private IDictionary<string, JToken> _others;

        [OnDeserialized]
        public void ParseOthers(StreamingContext ctx)
        {
            var size = _others["size"];
            Size = new int[2];

            for(int i = 0; i < 2; i++)
            {
                switch (size[i].Type) {
                    case JTokenType.Integer:
                        Size[i] = size[i].ToObject<int>();
                        break;
                    case JTokenType.String:
                        var part = size[i].ToString();
                        if (part.EndsWith("%"))
                        {
                            var percent = float.Parse(part.Substring(0, part.Length - 1)) / 100;
                            var screenSize = Screen.PrimaryScreen.WorkingArea;

                            Size[i] = (int) Math.Round(percent * (i == 0 ? screenSize.Width : screenSize.Height));
                        } else
                        {
                            Size[i] = int.Parse(part);
                        }
                        break;
                    default:
                        Size[i] = 300;
                        break;
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
