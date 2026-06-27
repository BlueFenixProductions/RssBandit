using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NewsComponents.Utils
{
	/// <summary>
	/// Windows implementation of <see cref="IFileMover"/>: the engine's historical file-move
	/// primitive, a 10x retry around the <c>kernel32!MoveFileEx</c> P/Invoke.
	///
	/// <para>
	/// The body of <see cref="MoveFile"/> is the former <c>FileHelper.MoveFile</c> implementation moved
	/// here VERBATIM so behavior on Windows is byte-identical (the <see cref="FileHelperMoveFileTests"/>
	/// characterization tests pin it). The P/Invoke is now used only here.
	/// </para>
	/// </summary>
	internal sealed class WindowsFileMover : IFileMover
	{
		private static readonly int msecsBetweenRetries = 100;

		/// <summary>
		/// Move a file from a folder to a new one.
		/// </summary>
		/// <param name="existingFileName">The original file name.</param>
		/// <param name="newFileName">The new file name.</param>
		/// <param name="flags">Flags about how to move the files.</param>
		/// <returns>indicates whether the file was moved.</returns>
		public bool MoveFile( string existingFileName, string newFileName, MoveFileFlag flags) {

			int retries = 10;

			while ( retries > 0 ) {
				try {
					return NativeMethods.MoveFileEx( existingFileName, newFileName, flags );
				}
				catch (Exception) {
					retries--;

					if (retries <= 0)
						throw;	// giving up and report error

					// yield control to other threads so that we get a little
					// wait before we retry.
					Thread.Sleep(msecsBetweenRetries);
					continue;
				}
			}//while

			return false;
		}

		private class NativeMethods
		{
			/// <summary>
			/// API declaration of the Win32 function.
			/// </summary>
			/// <param name="lpExistingFileName">Existing file path.</param>
			/// <param name="lpNewFileName">The file path.</param>
			/// <param name="dwFlags">Move file flags.</param>
			/// <returns>Whether the file was moved or not.</returns>
			[DllImport("KERNEL32.DLL", CharSet = CharSet.Unicode)]
			internal static extern bool MoveFileEx(
				string lpExistingFileName,
				string lpNewFileName,
				MoveFileFlag dwFlags);

		}
	}
}
