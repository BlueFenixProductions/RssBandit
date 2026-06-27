using System;
using System.IO;
using NewsComponents.Utils;
using NUnit.Framework;

namespace NewsComponents.UnitTests.Utils
{
	/// <summary>
	/// Characterization tests pinning the CURRENT behavior of
	/// <see cref="FileHelper.MoveFile(string,string,MoveFileFlag)"/> on Windows.
	///
	/// <para>
	/// <c>FileHelper.MoveFile</c> is the engine's only P/Invoke: a 10x retry around
	/// <c>kernel32!MoveFileEx</c>. These tests lock down the testable, real-world usage
	/// of the method - the <see cref="MoveFileFlag.ReplaceExisting"/> atomic move/replace,
	/// which is how all 6 live callers use it (FileStorageDataService cache writes and
	/// LuceneIndexModifier index commits). They exist so the upcoming IFileMover extraction
	/// (Task 2) can be proven behavior-preserving on Windows.
	/// </para>
	///
	/// <para>
	/// The <see cref="MoveFileFlag.DelayUntilReboot"/> path (<c>newFileName == null</c>) is
	/// intentionally NOT characterized: it asks the OS to schedule a real file deletion at the
	/// next reboot (a genuine, unwanted machine-level side effect) and is preserved verbatim by
	/// Task 2 rather than pinned here.
	/// </para>
	/// </summary>
	[TestFixture]
	public class FileHelperMoveFileTests
	{
		private const string SourceContent = "SOURCE-CONTENT-0123456789";
		private const string DestinationContent = "DEST-CONTENT-abcdefghij";

		private string _tempDir;

		[SetUp]
		public void SetUp()
		{
			// Isolated, per-test temp directory under the OS temp path.
			_tempDir = Path.Combine(Path.GetTempPath(), "RssBandit_FileHelperMoveFileTests_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempDir);
		}

		[TearDown]
		public void TearDown()
		{
			try
			{
				if (_tempDir != null && Directory.Exists(_tempDir))
					Directory.Delete(_tempDir, true);
			}
			catch
			{
				// Best-effort cleanup; never fail a test on teardown.
			}
		}

		private string PathFor(string name)
		{
			return Path.Combine(_tempDir, name);
		}

		private string WriteFile(string name, string content)
		{
			string path = PathFor(name);
			File.WriteAllText(path, content);
			return path;
		}

		/// <summary>
		/// Behavior 1: ReplaceExisting + destination EXISTS -> atomic replace.
		/// Returns true; destination now holds the source's content; source is gone.
		/// </summary>
		[Test]
		public void MoveFile_ReplaceExisting_WhenDestinationExists_AtomicallyReplacesDestination()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = WriteFile("dest.dat", DestinationContent);

			bool moved = FileHelper.MoveFile(src, dst, MoveFileFlag.ReplaceExisting);

			Assert.That(moved, Is.True, "MoveFile with ReplaceExisting should report success.");
			Assert.That(File.Exists(dst), Is.True, "Destination should still exist after the replace.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(SourceContent), "Destination should now hold the source content.");
			Assert.That(File.Exists(src), Is.False, "Source should no longer exist after the move.");
		}

		/// <summary>
		/// Behavior 2: ReplaceExisting + destination ABSENT -> plain move.
		/// Returns true; destination is created with the source content; source is gone.
		/// </summary>
		[Test]
		public void MoveFile_ReplaceExisting_WhenDestinationAbsent_MovesFile()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = PathFor("new-dest.dat");
			Assume.That(File.Exists(dst), Is.False, "Precondition: destination must not exist.");

			bool moved = FileHelper.MoveFile(src, dst, MoveFileFlag.ReplaceExisting);

			Assert.That(moved, Is.True, "MoveFile with ReplaceExisting to an absent destination should succeed.");
			Assert.That(File.Exists(dst), Is.True, "Destination should have been created by the move.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(SourceContent), "Destination should hold the source content.");
			Assert.That(File.Exists(src), Is.False, "Source should no longer exist after the move.");
		}

		/// <summary>
		/// Behavior 3: NO ReplaceExisting + destination EXISTS -> fails, leaving both files intact.
		///
		/// <para>
		/// OBSERVED current behavior: <c>MoveFileEx</c> without REPLACE_EXISTING fails when the
		/// target already exists. The native call returns FALSE (it does NOT raise a managed
		/// exception, so the 10x retry/Thread.Sleep loop is never entered), and <c>MoveFile</c>
		/// returns FALSE on its first iteration. The source is left in place and the destination
		/// keeps its original content - nothing is moved or overwritten.
		/// </para>
		/// </summary>
		[Test]
		public void MoveFile_WithoutReplaceExisting_WhenDestinationExists_FailsAndLeavesFilesIntact()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = WriteFile("dest.dat", DestinationContent);

			bool moved = FileHelper.MoveFile(src, dst, MoveFileFlag.None);

			Assert.That(moved, Is.False, "Move without ReplaceExisting must fail when the destination exists.");
			Assert.That(File.Exists(src), Is.True, "Source must remain in place when the move fails.");
			Assert.That(File.ReadAllText(src), Is.EqualTo(SourceContent), "Source content must be untouched.");
			Assert.That(File.Exists(dst), Is.True, "Destination must still exist.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(DestinationContent), "Destination content must be untouched (not overwritten).");
		}

		/// <summary>
		/// Behavior 4 (companion to behavior 3): NO ReplaceExisting + destination ABSENT -> plain move.
		/// A default move to a free destination succeeds even without the ReplaceExisting flag.
		/// </summary>
		[Test]
		public void MoveFile_WithoutReplaceExisting_WhenDestinationAbsent_MovesFile()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = PathFor("new-dest.dat");
			Assume.That(File.Exists(dst), Is.False, "Precondition: destination must not exist.");

			bool moved = FileHelper.MoveFile(src, dst, MoveFileFlag.None);

			Assert.That(moved, Is.True, "Default move to an absent destination should succeed.");
			Assert.That(File.Exists(dst), Is.True, "Destination should have been created by the move.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(SourceContent), "Destination should hold the source content.");
			Assert.That(File.Exists(src), Is.False, "Source should no longer exist after the move.");
		}
	}
}
