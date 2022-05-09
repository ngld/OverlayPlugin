using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RainbowMage.HtmlRenderer;
using Advanced_Combat_Tracker;
using System.Threading;
using System.Reflection;
using System.IO;

namespace RainbowMage.OverlayPlugin
{
    public partial class GeneralConfigTab : UserControl
    {
        readonly TinyIoCContainer container;
        readonly string pluginDirectory;
        readonly PluginConfig config;
        readonly ILogger logger;

        private DateTime lastClick;

        public GeneralConfigTab(TinyIoCContainer container)
        {
            InitializeComponent();
            Dock = DockStyle.Fill;

            this.container = container;
            pluginDirectory = container.Resolve<PluginMain>().PluginDirectory;
            config = container.Resolve<PluginConfig>();
            logger = container.Resolve<ILogger>();

            cbErrorReports.Checked = config.ErrorReports;
            cbUpdateCheck.Checked = config.UpdateCheck;
            cbHideOverlaysWhenNotActive.Checked = config.HideOverlaysWhenNotActive;
            cbHideOverlaysDuringCutscene.Checked = config.HideOverlayDuringCutscene;

            // Attach the event handlers only *after* loading the configuration because we'd otherwise trigger them ourselves.
            cbErrorReports.CheckedChanged += CbErrorReports_CheckedChanged;
            cbUpdateCheck.CheckedChanged += CbUpdateCheck_CheckedChanged;
            cbHideOverlaysWhenNotActive.CheckedChanged += cbHideOverlaysWhenNotActive_CheckedChanged;
            cbHideOverlaysDuringCutscene.CheckedChanged += cbHideOverlaysDuringCutscene_CheckedChanged;
        }

        public void SetReadmeVisible(bool visible)
        {
            lblReadMe.Visible = visible;
            lblNewUserWelcome.Visible = visible;
        }

        private void btnUpdateCheck_MouseClick(object sender, MouseEventArgs e)
        {
            // Shitty double-click detection. I'd love to have a proper double click event on buttons in WinForms. =/
            double timePassed = 1000;
            var now = DateTime.Now;

            if (lastClick != null)
            {
                timePassed = now.Subtract(lastClick).TotalMilliseconds;
            }

            lastClick = now;

            Task.Run(() =>
            {
                Thread.Sleep(500);

                if (lastClick != now) return;
                Updater.Updater.PerformUpdateIfNecessary(pluginDirectory, container, true, timePassed < 500);
            });
        }

        private void CbErrorReports_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (cbErrorReports.Checked)
                {
                    Renderer.EnableErrorReports(ActGlobals.oFormActMain.AppDataFolder.FullName);
                } else
                {
                    Renderer.DisableErrorReports(ActGlobals.oFormActMain.AppDataFolder.FullName);
                }
            } catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Failed to switch error reports: {ex}");
                cbErrorReports.Checked = !cbErrorReports.Checked;

                MessageBox.Show($"Failed to switch error reports: {ex}", "OverlayPlugin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            config.ErrorReports = cbErrorReports.Checked;

            MessageBox.Show("You have to restart ACT to apply this change.", "OverlayPlugin", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CbUpdateCheck_CheckedChanged(object sender, EventArgs e)
        {
            config.UpdateCheck = cbUpdateCheck.Checked;
        }

        private void cbHideOverlaysWhenNotActive_CheckedChanged(object sender, EventArgs e)
        {
            config.HideOverlaysWhenNotActive = cbHideOverlaysWhenNotActive.Checked;
            container.Resolve<OverlayHider>().UpdateOverlays();
        }

        private void cbHideOverlaysDuringCutscene_CheckedChanged(object sender, EventArgs e)
        {
            config.HideOverlayDuringCutscene = cbHideOverlaysDuringCutscene.Checked;
            container.Resolve<OverlayHider>().UpdateOverlays();
        }

        private void lnkGithubRepo_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(lnkGithubRepo.Text);
        }

        private void newUserWelcome_Click(object sender, EventArgs e)
        {

        }

        private void btnCactbotUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                var asm = Assembly.Load("CactbotEventSource");
                var checkerType = asm.GetType("Cactbot.VersionChecker");
                var loggerType = typeof(ILogger);
                var configType = asm.GetType("Cactbot.CactbotEventSourceConfig");

                var esList = container.Resolve<Registry>().EventSources;
                IEventSource cactbotEs = null;

                foreach (var es in esList)
                {
                    if (es.Name == "Cactbot Config" || es.Name == "Cactbot")
                    {
                        cactbotEs = es;
                        break;
                    }
                }

                if (cactbotEs == null)
                {
                    MessageBox.Show("Cactbot is loaded but it never registered with OverlayPlugin!", "Error");
                    return;
                }

                var cactbotConfig = cactbotEs.GetType().GetProperty("Config").GetValue(cactbotEs);
                configType.GetField("LastUpdateCheck").SetValue(cactbotConfig, DateTime.MinValue);

                var checker = checkerType.GetConstructor(new Type[] { loggerType }).Invoke(new object[] { cactbotEs });
                checkerType.GetMethod("DoUpdateCheck", new Type[] {configType}).Invoke(checker, new object[] { cactbotConfig });
            } catch(FileNotFoundException)
            {
                MessageBox.Show("Could not find Cactbot!", "Error");
            } catch(Exception ex)
            {
                MessageBox.Show("Failed: " + ex.ToString(), "Error");
            }
        }
    }
}
