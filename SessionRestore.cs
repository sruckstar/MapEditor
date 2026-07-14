using System;
using System.IO;

namespace MapEditor
{
	/// <summary>
	/// The map being edited, parked on disk for the moment the script is not running.
	///
	/// A reload tears the script down and builds it again from nothing, but everything it spawned is game-side
	/// and stays exactly where it was — with no script left that knows those entities exist. The player is left
	/// looking at a map they can no longer select, save, or even delete, and only a restart of the game takes it
	/// away. So the map is written out here on the way down and taken back out of the world, and the instance
	/// that replaces this one puts it back as the map it is editing.
	/// </summary>
	public static class SessionRestore
	{
		/// <summary>
		/// Deliberately not under scripts\MapEditor: every .xml directly in there is read as a translation file,
		/// and every .xml in UserMaps is read as a map that might want autoloading.
		/// </summary>
		public const string FilePath = "scripts\\MapEditor.SessionRestore.xml";

		/// <summary>
		/// The game aborts its scripts on the way out as well, so quitting also leaves a file behind. A reload
		/// has the next instance asking for it within seconds of the last one writing it, and nothing else comes
		/// close, so age is what tells the two apart. An older file is from a previous run of the game: the world
		/// it belonged to is gone, and springing its map on the player now would be nothing but a surprise.
		/// </summary>
		private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

		/// <summary>Whether a map is waiting to be put back. A file too old to be one is dropped here.</summary>
		public static bool Pending
		{
			get
			{
				if (!File.Exists(FilePath)) return false;

				if (DateTime.Now - File.GetLastWriteTime(FilePath) <= MaxAge) return true;

				Discard();
				return false;
			}
		}

		/// <summary>
		/// Writes out everything the editor is holding, and says whether the map is now safe to take out of the
		/// world. An empty map is nothing to come back to, so it leaves no file at all — and takes away any older
		/// one, which would otherwise be restored in its place.
		///
		/// False means the map is only in the world: without a file to put it back from, deleting it would be the
		/// end of the player's work, so it is left standing to be cleaned up the old way, by restarting the game.
		/// </summary>
		public static bool Save()
		{
			var map = new Map();
			map.Objects.AddRange(PropStreamer.GetAllEntities());
			map.RemoveFromWorld.AddRange(PropStreamer.RemovedObjects);
			map.Markers.AddRange(PropStreamer.Markers);
			map.Metadata = PropStreamer.CurrentMapMetadata;

			if (map.Objects.Count == 0 && map.RemoveFromWorld.Count == 0 && map.Markers.Count == 0)
			{
				Discard();
				return true;
			}

			try
			{
				// The file a map came from is left out of a saved map, but this one is a hand-off between two
				// instances of the same editor: the map has to come back as it was, still tied to the file the
				// player has been saving it to.
				MapMetadata.SerializeFilename = true;
				new MapSerializer().Serialize(FilePath, map, MapSerializer.Format.NormalXml);
				return true;
			}
			catch (Exception e)
			{
				// The script is already on its way out, so there is nothing left to notify through: the log is
				// the only place this can be said.
				try
				{
					File.AppendAllText("scripts\\MapEditor.log",
						DateTime.Now + " SESSION FAILED TO SAVE:\r\n" + e + "\r\n");
				}
				catch (Exception) { }

				Discard();
				return false;
			}
			finally
			{
				MapMetadata.SerializeFilename = false;
			}
		}

		public static void Discard()
		{
			try
			{
				if (File.Exists(FilePath)) File.Delete(FilePath);
			}
			catch (Exception) { }
		}
	}
}
