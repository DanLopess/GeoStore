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
            string command = $"Crash {ServerIdTextbox.Text}";
            try
            {
                logic.SendCrashCommand(command);
                ResetListBox();
                MessageBox.Show("Crash command ran successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Oops! Something went wrong. Please try again.\n\nException: " + ex.GetType() + "\nMessage: " + ex.Message);
            }
        }

        private void ResetListBox()
        {
            ServerIdTextbox.Text = "";
            ServerList.Items.Clear();
            servers = logic.GetServerIdsList();
            servers.ForEach(server => ServerList.Items.Add(server));
        }

        private void FreezeButton_Click(object sender, EventArgs e)
        {
            string command = $"Freeze {ServerIdTextbox.Text}";
            try
            {
                logic.SendFreezeCommand(command);
                MessageBox.Show("Freeze command ran successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Oops! Something went wrong. Please try again.\n\nException: " + ex.GetType() + "\nMessage: " + ex.Message);
            }
        }

        private void UnfreezeButton_Click(object sender, EventArgs e)
        {
            string command = $"Unfreeze {ServerIdTextbox.Text}";
            try 
            { 
                logic.SendUnfreezeCommand(command);
                MessageBox.Show("Unfreeze command ran successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Oops! Something went wrong. Please try again.\n\nException: " + ex.GetType() + "\nMessage: " + ex.Message);
            }
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
            try
            {
                logic.SendStatusCommand();
                MessageBox.Show("Status command ran successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Oops! Something went wrong. Please try again.\n\nException: " + ex.GetType() + "\nMessage: " + ex.Message);
            }
        }

        private void ServerIdLabel_Click(object sender, EventArgs e)
        {

        }
    }
}
