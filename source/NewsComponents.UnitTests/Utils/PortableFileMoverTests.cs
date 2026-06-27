using System;
using System.IO;
using NewsComponents.Utils;
using NUnit.Framework;

namespace NewsComponents.UnitTests.Utils
{
	/// <summary>
	/// Tests for <see cref="PortableFileMover"/>, the managed (non-Windows) <see cref="IFileMover"/>
	/// used by the portable engine off-Windows.
	///
	/// <para>
	/// They pin that <c>PortableFileMover</c> reproduces the same observable contract as the verbatim
	/// <c>WindowsFileMover</c> (which <see cref="FileHelperMoveFileTests"/> pins on Windows): the
	/// <see cref="MoveFileFlag.ReplaceExisting"/> atomic replace, the no-<c>ReplaceExisting</c> +
	/// destination-exists -> <b>false-without-throwing</b> quirk, plain moves to a free destination, and
	/// the best-effort <see cref="MoveFileFlag.DelayUntilReboot"/> path. <c>File.Move(overwrite)</c> works
	/// on Windows too, so these run on the Windows CI host as a portable-path proxy.
	/// </para>
	/// </summary>
	[TestFixture]
	public class PortableFileMoverTests
	{
		private const string SourceContent = "SOURCE-CONTENT-0123456789";
		private const string DestinationContent = "DEST-CONTENT-abcdefghij";

		private string _tempDir;

		[SetUp]
		public void SetUp()
		{
			// Isolated, per-test temp directory under the OS temp path.
			_tempDir = Path.Combine(Path.GetTempPath(), "RssBandit_PortableFileMoverTests_" + Guid.NewGuid().ToString("N"));
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
		/// Case 1: ReplaceExisting + destination EXISTS -> atomic replace.
		/// Returns true; destination now holds the source's content; source is gone.
		/// </summary>
		[Test]
		public void MoveFile_ReplaceExisting_WhenDestinationExists_AtomicallyReplacesDestination()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = WriteFile("dest.dat", DestinationContent);

			bool moved = new PortableFileMover().MoveFile(src, dst, MoveFileFlag.ReplaceExisting);

			Assert.That(moved, Is.True, "MoveFile with ReplaceExisting should report success.");
			Assert.That(File.Exists(dst), Is.True, "Destination should still exist after the replace.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(SourceContent), "Destination should now hold the source content.");
			Assert.That(File.Exists(src), Is.False, "Source should no longer exist after the move.");
		}

		/// <summary>
		/// Case 2: ReplaceExisting + destination ABSENT -> plain move.
		/// Returns true; destination is created with the source content; source is gone.
		/// </summary>
		[Test]
		public void MoveFile_ReplaceExisting_WhenDestinationAbsent_MovesFile()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = PathFor("new-dest.dat");
			Assume.That(File.Exists(dst), Is.False, "Precondition: destination must not exist.");

			bool moved = new PortableFileMover().MoveFile(src, dst, MoveFileFlag.ReplaceExisting);

			Assert.That(moved, Is.True, "MoveFile with ReplaceExisting to an absent destination should succeed.");
			Assert.That(File.Exists(dst), Is.True, "Destination should have been created by the move.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(SourceContent), "Destination should hold the source content.");
			Assert.That(File.Exists(src), Is.False, "Source should no longer exist after the move.");
		}

		/// <summary>
		/// Case 3: NO ReplaceExisting + destination EXISTS -> fails WITHOUT throwing, both files intact.
		/// This is the Windows quirk (Task 1 Behavior 3) the portable impl must match: a managed
		/// File.Move without overwrite would throw IOException, so PortableFileMover guards and returns
		/// false instead.
		/// </summary>
		[Test]
		public void MoveFile_WithoutReplaceExisting_WhenDestinationExists_ReturnsFalseWithoutThrowing()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = WriteFile("dest.dat", DestinationContent);

			bool moved = false;
			Assert.DoesNotThrow(
				() => moved = new PortableFileMover().MoveFile(src, dst, MoveFileFlag.None),
				"Move without ReplaceExisting must not throw when the destination exists.");

			Assert.That(moved, Is.False, "Move without ReplaceExisting must fail when the destination exists.");
			Assert.That(File.Exists(src), Is.True, "Source must remain in place when the move fails.");
			Assert.That(File.ReadAllText(src), Is.EqualTo(SourceContent), "Source content must be untouched.");
			Assert.That(File.Exists(dst), Is.True, "Destination must still exist.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(DestinationContent), "Destination content must be untouched (not overwritten).");
		}

		/// <summary>
		/// Case 4: NO ReplaceExisting + destination ABSENT -> plain move.
		/// A default move to a free destination succeeds even without the ReplaceExisting flag.
		/// </summary>
		[Test]
		public void MoveFile_WithoutReplaceExisting_WhenDestinationAbsent_MovesFile()
		{
			string src = WriteFile("source.dat", SourceContent);
			string dst = PathFor("new-dest.dat");
			Assume.That(File.Exists(dst), Is.False, "Precondition: destination must not exist.");

			bool moved = new PortableFileMover().MoveFile(src, dst, MoveFileFlag.None);

			Assert.That(moved, Is.True, "Default move to an absent destination should succeed.");
			Assert.That(File.Exists(dst), Is.True, "Destination should have been created by the move.");
			Assert.That(File.ReadAllText(dst), Is.EqualTo(SourceContent), "Destination should hold the source content.");
			Assert.That(File.Exists(src), Is.False, "Source should no longer exist after the move.");
		}

		/// <summary>
		/// Case 5: DelayUntilReboot (newFileName == null) on an EXISTING file -> best-effort delete.
		/// There is no managed equivalent of the Windows reboot-time deletion, so the portable impl
		/// deletes the source now, returns true, and never throws.
		/// </summary>
		[Test]
		public void MoveFile_DelayUntilReboot_WhenSourceFileExists_BestEffortDeletesAndReturnsTrue()
		{
			string src = WriteFile("doomed.dat", SourceContent);

			bool moved = false;
			Assert.DoesNotThrow(
				() => moved = new PortableFileMover().MoveFile(src, null, MoveFileFlag.DelayUntilReboot),
				"DelayUntilReboot must never throw.");

			Assert.That(moved, Is.True, "DelayUntilReboot should report success (best-effort).");
			Assert.That(File.Exists(src), Is.False, "Best-effort delete should have removed the source file.");
		}

		/// <summary>
		/// Case 5b: DelayUntilReboot on an EXISTING directory -> best-effort recursive delete.
		/// (DestroyFolder routes directories through this path with a null newFileName.)
		/// </summary>
		[Test]
		public void MoveFile_DelayUntilReboot_WhenSourceDirectoryExists_BestEffortDeletesAndReturnsTrue()
		{
			string dir = PathFor("doomed-dir");
			Directory.CreateDirectory(dir);
			File.WriteAllText(Path.Combine(dir, "child.dat"), SourceContent);

			bool moved = false;
			Assert.DoesNotThrow(
				() => moved = new PortableFileMover().MoveFile(dir, null, MoveFileFlag.DelayUntilReboot),
				"DelayUntilReboot must never throw for a directory source.");

			Assert.That(moved, Is.True, "DelayUntilReboot should report success (best-effort).");
			Assert.That(Directory.Exists(dir), Is.False, "Best-effort delete should have removed the source directory.");
		}

		/// <summary>
		/// Case 5c: DelayUntilReboot on a MISSING source -> still best-effort, returns true, no throw.
		/// </summary>
		[Test]
		public void MoveFile_DelayUntilReboot_WhenSourceMissing_ReturnsTrueWithoutThrowing()
		{
			string missing = PathFor("never-existed.dat");
			Assume.That(File.Exists(missing), Is.False, "Precondition: source must not exist.");

			bool moved = false;
			Assert.DoesNotThrow(
				() => moved = new PortableFileMover().MoveFile(missing, null, MoveFileFlag.DelayUntilReboot),
				"DelayUntilReboot on a missing source must never throw.");

			Assert.That(moved, Is.True, "DelayUntilReboot should report success even when there is nothing to delete.");
		}
	}
}
