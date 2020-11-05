using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;  
using System.Windows.Forms;

namespace PuppetMasterGUI
{
    public partial class StartingPage : Form
    {
        private PMLogic logic;

        public StartingPage(PMLogic logic)
        {
            this.logic = logic;
            InitializeComponent();
        }

        private void browseButton_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog.ShowDialog();
            // if a file is selected
            if (result == DialogResult.OK)
            {
                // Set the selected file URL to the textbox
                configurationPath.Text = openFileDialog.FileName;
            }
        }

        private void runScriptButton_Click(object sender, EventArgs e)
        {
            if (configurationPath.Text == "")
            {

            }
            logic.scriptFilename = configurationPath.Text;
            
            try
            {
                logic.RunScript();
                LoadCommandPage();

            } catch (Exception ex)
            {
                MessageBox.Show("Oops! Something went wrong. Please try again.\n\nException: " + ex.GetType() + "\nMessage: " + ex.Message);
            }
        }

        private void configurationPath_TextChanged(object sender, EventArgs e)
        {
            SetScriptButton();
        }


        // ==== Auxiliary Methods ====
        private void SetScriptButton()
        {
            runScriptButton.Enabled = (configurationPath.Text != "" && File.Exists(configurationPath.Text));
        }

        private void LoadCommandPage()
        {
            var frm = new CommandsPage();
            frm.Location = this.Location;
            frm.StartPosition = FormStartPosition.Manual;
            frm.FormClosing += delegate { this.Show(); };
            frm.Show();
            this.Hide();
        }

    }
}
