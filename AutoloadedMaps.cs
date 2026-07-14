using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace MapEditor
{
	/// <summary>
	/// The maps the player marked for autoloading, spawned once when the script starts.
	///
	/// They are deliberately kept out of <see cref="PropStreamer"/>: an autoloaded map is scenery the player
	/// wants standing while they build something else, not the map they are editing. Staying out of the
	/// streamer's lists is what keeps them out of the entity menu, out of whatever gets saved, and — the
	/// reason this class exists — standing through "New Map". <see cref="Unload"/> and <see cref="UnloadAll"/>
	/// are the only way out.
	/// </summary>
	public static class AutoloadedMaps
	{
		/// <summary>
		/// Maps dropped in here autoload no matter what they say, from before the flag moved into the map file.
		/// </summary>
		private const string LegacyFolder = "scripts\\AutoloadMaps";

		/// <summary>
		/// What one autoloaded map put into the world. Kept per map rather than in one shared pile so that a
		/// single map can be taken back out while the others stay standing.
		/// </summary>
		private sealed class LoadedMap
		{
			public LoadedMap(string name)
			{
				Name = name;
			}

			public string Name { get; }

			public readonly List<Entity> Entities = new List<Entity>();
			public readonly List<int> Pickups = new List<int>();
			public readonly List<Marker> Markers = new List<Marker>();
			public readonly List<MapObject> RemovedObjects = new List<MapObject>();
		}

		private static readonly List<LoadedMap> Maps = new List<LoadedMap>();

		private static bool _justTeleported;

		public static int MapCount => Maps.Count;

		public static int EntityCount => Maps.Sum(m => m.Entities.Count + m.Pickups.Count);

		public static bool Any => Maps.Count > 0;

		/// <summary>The loaded maps by name, in the order <see cref="Unload"/> indexes them.</summary>
		public static IEnumerable<string> Names => Maps.Select(m => m.Name);

		public static void LoadAll()
		{
			foreach (var path in FindMaps())
			{
				try
				{
					Load(path);
				}
				catch (Exception e)
				{
					Compat.Notify("~r~~h~Map Editor~h~~w~~n~" + Translation.Translate("Map failed to load, see error below."));
					Compat.Notify(e.Message);
					File.AppendAllText("scripts\\MapEditor.log",
						DateTime.Now + " AUTOLOAD FAILED (" + path + "):\r\n" + e + "\r\n");
				}
			}

			if (Maps.Count > 0)
				Compat.Notify("~b~~h~Map Editor~h~~w~~n~" + Translation.Translate("Autoloaded maps:") + " ~h~" +
				              string.Join(", ", Names.ToArray()) + "~h~.");
		}

		/// <summary>
		/// Every map that asked to be loaded: the ones in the user's folder that carry the flag, plus everything
		/// in the legacy folder.
		/// </summary>
		private static IEnumerable<string> FindMaps()
		{
			var found = new List<string>();

			if (Directory.Exists(UserMaps.Folder))
			{
				foreach (var file in Directory.GetFiles(UserMaps.Folder, "*.xml"))
				{
					if (WantsAutoload(file))
						found.Add(file);
				}
			}

			if (Directory.Exists(LegacyFolder))
			{
				found.AddRange(Directory.GetFiles(LegacyFolder, "*.xml"));
				found.AddRange(Directory.GetFiles(LegacyFolder, "*.ini"));
			}

			return found;
		}

		/// <summary>
		/// Reads only the flag. The folder holds every map the player ever saved, most of which are not meant to
		/// spawn, and a Menyoo or otherwise foreign .xml in there is simply not one of ours.
		/// </summary>
		private static bool WantsAutoload(string path)
		{
			try
			{
				var map = new MapSerializer().Deserialize(path, MapSerializer.Format.NormalXml);
				return map?.Metadata != null && map.Metadata.Autoload;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static void Load(string path)
		{
			var format = path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
				? MapSerializer.Format.SimpleTrainer
				: MapSerializer.Format.NormalXml;

			var map = new MapSerializer().Deserialize(path, format);
			if (map == null) return;

			var loaded = new LoadedMap(map.Metadata != null && !string.IsNullOrWhiteSpace(map.Metadata.Name)
				? map.Metadata.Name
				: Path.GetFileNameWithoutExtension(path));

			// Listed before anything is spawned, so a map that throws halfway through still owns what it managed
			// to put into the world and can be unloaded again.
			Maps.Add(loaded);

			foreach (var o in map.Objects)
			{
				if (o != null) Spawn(o, loaded);
			}

			foreach (var o in map.RemoveFromWorld)
			{
				if (o != null) loaded.RemovedObjects.Add(o);
			}

			foreach (var marker in map.Markers)
			{
				if (marker != null) loaded.Markers.Add(marker);
			}
		}

		private static void Spawn(MapObject o, LoadedMap map)
		{
			// A pickup is named by a pickup hash, not a model hash. Handing that to the model loader would send
			// it looking for a model that does not exist and blacklist the hash on the way out.
			if (o.Type == ObjectTypes.Pickup)
			{
				var pickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, o.Hash, o.Position.X, o.Position.Y,
					o.Position.Z, 0f, 0f, o.Rotation.Z, 515, o.Amount, 0, false, 0);
				if (pickup != 0) map.Pickups.Add(pickup);
				return;
			}

			var model = ObjectPreview.LoadObject(o.Hash);
			if (model == null) return;

			switch (o.Type)
			{
				case ObjectTypes.Prop:
				{
					// A door is spawned static so the game does not drop it, then unfrozen so it can still swing.
					var prop = World.CreatePropNoOffset(model, o.Position, o.Rotation, o.Dynamic && !o.Door);
					if (prop == null) break;

					if (o.Quaternion != null && !IsUnset(o.Quaternion))
						Quaternion.SetEntityQuaternion(prop, o.Quaternion);

					prop.PositionNoOffset = o.Position;
					prop.IsPositionFrozen = !o.Dynamic && !o.Door;
					Track(prop, map);
					break;
				}
				case ObjectTypes.Vehicle:
				{
					var vehicle = World.CreateVehicle(model, o.Position, o.Rotation.Z);
					if (vehicle == null) break;

					vehicle.Mods.PrimaryColor = (VehicleColor) o.PrimaryColor;
					vehicle.Mods.SecondaryColor = (VehicleColor) o.SecondaryColor;
					vehicle.IsSirenActive = o.SirensActive;
					vehicle.IsPositionFrozen = !o.Dynamic;
					Track(vehicle, map);
					break;
				}
				case ObjectTypes.Ped:
				{
					// Peds stand on their feet where props hang from their centre, and the editor stores the centre.
					var ped = World.CreatePed(model, o.Position - new Vector3(0f, 0f, 1f), o.Rotation.Z);
					if (ped == null) break;

					ped.IsPositionFrozen = !o.Dynamic;

					if (o.Weapon.HasValue && o.Weapon.Value != WeaponHash.Unarmed)
						ped.Weapons.Give(o.Weapon.Value, 999, true, true);

					if (!string.IsNullOrEmpty(o.Relationship) && o.Relationship != "Companion")
						ObjectDatabase.SetPedRelationshipGroup(ped, o.Relationship);

					StartScenario(ped, o.Action);
					Track(ped, map);
					break;
				}
			}

			model.MarkAsNoLongerNeeded();
		}

		/// <summary>A quaternion that was never written to the map file, as opposed to a real rotation.</summary>
		private static bool IsUnset(Quaternion q)
		{
			return q.X == 0 && q.Y == 0 && q.Z == 0 && q.W == 0;
		}

		private static void StartScenario(Ped ped, string action)
		{
			if (string.IsNullOrEmpty(action) || action == "None") return;

			switch (action)
			{
				case "Any":
				case "Any - Walk":
					Function.Call(Hash.TASK_USE_NEAREST_SCENARIO_TO_COORD, ped.Handle, ped.Position.X, ped.Position.Y,
						ped.Position.Z, 100f, -1);
					return;
				case "Any - Warp":
					Function.Call(Hash.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP, ped.Handle, ped.Position.X,
						ped.Position.Y, ped.Position.Z, 100f, -1);
					return;
				case "Wander":
					ped.Task.WanderAround();
					return;
			}

			string scenario;
			if (ObjectDatabase.ScrenarioDatabase.TryGetValue(action, out scenario))
				Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ped.Handle, scenario, 0, 0);
		}

		/// <summary>
		/// Persistence is what stops the game from streaming the map out again the moment the player walks away.
		/// </summary>
		private static void Track(Entity entity, LoadedMap map)
		{
			entity.IsPersistent = true;
			map.Entities.Add(entity);
		}

		/// <summary>Every marker still standing, from whichever maps are still loaded.</summary>
		private static IEnumerable<Marker> AllMarkers => Maps.SelectMany(m => m.Markers);

		public static void Tick()
		{
			if (Maps.Count == 0) return;

			foreach (var map in Maps)
			{
				foreach (var o in map.RemovedObjects)
				{
					var prop = World.GetClosestProp(o.Position, 1f, new Model(o.Hash));
					// A prop another autoloaded map spawned here is not the world prop this one wants gone.
					if (prop == null || !prop.Exists() || Maps.Any(m => m.Entities.Contains(prop))) continue;
					prop.Delete();
				}
			}

			foreach (var marker in AllMarkers)
			{
				if (marker.OnlyVisibleInEditor && !MapEditor.IsInFreecam) continue;

				World.DrawMarker(marker.Type, marker.Position, Vector3.Zero, marker.Rotation, marker.Scale,
					System.Drawing.Color.FromArgb(marker.Alpha, marker.Red, marker.Green, marker.Blue),
					marker.BobUpAndDown, marker.RotateToCamera);
			}

			TickTeleports();
		}

		private static void TickTeleports()
		{
			foreach (var marker in AllMarkers)
			{
				if (!marker.TeleportTarget.HasValue || _justTeleported) continue;
				if (!Game.Player.Character.IsInRange(marker.Position, Math.Max(2f, marker.Scale.X))) continue;

				if (Game.Player.Character.IsInVehicle())
					Game.Player.Character.CurrentVehicle.Position = marker.TeleportTarget.Value;
				else
					Game.Player.Character.Position = marker.TeleportTarget.Value;

				_justTeleported = true;
			}

			// Held down until the player leaves the pad, or they would be bounced straight back on arrival.
			if (_justTeleported && !AllMarkers.Any(m => m.TeleportTarget.HasValue &&
			                                            Game.Player.Character.IsInRange(m.Position, Math.Max(2f, m.Scale.X))))
				_justTeleported = false;
		}

		/// <summary>
		/// Takes one autoloaded map back out of the world, by its index in <see cref="Names"/>, and leaves the
		/// others standing. Returns the name of the map that went, or null if the index was not one.
		/// </summary>
		public static string Unload(int index)
		{
			if (index < 0 || index >= Maps.Count) return null;

			var map = Maps[index];
			Maps.RemoveAt(index);
			Remove(map);

			// Tick, and with it the latch's own reset, stops running once the last map is gone.
			if (Maps.Count == 0) _justTeleported = false;

			return map.Name;
		}

		/// <summary>
		/// Takes every autoloaded map back out of the world, leaving the map being edited untouched.
		/// </summary>
		public static void UnloadAll()
		{
			foreach (var map in Maps)
				Remove(map);

			Maps.Clear();
			_justTeleported = false;
		}

		/// <summary>
		/// Everything one map put into the world goes, and the world objects it deleted come back, since nothing
		/// is holding them down any more. A map still loaded that deletes the same object takes it out again on
		/// the next tick.
		/// </summary>
		private static void Remove(LoadedMap map)
		{
			foreach (var entity in map.Entities)
			{
				if (entity != null && entity.Exists())
					entity.Delete();
			}

			foreach (var pickup in map.Pickups)
				Function.Call(Hash.REMOVE_PICKUP, pickup);

			foreach (var o in map.RemovedObjects)
			{
				var prop = World.CreateProp(new Model(o.Hash), o.Position, o.Rotation, true, false);
				if (prop != null) prop.PositionNoOffset = o.Position;
			}

			map.Entities.Clear();
			map.Pickups.Clear();
			map.Markers.Clear();
			map.RemovedObjects.Clear();
		}
	}
}
