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
        public CommandsPage()
        {
            InitializeComponent();
        }

        private void CrashButton_Click(object sender, EventArgs e)
        {

        }

        private void FreezeButton_Click(object sender, EventArgs e)
        {

        }

        private void UnfreezeButton_Click(object sender, EventArgs e)
        {

        }

        private void ServerIdTextbox_TextChanged(object sender, EventArgs e)
        {
            if (ServerIdTextbox.TextLength > 0)
            {
                CrashButton.Enabled = true;
                FreezeButton.Enabled = true;
                UnfreezeButton.Enabled = true;
            } else
            {
                CrashButton.Enabled = false;
                FreezeButton.Enabled = false;
                UnfreezeButton.Enabled = false;
            }
        }
    }
}
