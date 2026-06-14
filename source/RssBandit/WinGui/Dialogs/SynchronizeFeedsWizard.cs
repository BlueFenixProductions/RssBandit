#region Version Info Header
/*
 * $Id$
 * $HeadURL$
 * Last modified by $Author$
 * Last modified at $Date$
 * $Revision$
 */
#endregion

using System;
using System.Windows.Forms;

using Divelements.WizardFramework;
using RssBandit.AppServices;
using RssBandit.Resources;
using RssBandit.WinGui.Controls;
using NewsComponents;


namespace RssBandit.WinGui.Dialogs
{

    /// <summary>
    /// SynchronizeFeedsWizard handles adding new FeedSources including the Windows RSS platform and Feedly Cloud.
    /// </summary>
    internal class SynchronizeFeedsWizard : Form
    {

        private readonly WindowSerializer _windowSerializer;

        #region Designer Form variables

        private CheckBox chkDisplayWelcome;
        private WizardPage pageFeedCredentials;
        private Label lblFeedCredentialsIntro;
        private Label lblUsername;
        private TextBox textUser;
        private Label lblPassword;
        private TextBox textPassword;
        private WizardPage pageStartImport;
        private RadioButton radioFeedlyCloud;
        private Button _btnImmediateFinish;
        private Wizard wizard;
		private Label label1;
        private WizardPage pageSourceName;
        private TextBox textFeedSourceName;
        private Label label2;
		private ErrorProvider errorProvider1;
		private LinkLabel fbrssLinkHelp;
        private System.ComponentModel.IContainer components;

        public FeedSourceType SelectedFeedSource
        {
            get
            {
                if (radioFeedlyCloud.Checked)
                {
                    return FeedSourceType.FeedlyCloud;
                }
                else
                {
                    return FeedSourceType.Unknown;
                }

            }

            private set
            {
                if (value == FeedSourceType.FeedlyCloud)
                {
                    radioFeedlyCloud.Checked = true;
                }
            }
        }

        public string UserName
        {
            get { return textUser.Text; }
        }

        public string Password
        {
            get { return textPassword.Text; }
        }

        public string FeedSourceName
        {
            get { return textFeedSourceName.Text; }
        }

        #endregion

        #region ctor's
       
        public SynchronizeFeedsWizard(FeedSourceType selectedFeedSource)
        {
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			this.Icon = Properties.Resources.Add;

            // form location management:
            _windowSerializer = new WindowSerializer(this);
            _windowSerializer.SaveOnlyLocation = true;
            _windowSerializer.SaveNoWindowState = true;

            this.wizard.SelectedPage = this.pageStartImport;

            if (selectedFeedSource != FeedSourceType.DirectAccess && selectedFeedSource != FeedSourceType.Unknown)
            {
                this.SelectedFeedSource = selectedFeedSource; 
            }
        }

        #endregion

        #region Dispose
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

                if (components != null)
                {
                    components.Dispose();
                }

            }

            base.Dispose(disposing);
        }
        #endregion

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SynchronizeFeedsWizard));
			this.chkDisplayWelcome = new System.Windows.Forms.CheckBox();
			this.pageFeedCredentials = new Divelements.WizardFramework.WizardPage();
			this.lblFeedCredentialsIntro = new System.Windows.Forms.Label();
			this.lblUsername = new System.Windows.Forms.Label();
			this.textUser = new System.Windows.Forms.TextBox();
			this.lblPassword = new System.Windows.Forms.Label();
			this.textPassword = new System.Windows.Forms.TextBox();
			this.pageSourceName = new Divelements.WizardFramework.WizardPage();
			this.textFeedSourceName = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.pageStartImport = new Divelements.WizardFramework.WizardPage();
			this.fbrssLinkHelp = new System.Windows.Forms.LinkLabel();
			this.label1 = new System.Windows.Forms.Label();
			this.radioFeedlyCloud = new System.Windows.Forms.RadioButton();
			this._btnImmediateFinish = new System.Windows.Forms.Button();
			this.wizard = new Divelements.WizardFramework.Wizard();
			this.errorProvider1 = new System.Windows.Forms.ErrorProvider(this.components);
			this.pageFeedCredentials.SuspendLayout();
			this.pageSourceName.SuspendLayout();
			this.pageStartImport.SuspendLayout();
			this.wizard.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).BeginInit();
			this.SuspendLayout();
			// 
			// chkDisplayWelcome
			// 
			this.chkDisplayWelcome.Checked = true;
			this.chkDisplayWelcome.CheckState = System.Windows.Forms.CheckState.Checked;
			resources.ApplyResources(this.chkDisplayWelcome, "chkDisplayWelcome");
			this.chkDisplayWelcome.Name = "chkDisplayWelcome";
			// 
			// pageFeedCredentials
			// 
			this.pageFeedCredentials.Controls.Add(this.lblFeedCredentialsIntro);
			this.pageFeedCredentials.Controls.Add(this.lblUsername);
			this.pageFeedCredentials.Controls.Add(this.textUser);
			this.pageFeedCredentials.Controls.Add(this.lblPassword);
			this.pageFeedCredentials.Controls.Add(this.textPassword);
			resources.ApplyResources(this.pageFeedCredentials, "pageFeedCredentials");
			this.pageFeedCredentials.Name = "pageFeedCredentials";
			this.pageFeedCredentials.NextPage = this.pageSourceName;
			this.pageFeedCredentials.PreviousPage = this.pageStartImport;
			this.pageFeedCredentials.BeforeDisplay += new System.EventHandler(this.OnPageFeedCredentials_BeforeDisplay);
			// 
			// lblFeedCredentialsIntro
			// 
			resources.ApplyResources(this.lblFeedCredentialsIntro, "lblFeedCredentialsIntro");
			this.lblFeedCredentialsIntro.FlatStyle = System.Windows.Forms.FlatStyle.System;
			this.lblFeedCredentialsIntro.Name = "lblFeedCredentialsIntro";
			// 
			// lblUsername
			// 
			resources.ApplyResources(this.lblUsername, "lblUsername");
			this.lblUsername.FlatStyle = System.Windows.Forms.FlatStyle.System;
			this.lblUsername.Name = "lblUsername";
			// 
			// textUser
			// 
			resources.ApplyResources(this.textUser, "textUser");
			this.textUser.Name = "textUser";
			this.textUser.TextChanged += new System.EventHandler(this.textUser_TextChanged);
			// 
			// lblPassword
			// 
			resources.ApplyResources(this.lblPassword, "lblPassword");
			this.lblPassword.FlatStyle = System.Windows.Forms.FlatStyle.System;
			this.lblPassword.Name = "lblPassword";
			// 
			// textPassword
			// 
			resources.ApplyResources(this.textPassword, "textPassword");
			this.textPassword.Name = "textPassword";
			this.textPassword.TextChanged += new System.EventHandler(this.textPassword_TextChanged);
			// 
			// pageSourceName
			// 
			this.pageSourceName.Controls.Add(this.textFeedSourceName);
			this.pageSourceName.Controls.Add(this.label2);
			resources.ApplyResources(this.pageSourceName, "pageSourceName");
			this.pageSourceName.Name = "pageSourceName";
			this.pageSourceName.PreviousPage = this.pageFeedCredentials;
			this.pageSourceName.BeforeDisplay += new System.EventHandler(this.OnPageSourceName_BeforeDisplay);
			// 
			// textFeedSourceName
			// 
			resources.ApplyResources(this.textFeedSourceName, "textFeedSourceName");
			this.textFeedSourceName.Name = "textFeedSourceName";
			this.textFeedSourceName.TextChanged += new System.EventHandler(this.textFeedSourceName_TextChanged);
			this.textFeedSourceName.Validating += new System.ComponentModel.CancelEventHandler(this.OnControlValidating);
			this.textFeedSourceName.Validated += new System.EventHandler(this.OnControlValidated);
			// 
			// label2
			// 
			this.label2.FlatStyle = System.Windows.Forms.FlatStyle.System;
			resources.ApplyResources(this.label2, "label2");
			this.label2.Name = "label2";
			// 
			// pageStartImport
			// 
			this.pageStartImport.Controls.Add(this.fbrssLinkHelp);
			this.pageStartImport.Controls.Add(this.label1);
			this.pageStartImport.Controls.Add(this.radioFeedlyCloud);
			resources.ApplyResources(this.pageStartImport, "pageStartImport");
			this.pageStartImport.Name = "pageStartImport";
			this.pageStartImport.NextPage = this.pageFeedCredentials;
			this.pageStartImport.BeforeMoveNext += new Divelements.WizardFramework.WizardPageEventHandler(this.OnPageStartImportBeforeMoveNext);
			// 
			// fbrssLinkHelp
			// 
			resources.ApplyResources(this.fbrssLinkHelp, "fbrssLinkHelp");
			this.fbrssLinkHelp.Name = "fbrssLinkHelp";
			this.fbrssLinkHelp.TabStop = true;
			this.fbrssLinkHelp.UseCompatibleTextRendering = true;
			this.fbrssLinkHelp.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.OnFbRssLinkClicked);
			// 
			// label1
			// 
			resources.ApplyResources(this.label1, "label1");
			this.label1.FlatStyle = System.Windows.Forms.FlatStyle.System;
			this.label1.Name = "label1";
			// 
			// radioFeedlyCloud
			// 
			resources.ApplyResources(this.radioFeedlyCloud, "radioFeedlyCloud");
			this.radioFeedlyCloud.Name = "radioFeedlyCloud";
			this.radioFeedlyCloud.UseVisualStyleBackColor = true;
			this.radioFeedlyCloud.CheckedChanged += new System.EventHandler(this.radioFeedlyCloud_CheckedChanged);
			//
			// _btnImmediateFinish
			// 
			resources.ApplyResources(this._btnImmediateFinish, "_btnImmediateFinish");
			this._btnImmediateFinish.Name = "_btnImmediateFinish";
			this._btnImmediateFinish.Click += new System.EventHandler(this.OnImmediateFinish_Click);
			// 
			// wizard
			// 
			this.wizard.BannerImage = ((System.Drawing.Image)(resources.GetObject("wizard.BannerImage")));
			this.wizard.Controls.Add(this.pageStartImport);
			this.wizard.Controls.Add(this.pageSourceName);
			this.wizard.Controls.Add(this.pageFeedCredentials);
			this.wizard.Controls.Add(this._btnImmediateFinish);
			resources.ApplyResources(this.wizard, "wizard");
			this.wizard.MarginImage = ((System.Drawing.Image)(resources.GetObject("wizard.MarginImage")));
			this.wizard.Name = "wizard";
			this.wizard.SelectedPage = this.pageStartImport;
			this.wizard.Cancel += new System.EventHandler(this.OnWizardCancel);
			this.wizard.Finish += new System.EventHandler(this.OnWizardFinish);
			// 
			// errorProvider1
			// 
			this.errorProvider1.ContainerControl = this;
			// 
			// SynchronizeFeedsWizard
			// 
			resources.ApplyResources(this, "$this");
			this.Controls.Add(this.wizard);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SynchronizeFeedsWizard";
			this.pageFeedCredentials.ResumeLayout(false);
			this.pageFeedCredentials.PerformLayout();
			this.pageSourceName.ResumeLayout(false);
			this.pageSourceName.PerformLayout();
			this.pageStartImport.ResumeLayout(false);
			this.pageStartImport.PerformLayout();
			this.wizard.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.errorProvider1)).EndInit();
			this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// Gets the feed credential user.
        /// </summary>
        /// <value>The feed credential user.</value>
        public string FeedCredentialUser
        {
            get { return this.textUser.Text.Trim(); }
        }
        /// <summary>
        /// Gets the feed credential PWD.
        /// </summary>
        /// <value>The feed credential PWD.</value>
        public string FeedCredentialPwd
        {
            get { return this.textPassword.Text.Trim(); }
        }
             
        // -------------------------------------------------------------------
        // Events 
        // -------------------------------------------------------------------



        private void OnWizardCancel(object sender, EventArgs e)
        {
            if (wizard.SelectedPage.AllowMovePrevious)
            {
                this.DialogResult = DialogResult.Cancel;
                Close();
            }
            else
            {
                this.DialogResult = System.Windows.Forms.DialogResult.None;
            }
        }

        private void OnWizardFinish(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void OnImmediateFinish_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void textUser_TextChanged(object sender, EventArgs e)
        {
            this.pageFeedCredentials.AllowMoveNext = false;                
            this.OnControlValidating(sender, new System.ComponentModel.CancelEventArgs()); 
        }

        private void textPassword_TextChanged(object sender, EventArgs e)
        {
            this.pageFeedCredentials.AllowMoveNext = false;                
            this.OnControlValidating(sender, new System.ComponentModel.CancelEventArgs()); 
        }

        private void radioFeedlyCloud_CheckedChanged(object sender, EventArgs e)
        {
            //this.pageStartImport.NextPage = pageFeedCredentials; 
			this.pageStartImport.NextPage = this.pageSourceName; 
        }
		
        private void OnPageFeedCredentials_BeforeMoveNext(object sender, EventArgs e)
        {
            this.OnControlValidating(this.textUser, new System.ComponentModel.CancelEventArgs());
            this.OnControlValidating(this.textPassword, new System.ComponentModel.CancelEventArgs());
        }

        private void OnPageFeedCredentials_BeforeDisplay(object sender, EventArgs e)
        {
            this.pageFeedCredentials.AllowMoveNext = false; 
        }

        private void OnPageSourceName_BeforeDisplay(object sender, EventArgs e)
        {
            this._btnImmediateFinish.Visible = false;
            switch (this.SelectedFeedSource)
            {
                case FeedSourceType.FeedlyCloud:
                    this.textFeedSourceName.Text = SR.FeedNodeMyFeedlyCloudFeedsCaption;
					this.pageSourceName.PreviousPage = this.pageStartImport;
                    break;
            }
        }

        private void OnControlValidated(object sender, EventArgs e)
        {
            wizard.SelectedPage.AllowMoveNext = true; 
        }

        /// <summary>
        /// called on every control
        /// </summary>
        /// <param name="sender">Which control is validated?</param>
        /// <param name="e">EventArgs with cancel parameter</param>
        private void OnControlValidating(object sender, System.ComponentModel.CancelEventArgs e)
        {

            if (ReferenceEquals(wizard.SelectedPage, pageSourceName) && sender == textFeedSourceName)
            {
	            if (textFeedSourceName.Text.Trim().Length == 0)
	            {
		            errorProvider1.SetError(textFeedSourceName, SR.ExceptionNoFeedSourceName);
		            this.pageSourceName.AllowMoveNext = false;
		            this.pageSourceName.AllowCancel = true;
		            e.Cancel = true;
	            }
	            else
	            {
		            ICoreApplication coreApp = IoC.Resolve<ICoreApplication>();
					if (coreApp.FeedSources.Contains(textFeedSourceName.Text.Trim()))
		            {
						errorProvider1.SetError(textFeedSourceName, string.Format(SR.ExceptionDuplicateFeedSourceName, textFeedSourceName.Text.Trim()));

						this.pageSourceName.AllowMoveNext = false;
						this.pageSourceName.AllowCancel = true;
						e.Cancel = true;
		            }
	            }
            }
            else if (ReferenceEquals(wizard.SelectedPage, pageFeedCredentials) && sender == textUser)
            {
                textUser.Text = textUser.Text.Trim();
                if (textUser.Text.Trim().Length == 0)
                {
                    errorProvider1.SetError(textUser, SR.ExceptionNoUserName);
                    e.Cancel = true;
                }
            }
            else if (ReferenceEquals(wizard.SelectedPage, pageFeedCredentials) && sender == textPassword)
            {
                textPassword.Text = textPassword.Text.Trim();
                if (textPassword.Text.Trim().Length == 0)
                {
                    errorProvider1.SetError(textPassword, SR.ExceptionNoPassword);
                    e.Cancel = true;
                }
            }

            if (!e.Cancel)
            {
                errorProvider1.SetError((Control)sender, null);
                
                if (sender == textFeedSourceName)
                {
                    this.pageSourceName.AllowMoveNext = true;
                }

                if (textUser.Text.Length > 0 && textPassword.Text.Length > 0)
                {
                    this.pageFeedCredentials.AllowMoveNext = true;
                }
            }

        }

        private void textFeedSourceName_TextChanged(object sender, EventArgs e)
        {
            this.OnControlValidating(sender, new System.ComponentModel.CancelEventArgs());
        }

		private void OnPageStartImportBeforeMoveNext(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (this.SelectedFeedSource == FeedSourceType.Unknown)
				e.Cancel = true;
		}

		private void OnFbRssLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			var coreApp = IoC.Resolve<ICoreApplication>();
			if (coreApp != null)
			{
				coreApp.NavigateToUrlAsUserPreferred("http://fbrss.com/", "Loading...", true, true);
			}
		}

    }

}
