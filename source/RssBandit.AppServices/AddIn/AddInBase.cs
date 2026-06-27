using System;

namespace RssBandit.UIServices
{
	/// <summary>
	/// PluginBase.
	/// To have a chance to apply some code restrictions/security.
	/// </summary>
	public abstract class AddInBase: MarshalByRefObject, IDisposable
	{
		private bool _disposed = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddInBase"/> class.
		/// </summary>
		public AddInBase(){}

		// NOTE: the former InitializeLifetimeService() override (.NET Remoting lease control) was
		// removed: Remoting is not supported on modern .NET, the base member is obsolete, and nothing
		// activates AddInBase across a remoting boundary.

		#region IDisposable Members

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or
		/// resetting unmanaged resources.
		/// </summary>
		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion
		
		/// <summary>
		/// Disposes the specified disposing.
		/// </summary>
		/// <param name="disposing">if set to <c>true</c> [disposing].</param>
		protected virtual void Dispose(bool disposing) {

			if (!this._disposed) {

				// if this is a dispose call dispose on all state you 
				// hold, and take yourself off the Finalization queue.

				if (disposing) {

					// free managed resources:

				}

				// free your own state (unmanaged objects) here:

				// flag:
				this._disposed = true;

			}

		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="RssBandit.UIServices.AddInBase"/> is reclaimed by garbage collection.
		/// </summary>
		/// <remarks>finalizer simply calls Dispose(false)</remarks>
		~AddInBase() {
			Dispose(false);
		}

	}
}
