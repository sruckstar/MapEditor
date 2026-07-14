using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GTA;
using GTA.Native;

namespace MapEditor
{
	public class ObjectCategory
	{
		public ObjectCategory(string name)
		{
			Name = name;
		}

		public string Name { get; private set; }

		public Dictionary<string, int> Objects = new Dictionary<string, int>();
	}

	/// <summary>
	/// Splits the flat model lists into browsable categories, one plain-text file per category under
	/// <see cref="CategoriesRoot"/>. The files are the source of truth: to move an object between
	/// categories, or to add a model the lists do not know about, edit the .txt.
	///
	/// Prop categories ship with the mod (generated from ObjectList.ini by tools/generate_categories.py).
	/// Vehicle and ped categories cannot ship, because both lists are built at runtime from the SHVDN
	/// enums, so they are written out on first run and read back like any other category file.
	/// </summary>
	public static class ObjectCategories
	{
		private const string CategoriesRoot = "scripts\\MapEditor\\Categories";

		/// <summary>Pseudo-category placed first, holding the whole list, so the old flat browsing still works.</summary>
		public const string AllCategoryName = "All";

		/// <summary>Catches models that no category file claims, so an object can never drop out of the menu.</summary>
		public const string UncategorizedName = "Uncategorized";

		public static List<ObjectCategory> Props = new List<ObjectCategory>();
		public static List<ObjectCategory> Vehicles = new List<ObjectCategory>();
		public static List<ObjectCategory> Peds = new List<ObjectCategory>();

		public static List<ObjectCategory> For(ObjectTypes type)
		{
			switch (type)
			{
				case ObjectTypes.Vehicle: return Vehicles;
				case ObjectTypes.Ped: return Peds;
				default: return Props;
			}
		}

		internal static void LoadAll()
		{
			Props = Load("Props", ObjectDatabase.MainDb, null);
			Vehicles = Load("Vehicles", ObjectDatabase.VehicleDb, BuildVehicleCategories);
			Peds = Load("Peds", ObjectDatabase.PedDb, BuildPedCategories);
		}

		/// <summary>
		/// Deletes the generated category files and rebuilds them from the built-in rules. Only the
		/// vehicle and ped folders have rules to rebuild from; prop categories ship as data and are
		/// regenerated with tools/generate_categories.py instead.
		/// </summary>
		internal static void Regenerate()
		{
			foreach (var folder in new[] { "Vehicles", "Peds" })
			{
				string dir = Path.Combine(CategoriesRoot, folder);
				if (!Directory.Exists(dir)) continue;
				foreach (var file in Directory.GetFiles(dir, "*.txt"))
					File.Delete(file);
			}
			LoadAll();
		}

		private static List<ObjectCategory> Load(string folder, Dictionary<string, int> db,
			Func<Dictionary<string, List<string>>> bootstrap)
		{
			var categories = new List<ObjectCategory>();
			if (db == null) return categories;

			string dir = Path.Combine(CategoriesRoot, folder);
			if (bootstrap != null && (!Directory.Exists(dir) || Directory.GetFiles(dir, "*.txt").Length == 0))
				Write(dir, bootstrap());

			var claimed = new HashSet<string>();
			if (Directory.Exists(dir))
			{
				// Sorted by filename: the NN_ prefix on the shipped files is what fixes the menu order.
				foreach (var file in Directory.GetFiles(dir, "*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
				{
					var category = new ObjectCategory(DisplayName(file));
					foreach (var raw in File.ReadAllLines(file))
					{
						var name = raw.Trim();
						if (name.Length == 0 || name[0] == '#' || category.Objects.ContainsKey(name)) continue;

						int hash;
						if (!db.TryGetValue(name, out hash))
						{
							// A hand-added model the list has never seen. Resolve it and fold it into
							// the database so that search and saving recognise it too. This has to
							// happen before "All" is built below, or it would be missing from there.
							hash = new Model(name).Hash;
							db[name] = hash;
						}
						category.Objects.Add(name, hash);
						claimed.Add(name);
					}
					if (category.Objects.Count > 0)
						categories.Add(category);
				}
			}

			// Everything, first, so a user who does not care about categories keeps the old behaviour.
			var all = new ObjectCategory(AllCategoryName);
			foreach (var pair in db)
				all.Objects.Add(pair.Key, pair.Value);
			categories.Insert(0, all);

			// With no category files at all there is nothing for this to contrast with: every object
			// would simply be listed twice, under "All" and again under "Uncategorized".
			if (categories.Count == 1) return categories;

			var uncategorized = new ObjectCategory(UncategorizedName);
			foreach (var pair in db.Where(pair => !claimed.Contains(pair.Key)))
				uncategorized.Objects.Add(pair.Key, pair.Value);
			if (uncategorized.Objects.Count > 0)
				categories.Add(uncategorized);

			return categories;
		}

		/// <summary>Strips the ordering prefix: "07_Signs &amp; Billboards.txt" browses as "Signs &amp; Billboards".</summary>
		private static string DisplayName(string path)
		{
			var name = Path.GetFileNameWithoutExtension(path);
			return Regex.Replace(name, @"^\d+[_\-\s]+", string.Empty);
		}

		private static void Write(string dir, Dictionary<string, List<string>> categories)
		{
			Directory.CreateDirectory(dir);
			int index = 0;
			foreach (var pair in categories)
			{
				if (pair.Value.Count == 0) continue;
				index++;

				var sb = new StringBuilder();
				sb.Append("# ").Append(pair.Key).Append(Environment.NewLine);
				sb.Append("# One model name per line. Lines starting with # are ignored.").Append(Environment.NewLine);
				sb.Append("# Add an object to this category by appending its model name below.").Append(Environment.NewLine);
				foreach (var name in pair.Value.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
					sb.Append(name).Append(Environment.NewLine);

				var file = string.Format("{0:00}_{1}.txt", index, Sanitize(pair.Key));
				File.WriteAllText(Path.Combine(dir, file), sb.ToString());
			}
		}

		private static string Sanitize(string name)
		{
			return Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, '-'));
		}

		// GET_VEHICLE_CLASS_FROM_NAME returns the index into this table; it is the game's own grouping,
		// so it beats anything we could infer from the model name.
		private static readonly string[] VehicleClasses =
		{
			"Compacts", "Sedans", "SUVs", "Coupes", "Muscle", "Sports Classics", "Sports", "Super",
			"Motorcycles", "Off-road", "Industrial", "Utility", "Vans", "Cycles", "Boats",
			"Helicopters", "Planes", "Service", "Emergency", "Military", "Commercial", "Trains",
		};

		private static Dictionary<string, List<string>> BuildVehicleCategories()
		{
			var result = new Dictionary<string, List<string>>();
			foreach (var name in VehicleClasses)
				result.Add(name, new List<string>());
			result.Add("Other Vehicles", new List<string>());

			foreach (var pair in ObjectDatabase.VehicleDb)
			{
				int index = Function.Call<int>(Hash.GET_VEHICLE_CLASS_FROM_NAME, pair.Value);
				var category = index >= 0 && index < VehicleClasses.Length
					? VehicleClasses[index]
					: "Other Vehicles";
				result[category].Add(pair.Key);
			}
			return result;
		}

		// PedHash names are the SHVDN enum names ("Hooker01SFY"), not model names, so the family has to
		// be read off the trailing a/s/g/u + gender + age code the game uses: a_f_y_..., s_m_m_..., etc.
		private static readonly Regex PedFamilySuffix = new Regex(@"([ASGUM])([FM])([YMO])\d*$", RegexOptions.Compiled);

		private static readonly List<KeyValuePair<string, string>> PedModelPrefixes = new List<KeyValuePair<string, string>>
		{
			new KeyValuePair<string, string>("a_c_", "Animals"),
			new KeyValuePair<string, string>("a_f_", "Ambient"),
			new KeyValuePair<string, string>("a_m_", "Ambient"),
			new KeyValuePair<string, string>("s_f_", "Service & Police"),
			new KeyValuePair<string, string>("s_m_", "Service & Police"),
			new KeyValuePair<string, string>("g_f_", "Gangs"),
			new KeyValuePair<string, string>("g_m_", "Gangs"),
			new KeyValuePair<string, string>("u_f_", "Unique"),
			new KeyValuePair<string, string>("u_m_", "Unique"),
			new KeyValuePair<string, string>("ig_", "Story & Cutscene"),
			new KeyValuePair<string, string>("cs_", "Story & Cutscene"),
			new KeyValuePair<string, string>("csb_", "Story & Cutscene"),
			new KeyValuePair<string, string>("mp_", "Multiplayer"),
			new KeyValuePair<string, string>("player_", "Player"),
			new KeyValuePair<string, string>("hc_", "Gangs"),
		};

		private static Dictionary<string, List<string>> BuildPedCategories()
		{
			var order = new[]
			{
				"Story & Cutscene", "Ambient", "Service & Police", "Gangs", "Unique",
				"Multiplayer", "Player", "Animals", "Other Peds",
			};
			var result = new Dictionary<string, List<string>>();
			foreach (var name in order)
				result.Add(name, new List<string>());

			// PedList.ini and ObjectList.ini key on the same Jenkins hash, so a ped that also appears in
			// the object list can be mapped back to its real model name and classified exactly.
			var modelNamesByHash = new Dictionary<int, string>();
			if (ObjectDatabase.MainDb != null)
			{
				foreach (var pair in ObjectDatabase.MainDb)
					modelNamesByHash[pair.Value] = pair.Key;
			}

			foreach (var pair in ObjectDatabase.PedDb)
				result[PedCategoryFor(pair.Key, pair.Value, modelNamesByHash)].Add(pair.Key);

			return result;
		}

		private static string PedCategoryFor(string enumName, int hash, Dictionary<int, string> modelNamesByHash)
		{
			string modelName;
			if (modelNamesByHash.TryGetValue(hash, out modelName))
			{
				var lower = modelName.ToLowerInvariant();
				foreach (var prefix in PedModelPrefixes)
				{
					if (lower.StartsWith(prefix.Key, StringComparison.Ordinal))
						return prefix.Value;
				}
			}

			// Newer DLC peds are absent from ObjectList.ini; fall back to the enum name's own convention.
			if (enumName.EndsWith("Cutscene", StringComparison.OrdinalIgnoreCase))
				return "Story & Cutscene";
			if (enumName.StartsWith("Freemode", StringComparison.OrdinalIgnoreCase))
				return "Multiplayer";

			var match = PedFamilySuffix.Match(enumName);
			if (match.Success)
			{
				switch (match.Groups[1].Value)
				{
					case "A": return "Ambient";
					case "S": return "Service & Police";
					case "G": return "Gangs";
					case "U": return "Unique";
					case "M": return "Multiplayer";
				}
			}

			return "Other Peds";
		}
	}
}
