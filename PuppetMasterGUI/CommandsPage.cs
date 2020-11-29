using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PuppetMasterGUI
{
    public partial class CommandsPage : Form
    {
        private PMLogic logic;
        List<string> servers;

        public CommandsPage(PMLogic pmLogic)
        {
            this.logic = pmLogic;
            InitializeComponent();

            servers = logic.GetServerIdsList();

            servers.ForEach(server => ServerList.Items.Add(server));   
        }

        private void CrashButton_Click(object sender, EventArgs e)
        {
            String command = $"Crash {ServerIdTextbox.Text}";
            logic.SendCrashCommand(command);
        }

        private void FreezeButton_Click(object sender, EventArgs e)
        {
            String command = $"Freeze {ServerIdTextbox.Text}";
            logic.SendFreezeCommand(command);
        }

        private void UnfreezeButton_Click(object sender, EventArgs e)
        {
            String command = $"Unfreeze {ServerIdTextbox.Text}";
            logic.SendUnfreezeCommand(command);
        }

        private void ServerIdTextbox_TextChanged(object sender, EventArgs e)
        {
            if (ServerIdTextbox.TextLength > 0 && IsInServerList(ServerIdTextbox.Text))
            {
                CrashButton.Enabled = true;
                FreezeButton.Enabled = true;
                UnfreezeButton.Enabled = true;
                ServerIdLabel.Text = "";
            } else
            {
                CrashButton.Enabled = false;
                FreezeButton.Enabled = false;
                UnfreezeButton.Enabled = false;
                ServerIdLabel.Text = "Please Insert a Valid Server Id";
            }
        }

        private bool IsInServerList(string serverId)
        {
            foreach (string s in servers) {
                if (s.Equals(serverId))
                    return true;
            }
            return false;
        }

        private void StatusButton_Click(object sender, EventArgs e)
        {
            // TODO fix status command on server side
            //logic.SendStatusCommand();
        }

        private void ServerIdLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
