using System;
using System.IO;
using System.Text;

using NewsComponents.Utils;

namespace RssBandit
{
	/// <summary>
	/// Managed replacement for the retired Enterprise Library 1.0
	/// "Exception Management Application Block".
	/// <para>
	/// Publishing an exception reproduces the two behaviors that used to be
	/// wired up through the &lt;exceptionManagement&gt; section in app.config:
	/// </para>
	/// <list type="number">
	/// <item>a detailed entry (general environment info + descriptive exception
	/// text) appended to <c>%APPDATA%\RssBandit\error.log</c> (formerly
	/// <c>BanditExceptionPublisher</c>); and</item>
	/// <item>a log4net error (formerly <c>ExceptionLog4NetPublisher</c>).</item>
	/// </list>
	/// </summary>
	public static class ExceptionPublisher
	{
		private static readonly string LogFileName =
			Environment.ExpandEnvironmentVariables(@"%APPDATA%\RssBandit\error.log");

		/// <summary>
		/// Publishes the specified exception: writes a detailed entry to
		/// <c>%APPDATA%\RssBandit\error.log</c> and logs it through log4net.
		/// </summary>
		/// <param name="exception">The exception to publish.</param>
		public static void Publish(Exception exception)
		{
			// Publishing happens from inside callers' catch blocks; like the old
			// Enterprise Library ExceptionManager.Publish, a publisher failure must
			// never propagate back to the caller. Guard the argument and run the two
			// effects independently so one failing cannot skip the other and neither
			// can throw back out of this method.
			if (exception == null)
				return;

			try
			{
				WriteToErrorLog(exception);
			}
			catch (Exception writeFailure)
			{
				try
				{
					Common.Logging.Log.Error("ExceptionPublisher: failed to write to error.log", writeFailure);
				}
				catch
				{
					// Nothing more we can do; swallow so publishing never throws.
				}
			}

			try
			{
				Common.Logging.Log.Error(exception.ToDescriptiveString(), exception);
			}
			catch
			{
				// A log4net failure must not propagate back into the caller's catch block.
			}
		}

		/// <summary>
		/// Appends a "General Information" + exception block to the error.log file.
		/// </summary>
		private static void WriteToErrorLog(Exception exception)
		{
			// Create StringBuilder to maintain publishing information.
			StringBuilder strInfo = new StringBuilder();

			// Record General information.
			int bits = (Win32.Is32Bit ? 32 : (Win32.Is64Bit ? 64 : 0));
			strInfo.AppendFormat("{0}General Information{0}", Environment.NewLine);
			strInfo.AppendFormat("{0}{1} ({2}-bit)", Environment.NewLine, RssBanditApplication.Caption, bits);
			strInfo.AppendFormat("{0}OS Version: {1}", Environment.NewLine, Win32.OSVersionDisplayString);
			strInfo.AppendFormat("{0}OS-Culture: {1}", Environment.NewLine, System.Globalization.CultureInfo.InstalledUICulture.Name);
			strInfo.AppendFormat("{0}Framework Version: .NET CLR {1}", Environment.NewLine, System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion());
			strInfo.AppendFormat("{0}Thread-Culture: {1}", Environment.NewLine, System.Threading.Thread.CurrentThread.CurrentCulture.Name);
			strInfo.AppendFormat("{0}UI-Culture: {1}", Environment.NewLine, System.Threading.Thread.CurrentThread.CurrentUICulture.Name);
			strInfo.AppendFormat("{0}IE-Version: {1}", Environment.NewLine, Win32.IEVersion);
			strInfo.AppendFormat("{0}Date/Time: {1}", Environment.NewLine, DateTime.Now.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
			strInfo.AppendFormat("{0}Machine Name: {1}", Environment.NewLine, Environment.MachineName);

			// Append the exception text.
			if (exception != null)
			{
				strInfo.AppendFormat("{0}{0}Exception Information{0}{1}", Environment.NewLine, exception.ToDescriptiveString());
			}
			else
			{
				strInfo.AppendFormat("{0}{0}No Exception.{0}", Environment.NewLine);
			}

			// Write the entry to the log file.
			using (FileStream fs = FileHelper.OpenForWriteAppend(LogFileName))
			{
				StreamWriter sw = new StreamWriter(fs);
				sw.WriteLine(strInfo.ToString());
				sw.WriteLine("================= End Entry =================");
				sw.Flush();
			}
		}
	}
}
