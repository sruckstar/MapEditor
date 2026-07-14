using System.IO;

namespace MapEditor
{
	/// <summary>
	/// Home of every map the player saves. A bare filename typed into the save prompt is a relative path,
	/// which would otherwise resolve against the game's own directory and scatter maps next to GTA5.exe.
	/// </summary>
	public static class UserMaps
	{
		public const string Folder = "scripts\\MapEditor\\UserMaps";

		public static string EnsureFolder()
		{
			if (!Directory.Exists(Folder))
				Directory.CreateDirectory(Folder);
			return Folder;
		}

		/// <summary>
		/// Points <paramref name="filename"/> at <see cref="Folder"/>, and creates the folder if this is the
		/// first map being saved. A path the player spelled out themselves is left where they put it.
		/// </summary>
		public static string Resolve(string filename)
		{
			if (string.IsNullOrWhiteSpace(filename)) return filename;

			filename = filename.Trim();

			if (HasDirectory(filename))
			{
				var directory = Path.GetDirectoryName(Path.GetFullPath(filename));
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					Directory.CreateDirectory(directory);
				return filename;
			}

			return Path.Combine(EnsureFolder(), filename);
		}

		private static bool HasDirectory(string filename)
		{
			return Path.IsPathRooted(filename) ||
			       filename.IndexOf(Path.DirectorySeparatorChar) != -1 ||
			       filename.IndexOf(Path.AltDirectorySeparatorChar) != -1;
		}
	}
}
