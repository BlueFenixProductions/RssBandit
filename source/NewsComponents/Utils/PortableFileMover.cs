using System.IO;

namespace NewsComponents.Utils
{
	/// <summary>
	/// Portable (non-Windows) implementation of <see cref="IFileMover"/> built on managed
	/// <see cref="System.IO.File"/> operations, so the engine no longer depends on the
	/// <c>kernel32!MoveFileEx</c> P/Invoke when running off-Windows.
	///
	/// <para>
	/// It reproduces the observable contract of <see cref="WindowsFileMover"/>:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="MoveFileFlag.ReplaceExisting"/> set: atomically replace the
	/// destination (returns <c>true</c>).</description></item>
	/// <item><description>No <see cref="MoveFileFlag.ReplaceExisting"/> and the destination exists:
	/// return <c>false</c> WITHOUT throwing (matches the Windows quirk where <c>MoveFileEx</c> fails
	/// rather than overwriting).</description></item>
	/// <item><description>Destination absent: plain move (returns <c>true</c>).</description></item>
	/// <item><description><see cref="MoveFileFlag.DelayUntilReboot"/> (<c>newFileName == null</c>):
	/// there is no managed equivalent of the Windows reboot-time deletion, so make a best-effort
	/// delete of the source and return <c>true</c>, never throwing.</description></item>
	/// </list>
	/// </summary>
	internal sealed class PortableFileMover : IFileMover
	{
		/// <summary>
		/// Move a file from a folder to a new one.
		/// </summary>
		/// <param name="existingFileName">The original file name.</param>
		/// <param name="newFileName">The new file name (<c>null</c> for the DelayUntilReboot path).</param>
		/// <param name="flags">Flags about how to move the files.</param>
		/// <returns>indicates whether the file was moved.</returns>
		public bool MoveFile(string existingFileName, string newFileName, MoveFileFlag flags)
		{
			// DelayUntilReboot (newFileName == null): no managed equivalent of the Windows
			// reboot-time deletion exists. The normal delete already failed (this is the
			// DestroyFile/DestroyFolder fallback), so make a best-effort delete now and report
			// success without ever throwing.
			if (newFileName == null)
			{
				try
				{
					if (Directory.Exists(existingFileName))
						Directory.Delete(existingFileName, true);
					else if (File.Exists(existingFileName))
						File.Delete(existingFileName);
				}
				catch
				{
					// best-effort: give up gracefully, never throw.
				}
				return true;
			}

			if (flags.HasFlag(MoveFileFlag.ReplaceExisting))
			{
				// net10.0 File.Move(src, dst, overwrite) does an atomic-ish replace.
				File.Move(existingFileName, newFileName, overwrite: true);
				return true;
			}

			// No ReplaceExisting: match the Windows quirk where MoveFileEx fails (returns false)
			// rather than overwriting an existing destination. A plain File.Move would throw an
			// IOException here, so guard with File.Exists and report failure without throwing.
			if (File.Exists(newFileName))
				return false;

			File.Move(existingFileName, newFileName);
			return true;
		}
	}
}
