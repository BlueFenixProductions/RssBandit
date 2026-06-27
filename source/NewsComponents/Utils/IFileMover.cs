using System;

namespace NewsComponents.Utils
{
	/// <summary>
	/// Abstraction over the single file-move primitive used by the engine
	/// (<see cref="FileHelper.MoveFile(string,string,MoveFileFlag)"/>).
	///
	/// <para>
	/// The engine's only P/Invoke (a 10x retry around <c>kernel32!MoveFileEx</c>) lives behind this
	/// seam so the now-portable engine can run off-Windows: on Windows the call is served by the
	/// verbatim <see cref="WindowsFileMover"/> (behavior is byte-identical to the historical
	/// implementation), while non-Windows hosts use the managed <see cref="PortableFileMover"/>
	/// (which matches the same observable contract via <c>System.IO.File.Move</c>).
	/// </para>
	/// </summary>
	internal interface IFileMover
	{
		/// <summary>
		/// Move a file from a folder to a new one.
		/// </summary>
		/// <param name="existingFileName">The original file name.</param>
		/// <param name="newFileName">The new file name.</param>
		/// <param name="flags">Flags about how to move the files.</param>
		/// <returns>indicates whether the file was moved.</returns>
		bool MoveFile(string existingFileName, string newFileName, MoveFileFlag flags);
	}
}
