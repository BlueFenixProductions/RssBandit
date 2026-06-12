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

using RssBandit.Core.Storage;
using RssBandit.WinGui.Dialogs;

using UserIdentity = RssBandit.Core.Storage.Serialization.UserIdentity;

namespace RssBandit
{

	#region IdentityNewsServerManager
	/// <summary>
	/// Manages the user identities used e.g. for comment replies.
	/// </summary>
	internal class IdentityNewsServerManager
	{
		public event EventHandler IdentityDefinitionsModified;

		// logging/tracing:
		private static readonly log4net.ILog _log = Common.Logging.Log.GetLogger(typeof(IdentityNewsServerManager));

		private static UserIdentity anonymous;

		private IdentitiesDictionary identities;
		private readonly RssBanditApplication app;

	    internal IdentityNewsServerManager(RssBanditApplication app)
		{
			this.app = app;
		}

		#region public methods

		public static UserIdentity AnonymousIdentity
		{
			get {
				if (anonymous == null)
				{
					anonymous = new UserIdentity();
					anonymous.Name = anonymous.RealName = "anonymous";
					anonymous.MailAddress = anonymous.ResponseAddress = String.Empty;
					anonymous.Organization = anonymous.ReferrerUrl = String.Empty;
					anonymous.Signature = String.Empty;
				}
				return anonymous;
			}
		}

		public IdentitiesDictionary Identities
		{
			get
			{
				if (identities == null)
				{
					identities = LoadIdentities(IoC.Resolve<IUserRoamingDataService>());
				}
				return identities;
			}
			set
			{
				identities = value;
			}
		}

		/// <summary>
		/// Saves the modified objects of this instance.
		/// </summary>
		public void Save()
		{
			if (Identities.Modified)
				SaveIdentities(IoC.Resolve<IUserRoamingDataService>(), Identities);
		}

		/// <summary>
		/// Resets the identities. They are re-loaded from storage on next request
		/// </summary>
		public void Reset()
		{
			identities = null;
		}

		public void MigrateOrMergeIdentities(List<NewsComponents.Feed.UserIdentity> oldVersionIdentities, bool replace)
		{
			if (oldVersionIdentities != null && oldVersionIdentities.Count > 0)
			{
				IdentitiesDictionary migrated = new IdentitiesDictionary(oldVersionIdentities.Count);
				foreach (NewsComponents.Feed.UserIdentity oldIdent in oldVersionIdentities)
				{
					UserIdentity newIdent = new UserIdentity();
					newIdent.Name = oldIdent.Name;
					newIdent.MailAddress = oldIdent.MailAddress;
					newIdent.Organization = oldIdent.Organization;
					newIdent.RealName = oldIdent.RealName;
					newIdent.ReferrerUrl = oldIdent.ReferrerUrl;
					newIdent.ResponseAddress = oldIdent.ResponseAddress;
					newIdent.Signature = oldIdent.Signature;
					migrated.Add(newIdent.Name, newIdent);
				}

				if (replace)
				{
					Identities = migrated;
				}
				else
				{
					foreach (UserIdentity identity in migrated.Values)
					{
						if (Identities.ContainsKey(identity.Name))
						{
							Identities[identity.Name] = identity;
						} else
						{
							Identities.Add(identity.Name, identity);
						}
					}
				}

				Save();
			}
		}

		#endregion

		#region private methods

		static IdentitiesDictionary LoadIdentities(IClientDataService dataService)
		{
			if (dataService == null)
				throw new ArgumentNullException("dataService");
			try
			{
				return dataService.LoadIdentities();
			}
			catch (Exception ex)
			{
				_log.Error("Could not load user identities", ex);
				return new IdentitiesDictionary();
			}
		}

		private static void SaveIdentities(IClientDataService dataService, IdentitiesDictionary identitiesDictionary)
		{
			if (dataService == null)
				throw new ArgumentNullException("dataService");

			if (!identitiesDictionary.Modified)
				return;

			try
			{
				dataService.SaveIdentities(identitiesDictionary);
				identitiesDictionary.Modified = false;
			}
			catch (Exception ex)
			{
				_log.Error("Could not save user identities", ex);
			}
		}

		#endregion

		#region ShowDialog()'s
		public void ShowIdentityDialog(IWin32Window owner) {
			using (IdentitiesDialog dialog = new IdentitiesDialog(Identities.Values))
			{
				if (dialog.ShowDialog(owner) != DialogResult.OK)
					return;

				IdentitiesDictionary edited = new IdentitiesDictionary(dialog.Identities.Count);
				foreach (UserIdentity identity in dialog.Identities)
					edited.Add(identity.Name, identity);
				edited.Modified = true;

				Identities = edited;
				Save();
				RaiseIdentityDefinitionsModified();
			}
		}
		#endregion

		#region private members
		void RaiseIdentityDefinitionsModified() {
			if (IdentityDefinitionsModified != null)
				IdentityDefinitionsModified(this, EventArgs.Empty);
		}
		#endregion

	}
	#endregion

	/// <summary>
	/// A dictionary of user identities
	/// </summary>
	internal class IdentitiesDictionary : Core.Storage.Serialization.StatefullKeyItemCollection<string, UserIdentity>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="IdentitiesDictionary"/> class.
		/// </summary>
		public IdentitiesDictionary()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="IdentitiesDictionary"/> class.
		/// </summary>
		/// <param name="capacity">The capacity.</param>
		public IdentitiesDictionary(int capacity): base(capacity)
		{

		}
	}
}
