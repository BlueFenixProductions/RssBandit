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
using System.Collections.Generic;
using System.Windows.Forms;

using RssBandit.Resources;

using UserIdentity = RssBandit.Core.Storage.Serialization.UserIdentity;

namespace RssBandit.WinGui.Dialogs
{
	/// <summary>
	/// Standalone editor dialog for the user identities used e.g. for comment replies.
	/// Replaces the identity editor that was part of the removed NNTP newsgroups
	/// configuration dialog.
	/// </summary>
	internal sealed class IdentitiesDialog : Form
	{
		private readonly List<UserIdentity> identities = new List<UserIdentity>();
		private UserIdentity current;
		private bool suppressEvents;

		private ListBox lstIdentities;
		private Button btnNew;
		private Button btnRemove;
		private Label lblName;
		private TextBox txtName;
		private Label lblRealName;
		private TextBox txtRealName;
		private Label lblOrganization;
		private TextBox txtOrganization;
		private Label lblMailAddress;
		private TextBox txtMailAddress;
		private Label lblResponseAddress;
		private TextBox txtResponseAddress;
		private Label lblReferrerUrl;
		private TextBox txtReferrerUrl;
		private Label lblSignature;
		private TextBox txtSignature;
		private Button btnOk;
		private Button btnCancel;

		public IdentitiesDialog(IEnumerable<UserIdentity> currentIdentities)
		{
			InitializeComponent();
			this.Text = SR.ConfigIdentitiesDialogCaption;

			if (currentIdentities != null)
			{
				foreach (UserIdentity identity in currentIdentities)
				{
					// work on clones, so Cancel leaves the originals untouched:
					this.identities.Add((UserIdentity)identity.Clone());
				}
			}

			foreach (UserIdentity identity in this.identities)
				this.lstIdentities.Items.Add(identity);

			if (this.lstIdentities.Items.Count > 0)
				this.lstIdentities.SelectedIndex = 0;
			else
				RefreshDetailFields();
		}

		/// <summary>
		/// The edited identities (valid after the dialog returned DialogResult.OK).
		/// </summary>
		public List<UserIdentity> Identities
		{
			get { return this.identities; }
		}

		private void OnSelectedIdentityChanged(object sender, EventArgs e)
		{
			if (this.suppressEvents)
				return;
			this.current = this.lstIdentities.SelectedItem as UserIdentity;
			RefreshDetailFields();
		}

		private void RefreshDetailFields()
		{
			this.suppressEvents = true;
			try
			{
				bool haveSelection = (this.current != null);
				this.txtName.Text = haveSelection ? this.current.Name : String.Empty;
				this.txtRealName.Text = haveSelection ? this.current.RealName : String.Empty;
				this.txtOrganization.Text = haveSelection ? this.current.Organization : String.Empty;
				this.txtMailAddress.Text = haveSelection ? this.current.MailAddress : String.Empty;
				this.txtResponseAddress.Text = haveSelection ? this.current.ResponseAddress : String.Empty;
				this.txtReferrerUrl.Text = haveSelection ? this.current.ReferrerUrl : String.Empty;
				this.txtSignature.Text = haveSelection ? this.current.Signature : String.Empty;

				this.txtName.Enabled = this.txtRealName.Enabled = this.txtOrganization.Enabled =
					this.txtMailAddress.Enabled = this.txtResponseAddress.Enabled =
					this.txtReferrerUrl.Enabled = this.txtSignature.Enabled =
					this.btnRemove.Enabled = haveSelection;
			}
			finally
			{
				this.suppressEvents = false;
			}
		}

		private void OnNameTextChanged(object sender, EventArgs e)
		{
			if (this.suppressEvents || this.current == null)
				return;

			this.current.Name = this.txtName.Text.Trim();

			// refresh the displayed name in the list without re-entering selection handling:
			int index = this.lstIdentities.SelectedIndex;
			if (index >= 0)
			{
				this.suppressEvents = true;
				try
				{
					this.lstIdentities.Items[index] = this.current;
					this.lstIdentities.SelectedIndex = index;
				}
				finally
				{
					this.suppressEvents = false;
				}
			}
		}

		private void OnDetailTextChanged(object sender, EventArgs e)
		{
			if (this.suppressEvents || this.current == null)
				return;

			this.current.RealName = this.txtRealName.Text;
			this.current.Organization = this.txtOrganization.Text;
			this.current.MailAddress = this.txtMailAddress.Text;
			this.current.ResponseAddress = this.txtResponseAddress.Text;
			this.current.ReferrerUrl = this.txtReferrerUrl.Text;
			this.current.Signature = this.txtSignature.Text;
		}

		private void OnNewIdentityClick(object sender, EventArgs e)
		{
			UserIdentity identity = new UserIdentity();
			identity.Name = NextNewIdentityName();
			identity.RealName = identity.Organization = String.Empty;
			identity.MailAddress = identity.ResponseAddress = String.Empty;
			identity.ReferrerUrl = identity.Signature = String.Empty;

			this.identities.Add(identity);
			this.lstIdentities.Items.Add(identity);
			this.lstIdentities.SelectedItem = identity;

			this.txtName.Focus();
			this.txtName.SelectAll();
		}

		private void OnRemoveIdentityClick(object sender, EventArgs e)
		{
			int index = this.lstIdentities.SelectedIndex;
			if (index < 0)
				return;

			UserIdentity identity = (UserIdentity)this.lstIdentities.Items[index];
			this.identities.Remove(identity);
			this.lstIdentities.Items.RemoveAt(index);

			if (this.lstIdentities.Items.Count > 0)
				this.lstIdentities.SelectedIndex = Math.Min(index, this.lstIdentities.Items.Count - 1);
			else
			{
				this.current = null;
				RefreshDetailFields();
			}
		}

		private void OnOkClick(object sender, EventArgs e)
		{
			Dictionary<string, UserIdentity> seen =
				new Dictionary<string, UserIdentity>(StringComparer.OrdinalIgnoreCase);

			foreach (UserIdentity identity in this.identities)
			{
				if (String.IsNullOrEmpty(identity.Name))
				{
					ReportInvalidIdentity(identity, "Please enter a name for each identity.");
					return;
				}
				if (seen.ContainsKey(identity.Name))
				{
					ReportInvalidIdentity(identity,
						String.Format("There is more than one identity named '{0}'. Identity names must be unique.", identity.Name));
					return;
				}
				seen.Add(identity.Name, identity);
			}

			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		private void ReportInvalidIdentity(UserIdentity identity, string message)
		{
			MessageBox.Show(this, message, this.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
			this.lstIdentities.SelectedItem = identity;
			this.txtName.Focus();
			this.txtName.SelectAll();
		}

		private string NextNewIdentityName()
		{
			const string baseName = "New identity";
			string name = baseName;
			int counter = 2;
			while (NameInUse(name))
			{
				name = String.Format("{0} ({1})", baseName, counter);
				counter++;
			}
			return name;
		}

		private bool NameInUse(string name)
		{
			foreach (UserIdentity identity in this.identities)
			{
				if (String.Equals(identity.Name, name, StringComparison.OrdinalIgnoreCase))
					return true;
			}
			return false;
		}

		private void InitializeComponent()
		{
			this.lstIdentities = new ListBox();
			this.btnNew = new Button();
			this.btnRemove = new Button();
			this.lblName = new Label();
			this.txtName = new TextBox();
			this.lblRealName = new Label();
			this.txtRealName = new TextBox();
			this.lblOrganization = new Label();
			this.txtOrganization = new TextBox();
			this.lblMailAddress = new Label();
			this.txtMailAddress = new TextBox();
			this.lblResponseAddress = new Label();
			this.txtResponseAddress = new TextBox();
			this.lblReferrerUrl = new Label();
			this.txtReferrerUrl = new TextBox();
			this.lblSignature = new Label();
			this.txtSignature = new TextBox();
			this.btnOk = new Button();
			this.btnCancel = new Button();
			this.SuspendLayout();
			//
			// lstIdentities
			//
			this.lstIdentities.IntegralHeight = false;
			this.lstIdentities.Location = new System.Drawing.Point(12, 12);
			this.lstIdentities.Name = "lstIdentities";
			this.lstIdentities.Size = new System.Drawing.Size(170, 305);
			this.lstIdentities.TabIndex = 0;
			this.lstIdentities.SelectedIndexChanged += new EventHandler(this.OnSelectedIdentityChanged);
			//
			// btnNew
			//
			this.btnNew.FlatStyle = FlatStyle.System;
			this.btnNew.Location = new System.Drawing.Point(12, 325);
			this.btnNew.Name = "btnNew";
			this.btnNew.Size = new System.Drawing.Size(82, 25);
			this.btnNew.TabIndex = 1;
			this.btnNew.Text = "&New";
			this.btnNew.Click += new EventHandler(this.OnNewIdentityClick);
			//
			// btnRemove
			//
			this.btnRemove.FlatStyle = FlatStyle.System;
			this.btnRemove.Location = new System.Drawing.Point(100, 325);
			this.btnRemove.Name = "btnRemove";
			this.btnRemove.Size = new System.Drawing.Size(82, 25);
			this.btnRemove.TabIndex = 2;
			this.btnRemove.Text = "&Remove";
			this.btnRemove.Click += new EventHandler(this.OnRemoveIdentityClick);
			//
			// lblName
			//
			this.lblName.AutoSize = true;
			this.lblName.Location = new System.Drawing.Point(196, 15);
			this.lblName.Name = "lblName";
			this.lblName.Text = "&Identity name:";
			//
			// txtName
			//
			this.txtName.Location = new System.Drawing.Point(310, 12);
			this.txtName.Name = "txtName";
			this.txtName.Size = new System.Drawing.Size(250, 21);
			this.txtName.TabIndex = 3;
			this.txtName.TextChanged += new EventHandler(this.OnNameTextChanged);
			//
			// lblRealName
			//
			this.lblRealName.AutoSize = true;
			this.lblRealName.Location = new System.Drawing.Point(196, 43);
			this.lblRealName.Name = "lblRealName";
			this.lblRealName.Text = "Real n&ame:";
			//
			// txtRealName
			//
			this.txtRealName.Location = new System.Drawing.Point(310, 40);
			this.txtRealName.Name = "txtRealName";
			this.txtRealName.Size = new System.Drawing.Size(250, 21);
			this.txtRealName.TabIndex = 4;
			this.txtRealName.TextChanged += new EventHandler(this.OnDetailTextChanged);
			//
			// lblOrganization
			//
			this.lblOrganization.AutoSize = true;
			this.lblOrganization.Location = new System.Drawing.Point(196, 71);
			this.lblOrganization.Name = "lblOrganization";
			this.lblOrganization.Text = "&Organization:";
			//
			// txtOrganization
			//
			this.txtOrganization.Location = new System.Drawing.Point(310, 68);
			this.txtOrganization.Name = "txtOrganization";
			this.txtOrganization.Size = new System.Drawing.Size(250, 21);
			this.txtOrganization.TabIndex = 5;
			this.txtOrganization.TextChanged += new EventHandler(this.OnDetailTextChanged);
			//
			// lblMailAddress
			//
			this.lblMailAddress.AutoSize = true;
			this.lblMailAddress.Location = new System.Drawing.Point(196, 99);
			this.lblMailAddress.Name = "lblMailAddress";
			this.lblMailAddress.Text = "&E-mail address:";
			//
			// txtMailAddress
			//
			this.txtMailAddress.Location = new System.Drawing.Point(310, 96);
			this.txtMailAddress.Name = "txtMailAddress";
			this.txtMailAddress.Size = new System.Drawing.Size(250, 21);
			this.txtMailAddress.TabIndex = 6;
			this.txtMailAddress.TextChanged += new EventHandler(this.OnDetailTextChanged);
			//
			// lblResponseAddress
			//
			this.lblResponseAddress.AutoSize = true;
			this.lblResponseAddress.Location = new System.Drawing.Point(196, 127);
			this.lblResponseAddress.Name = "lblResponseAddress";
			this.lblResponseAddress.Text = "Re&ply-to address:";
			//
			// txtResponseAddress
			//
			this.txtResponseAddress.Location = new System.Drawing.Point(310, 124);
			this.txtResponseAddress.Name = "txtResponseAddress";
			this.txtResponseAddress.Size = new System.Drawing.Size(250, 21);
			this.txtResponseAddress.TabIndex = 7;
			this.txtResponseAddress.TextChanged += new EventHandler(this.OnDetailTextChanged);
			//
			// lblReferrerUrl
			//
			this.lblReferrerUrl.AutoSize = true;
			this.lblReferrerUrl.Location = new System.Drawing.Point(196, 155);
			this.lblReferrerUrl.Name = "lblReferrerUrl";
			this.lblReferrerUrl.Text = "&Web address (URL):";
			//
			// txtReferrerUrl
			//
			this.txtReferrerUrl.Location = new System.Drawing.Point(310, 152);
			this.txtReferrerUrl.Name = "txtReferrerUrl";
			this.txtReferrerUrl.Size = new System.Drawing.Size(250, 21);
			this.txtReferrerUrl.TabIndex = 8;
			this.txtReferrerUrl.TextChanged += new EventHandler(this.OnDetailTextChanged);
			//
			// lblSignature
			//
			this.lblSignature.AutoSize = true;
			this.lblSignature.Location = new System.Drawing.Point(196, 183);
			this.lblSignature.Name = "lblSignature";
			this.lblSignature.Text = "&Signature:";
			//
			// txtSignature
			//
			this.txtSignature.AcceptsReturn = true;
			this.txtSignature.Location = new System.Drawing.Point(196, 202);
			this.txtSignature.Multiline = true;
			this.txtSignature.Name = "txtSignature";
			this.txtSignature.ScrollBars = ScrollBars.Vertical;
			this.txtSignature.Size = new System.Drawing.Size(364, 115);
			this.txtSignature.TabIndex = 9;
			this.txtSignature.TextChanged += new EventHandler(this.OnDetailTextChanged);
			//
			// btnOk
			//
			this.btnOk.FlatStyle = FlatStyle.System;
			this.btnOk.Location = new System.Drawing.Point(380, 325);
			this.btnOk.Name = "btnOk";
			this.btnOk.Size = new System.Drawing.Size(85, 25);
			this.btnOk.TabIndex = 10;
			this.btnOk.Text = "OK";
			this.btnOk.Click += new EventHandler(this.OnOkClick);
			//
			// btnCancel
			//
			this.btnCancel.CausesValidation = false;
			this.btnCancel.DialogResult = DialogResult.Cancel;
			this.btnCancel.FlatStyle = FlatStyle.System;
			this.btnCancel.Location = new System.Drawing.Point(475, 325);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(85, 25);
			this.btnCancel.TabIndex = 11;
			this.btnCancel.Text = "Cancel";
			//
			// IdentitiesDialog
			//
			this.AcceptButton = this.btnOk;
			this.AutoScaleMode = AutoScaleMode.Font;
			this.CancelButton = this.btnCancel;
			this.ClientSize = new System.Drawing.Size(572, 362);
			this.Controls.Add(this.lstIdentities);
			this.Controls.Add(this.btnNew);
			this.Controls.Add(this.btnRemove);
			this.Controls.Add(this.lblName);
			this.Controls.Add(this.txtName);
			this.Controls.Add(this.lblRealName);
			this.Controls.Add(this.txtRealName);
			this.Controls.Add(this.lblOrganization);
			this.Controls.Add(this.txtOrganization);
			this.Controls.Add(this.lblMailAddress);
			this.Controls.Add(this.txtMailAddress);
			this.Controls.Add(this.lblResponseAddress);
			this.Controls.Add(this.txtResponseAddress);
			this.Controls.Add(this.lblReferrerUrl);
			this.Controls.Add(this.txtReferrerUrl);
			this.Controls.Add(this.lblSignature);
			this.Controls.Add(this.txtSignature);
			this.Controls.Add(this.btnOk);
			this.Controls.Add(this.btnCancel);
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "IdentitiesDialog";
			this.ShowInTaskbar = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.ResumeLayout(false);
			this.PerformLayout();
		}
	}
}
