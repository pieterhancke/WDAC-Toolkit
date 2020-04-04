// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// jogeurte 11/19

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Serialization; 
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ComponentModel; 

namespace WDAC_Wizard
{
    public partial class SigningRules_Control : UserControl
    {
        // CI Policy objects
        public WDAC_Policy Policy;
        private PolicyCustomRules PolicyCustomRule;     // One instance of a custom rule. Appended to Policy.CustomRules
        private List<string> AllFilesinFolder;          // List to track all files in a folder 

        public Logger Log;
        private MainWindow _MainWindow;
        private string XmlPath;

        private int RowSelected; // Data grid row number selected by the user 
        private int rowInEdit = -1;
        private DisplayObject displayObjectInEdit;

        // Declare an ArrayList to serve as the data store. 
        public System.Collections.ArrayList displayObjects =
            new System.Collections.ArrayList();

        public SigningRules_Control(MainWindow pMainWindow)
        {
            InitializeComponent();
            this.Policy = new WDAC_Policy();
            this.PolicyCustomRule = new PolicyCustomRules();
            this.AllFilesinFolder = new List<string>(); 

            this._MainWindow = pMainWindow;
            this._MainWindow.RedoFlowRequired = false; 
            this.Log = this._MainWindow.Log;
            this.RowSelected = -1; 
        }

        /// <summary>
        /// Reads in the template or supplemental policy signed file rules and displays them to the user in the DataGridView. 
        /// Executing on UserControl load.
        /// </summary>
        private void SigningRules_Control_Load(object sender, EventArgs e)
        {
            // Try to read CI policy. Fail out gracefully if corrupt and return to home screen
            if(!readSetRules(sender, e))
                return;
            displayRules();
        }

        
        //private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)

        /// <summary>
        /// Shows the Custom Rules Panel when the user clicks on +Custom Rules. 
        /// </summary>
        private void label_AddCustomRules_Click(object sender, EventArgs e)
        {
            label_AddCustomRules.Text = "- Custom Rules";
            CustomRuleConditionsPanel customRulesPanel = new CustomRuleConditionsPanel(this);
            customRulesPanel.Show();

            this.Log.AddInfoMsg("--- Create Custom Rules Selected ---"); 
        }

        
        /// <summary>
        /// Diplays the signing rules from the template policy or the supplemental policy in the DataGridView on Control Load. 
        /// </summary>
        private void displayRules()
        {
            string friendlyName = String.Empty;    //  this.Policy.Signers[signerID].Name;
            string action = String.Empty;
            string level = String.Empty; 
            string exceptionList = String.Empty;
            string fileAttrList = String.Empty;
            string signerID = String.Empty;

            // Increase efficiency by constructing signers dictionary hint
            Dictionary<string, string> signersDict = new Dictionary<string, string>();
            Dictionary<string, string> fileRulesDict = new Dictionary<string, string>();

            foreach (var signer in this.Policy.siPolicy.Signers)
                signersDict.Add(signer.ID, signer.Name);

            //Console.WriteLine(this.Policy.siPolicy.FileRules[0]); 

            // Process publisher rules first:
            foreach (SigningScenario scenario in this.Policy.siPolicy.SigningScenarios)
            {
                for(int i=0; i< scenario.ProductSigners.AllowedSigners.AllowedSigner.Length; i++)
                {
                    // Get signer attributes
                    signerID = scenario.ProductSigners.AllowedSigners.AllowedSigner[i].SignerId;
                    friendlyName = signersDict[signerID];    //  this.Policy.Signers[signerID].Name;
                    action = "Allow"; // signer.ID; //  this.Policy.Signers[signerID].Action;
                    level = "Publisher"; 

                    // Get signer exceptions - if applicable
                     if (scenario.ProductSigners.AllowedSigners.AllowedSigner[i].ExceptDenyRule != null)
                     {
                         // Iterate through all of the exceptions, get the ID and map to filename
                         for(int j = 0; j< scenario.ProductSigners.AllowedSigners.AllowedSigner[i].ExceptDenyRule.Length; j++ )
                         {
                             exceptionList += String.Format("{0}, ", scenario.ProductSigners.AllowedSigners
                                 .AllowedSigner[i].ExceptDenyRule[j].DenyRuleID);
                         }
                     }

                     // Get associated/affected files
                     /*if (this.Policy.Signers[signerID].FileAttributes.Count > 0)
                     {
                         // Iterate through all of the exceptions, get the ID and map to filename
                         foreach (string ruleID in this.Policy.Signers[signerID].FileAttributes)
                         {
                             string fileAttrName = this.Policy.FileRules[ruleID].FileName;
                             if (fileAttrName == "*") // applies to all files with ver > min ver
                                 fileAttrName = "All files";
                             string minVersion = this.Policy.FileRules[ruleID].MinimumFileVersion;
                             fileAttrList += String.Format("{0} (v{1}+), ", fileAttrName, minVersion);
                         }
                     }*/

                    this.displayObjects.Add(new DisplayObject(action, level, friendlyName, fileAttrList, exceptionList));
                    this.rulesDataGrid.RowCount += 1; 

                    // Get row index #, Scroll to new row index
                    //index = rulesDataGrid.Rows.Add();
                }

                // Process file rules (hash, file path, file name)
                foreach (var signingScenario in this.Policy.SigningScenarios)
                {
                    foreach (var ruleID in signingScenario.FileRules)
                    {
                        if (this.Policy.FileRules[ruleID].FriendlyName.Contains("Page")
                            || this.Policy.FileRules[ruleID].FriendlyName.Contains("Sha256")) // Skip the 3 other hash instances -- no need to show to user (saves time)
                            continue;
                        else
                        {
                            
                            // Write to UI
                            action = this.Policy.FileRules[ruleID].Action;
                            level = this.Policy.FileRules[ruleID].GetRuleType().ToString();

                            if (this.Policy.FileRules[ruleID].GetRuleType() == PolicyFileRules.RuleType.FileName &&
                                this.Policy.FileRules[ruleID].FileName != null)
                                friendlyName = this.Policy.FileRules[ruleID].FileName;
                            else
                                friendlyName = this.Policy.FileRules[ruleID].FriendlyName;
                        }
                    }

                    this.displayObjects.Add(new DisplayObject(action, level, friendlyName, fileAttrList, exceptionList));
                    this.rulesDataGrid.RowCount += 1;

                }

            }

            // Scroll to bottom of table
            rulesDataGrid.FirstDisplayedScrollingRowIndex = this.rulesDataGrid.RowCount-1;
        }


        /// <summary>
        /// Method to parse either the template or supplemental policy and store into the custom data structures for digestion. 
        /// </summary>
        private bool readSetRules(object sender, EventArgs e)
        {
            // Always going to have to parse an XML file - either going to be pre-exisiting policy (edit mode, supplmental policy) or template policy (new base)
            if (this._MainWindow.Policy.TemplatePath != null)
                this.XmlPath = this._MainWindow.Policy.TemplatePath;
            else
                this.XmlPath = this._MainWindow.Policy.EditPolicyPath;
            this.Log.AddInfoMsg("--- Reading Set Signing Rules Beginning ---");

            try
            {
                // Read File
                XmlSerializer serializer = new XmlSerializer(typeof(SiPolicy));
                StreamReader reader = new StreamReader(this.XmlPath);
                this.Policy.siPolicy = (SiPolicy)serializer.Deserialize(reader);
                reader.Close();

            } 
            catch (Exception exp)
            {
                this.Log.AddErrorMsg("ReadSetRules() has encountered an error: ", exp);
                // Prompt user for additional confirmation
                DialogResult res = MessageBox.Show("The Wizard is unable to read your CI policy xml file. The policy XML is corrupted. ",
                    "Parsing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (res == DialogResult.OK)
                    this._MainWindow.ResetWorkflow(sender, e);
                return false; 
            }
            
            bubbleUp(); // all original signing rules are set in MainWindow object - ...
                        //all mutations to rules are from here on completed using cmdlets
            return true; 
        }

        /// <summary>
        /// A reccursive function to list all of the PE files in a folder and subfolders to create allow and 
        /// deny rules on folder path rules. Stores filepaths in this.AllFilesinFolder. 
        /// </summary>
        /// <param name="folder">The folder path </param>
        private void ProcessAllFiles(string folder)
        {
            // All extensions we look for
            var ext = new List<string> { ".exe", ".ps1", ".bat", ".vbs", ".js" };
            foreach (var file in Directory.GetFiles(folder,"*.*").Where(s => ext.Contains(Path.GetExtension(s))))
                this.AllFilesinFolder.Add(file);

            // Reccursively grab files from sub dirs
            foreach (string subDir in Directory.GetDirectories(folder))
            {
                try
                {
                    ProcessAllFiles(subDir);
                }
                catch(Exception e)
                {
                    Console.WriteLine(String.Format("Exception found: {0} ", e));
                }
            }

            //PolicyCustomRule.FolderContents = Directory.GetFiles(PolicyCustomRule.ReferenceFile, "*.*", SearchOption.AllDirectories)
        }

        /// <summary>
        /// Method to set all of the MainWindow objects to the local instances of the Policy helper class objects.
        /// </summary>
        public void bubbleUp()
        {
            // Passing rule, signing scenarios, etc datastructs to MainWindow class
           this._MainWindow.Policy.CISigners = this.Policy.CISigners;
           this._MainWindow.Policy.EKUs = this.Policy.EKUs;
           this._MainWindow.Policy.FileRules = this.Policy.FileRules;
           this._MainWindow.Policy.Signers = this.Policy.Signers;
           this._MainWindow.Policy.SigningScenarios = this.Policy.SigningScenarios;
           this._MainWindow.Policy.UpdateSigners = this.Policy.UpdateSigners;
           this._MainWindow.Policy.SupplementalSigners = this.Policy.SupplementalSigners;
           this._MainWindow.Policy.CISigners = this.Policy.CISigners;
           this._MainWindow.Policy.PolicySettings = this.Policy.PolicySettings;
           this._MainWindow.Policy.CustomRules = this.Policy.CustomRules;

        }

        /// <summary>
        /// Removes the highlighted rule row in the this.rulesDataGrid DataGridView. 
        /// Can only be executed on custom rules from this session. 
        /// </summary>
        private void deleteButton_Click(object sender, EventArgs e)
        {
  
            // Get info about the rule user wants to delete: row index and value
            int rowIdx = this.rulesDataGrid.CurrentCell.RowIndex;

            string ruleName = (String)this.rulesDataGrid["Column_Name", rowIdx].Value;
            string ruleType = (String)this.rulesDataGrid["Column_Level", rowIdx].Value;

            if (String.IsNullOrEmpty(ruleName) && String.IsNullOrEmpty(ruleType)) // Not a valid rule -- break
                return; 

            // Prompt the user for additional deletion confirmation
            DialogResult res = MessageBox.Show(String.Format("Are you sure you want to delete the '{0}' rule?", ruleName), "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (res == DialogResult.Yes)
            {
                var ruleIDs = new List<string>();

                // Convert ruleName to SignerID to delete from //Signers and //SigningScenarios
                // Handle both Signer template rules and custom rules
                Dictionary<string, PolicySigners>.KeyCollection _keys = this.Policy.Signers.Keys;
                Dictionary<string, PolicyFileRules>.KeyCollection _fileRulekeys = this.Policy.FileRules.Keys;

                if (ruleType == "Publisher")
                {
                    foreach (var signer in this.Policy.siPolicy.Signers)
                    {
                        if (ruleName == signer.Name)
                            ruleIDs.Add(signer.ID);
                    }
                }

                else
                {
                    // Get list of IDs for related rules. ie. Deleting one hash should delete all four hash values.
                    if (ruleType == "Hash")
                        ruleName = ruleName.Substring(0, ruleName.IndexOf("Hash") - 1);

                    foreach (var key in _fileRulekeys)
                    {
                        var pFileRule = this.Policy.FileRules[key];
                        if (pFileRule.FriendlyName.Contains(ruleName))
                            ruleIDs.Add(key);

                        foreach (var fileRule in this.Policy.siPolicy.FileRules)
                        {
                            //if(fileRule.)
                        }
                    }
                }

                // Remove from DisplayObject
                if (rowIdx < this.displayObjects.Count)
                    this.displayObjects.RemoveAt(rowIdx);


                // Only structure we have to remove the rule from is the one that is used in writing rules -- custom rules
                for (int i = this.Policy.CustomRules.Count - 1; i >= 0; i--)
                {
                    if (this.Policy.CustomRules[i].RowNumber == rowIdx)
                        this.Policy.CustomRules.Remove(this.Policy.CustomRules[i]); // Remove from structs
                }

                // Try to delete the rule from the doc
                XmlDocument doc = new XmlDocument();
                doc.Load(this.XmlPath); // Reading from the template (either one of the 3 bases or editing)


                // A friendly name can have multiple references in the doc -- remove each one
                // Skips section if custom rule
                if (ruleType == "Publisher")
                {
                    // Signer specific
                    XmlNodeList signerNodeList = doc.GetElementsByTagName("Signer");
                    XmlNodeList signingScenarioAllowList = doc.GetElementsByTagName("AllowedSigner");
                    XmlNodeList signingScenarioDenyList = doc.GetElementsByTagName("DeniedSigner");
                    foreach (var ruleID in ruleIDs)
                    {
                        for (int i = signerNodeList.Count - 1; i >= 0; i--) // Traverse through xml elements and delete signers == ruleID
                        {
                            if (signerNodeList[i].OuterXml.Contains(ruleID))
                                signerNodeList[i].ParentNode.RemoveChild(signerNodeList[i]);
                        }

                        for (int i = signingScenarioAllowList.Count - 1; i >= 0; i--) // Remove from signing scenarios too
                        {
                            if (signingScenarioAllowList[i].OuterXml.Contains(ruleID))
                                signingScenarioAllowList[i].ParentNode.RemoveChild(signingScenarioAllowList[i]);
                        }

                        for (int i = signingScenarioDenyList.Count - 1; i >= 0; i--) // Remove from signing scenarios too
                        {
                            if (signingScenarioDenyList[i].OuterXml.Contains(ruleID))
                                signingScenarioDenyList[i].ParentNode.RemoveChild(signingScenarioDenyList[i]);
                        }

                    }
                }

                else
                {
                    // Filerule specific
                    XmlNodeList allowFileRuleNodeList = doc.GetElementsByTagName("Allow"); // in <FileRules>
                    XmlNodeList denyFileRuleNodeList = doc.GetElementsByTagName("Deny");   // in <FileRules>
                    XmlNodeList fileAttrNodeList = doc.GetElementsByTagName("FileRuleRef"); // in <SigningScnearios-->FileRulesRef>
                    foreach (var ruleID in ruleIDs)
                    {
                        for (int i = allowFileRuleNodeList.Count - 1; i >= 0; i--) // Traverse through xml elements and delete signers == ruleID
                        {
                            if (allowFileRuleNodeList[i].OuterXml.Contains(ruleID))
                                allowFileRuleNodeList[i].ParentNode.RemoveChild(allowFileRuleNodeList[i]);
                        }

                        for (int i = denyFileRuleNodeList.Count - 1; i >= 0; i--) // Remove from file rule
                        {
                            if (denyFileRuleNodeList[i].OuterXml.Contains(ruleID))
                                denyFileRuleNodeList[i].ParentNode.RemoveChild(denyFileRuleNodeList[i]);
                        }

                        for (int i = fileAttrNodeList.Count - 1; i >= 0; i--) // Remove from signing scenarios too
                        {
                            if (fileAttrNodeList[i].OuterXml.Contains(ruleID))
                                fileAttrNodeList[i].ParentNode.RemoveChild(fileAttrNodeList[i]);
                        }

                    }
                }

                // Delete from UI elements:
                this.rulesDataGrid.Rows.RemoveAt(rowIdx);
                doc.Save(this.XmlPath);
            } 
        }

        /// <summary>
        /// Highlights the row of data in the DataGridView
        /// </summary>
        private void DataClicked(object sender, DataGridViewCellEventArgs e)
        {
            // Remove highlighting from previous selected row
            DataGridViewCellStyle defaultCellStyle = new DataGridViewCellStyle();
            defaultCellStyle.BackColor = Color.White;
            if(this.RowSelected > 0 && this.RowSelected < this.rulesDataGrid.Rows.Count)
                this.rulesDataGrid.Rows[this.RowSelected].DefaultCellStyle = defaultCellStyle; 

            // Highlight the row to show user feedback
            DataGridViewCellStyle highlightCellStyle = new DataGridViewCellStyle();
            highlightCellStyle.BackColor = Color.FromArgb(0, 120, 215); 
            DataGridViewRow customRow = this.rulesDataGrid.CurrentRow;
            this.rulesDataGrid.Rows[customRow.Index].DefaultCellStyle = highlightCellStyle;
            this.RowSelected = customRow.Index; 
            
        }

        /// <summary>
        /// Called when DataGridView needs to paint data
        /// </summary>
        private void rulesDataGrid_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            // If this is the row for new records, no values are needed.
            if (e.RowIndex == this.rulesDataGrid.RowCount - 1) return;

            DisplayObject displayObject = null;

            // Store a reference to the Customer object for the row being painted.
            if (e.RowIndex == rowInEdit)
            {
                displayObject = this.displayObjectInEdit;
            }
            else
            {
                displayObject = (DisplayObject)this.displayObjects[e.RowIndex];
            }

            // Set the cell value to paint using the Customer object retrieved.
            switch (this.rulesDataGrid.Columns[e.ColumnIndex].Name)
            {
                case "column_Action":
                    e.Value = displayObject.Action;
                    break;

                case "column_Level":
                    e.Value = displayObject.Level;
                    break;

                case "Column_Name":
                    e.Value = displayObject.Name;
                    break;

                case "Column_Files":
                    e.Value = displayObject.Files;
                    break;

                case "Column_Exceptions":
                    e.Value = displayObject.Exceptions;
                    break;
            }
        }

    }

    // Class for the datastore
    public class DisplayObject
    {
        public string Action;
        public string Level;
        public string Name;
        public string Files;
        public string Exceptions; 

        public DisplayObject()
        {
            this.Action = String.Empty;
            this.Level = String.Empty;
            this.Name = String.Empty;
            this.Files = String.Empty;
            this.Exceptions = String.Empty;
        }

        public DisplayObject(string action, string level, string name, string files, string exceptions)
        {
            this.Action = action;
            this.Level = level;
            this.Name = name;
            this.Files = files;
            this.Exceptions = exceptions;
        }
    }

}
