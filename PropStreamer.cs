using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;

namespace MapEditor
{
	/// <summary>
	/// Only create the first 200 objects that are in proximity to the player.
	/// </summary>
	public static class PropStreamer
	{
		public static int MAX_OBJECTS = 2048;

	    public static List<int> UsedModels = new List<int>();

		public static List<MapObject> MemoryObjects = new List<MapObject>();

		public static List<int> StreamedInHandles = new List<int>();

		public static List<int> StaticProps = new List<int>();

		public static List<int> Vehicles = new List<int>();

		public static List<int> Peds = new List<int>();

        public static List<DynamicPickup> Pickups = new List<DynamicPickup>();
        
        public static Dictionary<int, string> Identifications = new Dictionary<int, string>();

		public static List<Marker> Markers = new List<Marker>();

		public static Dictionary<int, string> ActiveScenarios = new Dictionary<int, string>();

		public static Dictionary<int, string> ActiveRelationships = new Dictionary<int, string>();

		public static Dictionary<int, WeaponHash> ActiveWeapons = new Dictionary<int, WeaponHash>();

        public static List<int> Doors = new List<int>(); 

		public static List<int> ActiveSirens = new List<int>();

		public static int PropCount => StreamedInHandles.Count + MemoryObjects.Count;

		public static int EntityCount => StreamedInHandles.Count + MemoryObjects.Count + Vehicles.Count + Peds.Count;

		public static List<MapObject> RemovedObjects = new List<MapObject>();

	    public static MapMetadata CurrentMapMetadata = new MapMetadata();
        
        public static Prop CreateProp(Model model, Vector3 position, Vector3 rotation, bool dynamic, Quaternion q = null, bool force = false, int drawDistance = -1)
		{
			if (StreamedInHandles.Count >= MAX_OBJECTS)
			{
				Compat.Notify("~r~~h~Map Editor~h~~w~\nYou have reached the prop limit. You cannot place any more props.");
				return null;
			}

            if (PropCount > 0 && PropCount % 249 == 0)
                Script.Wait(100);

			var prop = Compat.PropFrom(Function.Call<int>(Hash.CREATE_OBJECT_NO_OFFSET, model.Hash, position.X, position.Y, position.Z, true, true, dynamic));
			if (prop == null)
			{
				Compat.Notify("~r~~h~Map Editor~h~~w~~n~The prop failed to spawn.");
				return null;
			}
            prop.Rotation = rotation;
			StreamedInHandles.Add(prop.Handle);
			if (!dynamic)
			{
				StaticProps.Add(prop.Handle);
				prop.IsPositionFrozen = true;
			}
			if (q != null)
				Quaternion.SetEntityQuaternion(prop, q);
			prop.Position = position;
		    if (drawDistance != -1)
		        prop.LodDistance = drawDistance;
            UsedModels.Add(model.Hash);
            model.MarkAsNoLongerNeeded();
			return prop;
		}

		public static Vehicle CreateVehicle(Model model, Vector3 position, float heading, bool dynamic, Quaternion q = null, int drawDistance = -1)
		{
			Vehicle veh;
			int counter = 0;
			do
			{
				veh = World.CreateVehicle(model, position, heading);
				counter++;
			} while (veh == null && counter < 2000);

			if (veh == null)
			{
				Compat.Notify("~r~~h~Map Editor~h~~w~~n~I tried very hard, but the vehicle failed to load.");
				return null;
			}

			Vehicles.Add(veh.Handle);
			if (!dynamic)
			{
				StaticProps.Add(veh.Handle);
				veh.IsPositionFrozen = true;
			}
			if(q != null)
				Quaternion.SetEntityQuaternion(veh, q);
		    if (drawDistance != -1)
		        veh.LodDistance = drawDistance;
            UsedModels.Add(model.Hash);
            model.MarkAsNoLongerNeeded();
            return veh;
		}

		public static Ped CreatePed(Model model, Vector3 position, float heading, bool dynamic, Quaternion q = null, int drawDistance = -1)
		{
			var veh = World.CreatePed(model, position, heading);
			Peds.Add(veh.Handle);
			if (!dynamic)
			{
				StaticProps.Add(veh.Handle);
				veh.IsPositionFrozen = true;
			}
			if (q != null)
				Quaternion.SetEntityQuaternion(veh, q);
		    if (drawDistance != -1)
		        veh.LodDistance = drawDistance;
            UsedModels.Add(model.Hash);
            model.MarkAsNoLongerNeeded();
            return veh;
		}

	    private static int _pickupIds = 0;
        public static DynamicPickup CreatePickup(Model model, Vector3 position, float heading, int amount, bool dynamic, Quaternion q = null)
        {
            var v_4 = 515;
            int newPickup = -1;

            if (Game.Player.Character.IsInRange(position, 30f))
            {
                newPickup = Function.Call<int>(Hash.CREATE_PICKUP_ROTATE, model.Hash, position.X, position.Y,
                    position.Z, 0, 0, heading, v_4, amount, 0, false, 0);
            }

            var pcObj = new DynamicPickup(newPickup);
            pcObj.Flag = v_4;
            pcObj.Amount = amount;
            pcObj.RealPosition = position;
            if (newPickup != -1)
            {
                var start = 0;
                while (pcObj.ObjectHandle == -1 && start < 20)
                {
                    start++;
                    Script.Yield();
                }

                pcObj.Dynamic = false;

                var pickupObject = Compat.PropFrom(pcObj.ObjectHandle);
                if (pickupObject != null)
                {
                    pickupObject.IsPersistent = true;
                    if (q != null)
                        Quaternion.SetEntityQuaternion(pickupObject, q);
                }
                pcObj.UpdatePos();
            }
            
            Pickups.Add(pcObj);
            pcObj.PickupHash = model.Hash;
            pcObj.Timeout = 1;
            pcObj.UID = _pickupIds++;
            return pcObj;
        }

	    public static DynamicPickup GetPickup(int objectHandle)
	    {
            DynamicPickup pc = null;
            foreach (var pickup in Pickups)
            {
                if (pickup.ObjectHandle == objectHandle)
                {
                    pc = pickup;
                    break;
                }
            }

	        return pc;
	    }

        public static DynamicPickup GetPickupByUID(int uid)
        {
            DynamicPickup pc = null;
            foreach (var pickup in Pickups)
            {
                if (pickup.UID == uid)
                {
                    pc = pickup;
                    break;
                }
            }

            return pc;
        }

        public static void RemoveVehicle(int handle)
		{
		    var veh = Compat.VehicleFrom(handle);
		    if (veh != null)
		    {
		        ReleaseModel(veh.Model);
		        veh.Delete();
		    }
			if (Vehicles.Contains(handle)) Vehicles.Remove(handle);
			if (StaticProps.Contains(handle)) StaticProps.Remove(handle);
		}

		public static void RemovePed(int handle)
		{
		    var ped = Compat.PedFrom(handle);
		    if (ped != null)
		    {
		        ReleaseModel(ped.Model);
		        ped.Delete();
		    }
			if (Peds.Contains(handle)) Peds.Remove(handle);
			if (StaticProps.Contains(handle)) StaticProps.Remove(handle);
        }

		/// <summary>
		/// Drops one usage of a model and releases it back to the streamer when nothing else needs it.
		/// </summary>
		private static void ReleaseModel(Model model)
		{
		    UsedModels.Remove(model.Hash);
		    if (!UsedModels.Contains(model.Hash))
		        model.MarkAsNoLongerNeeded();
		}

	    public static void RemovePickup(int objectHandle)
	    {
	        DynamicPickup pc = null;
	        foreach (var pickup in Pickups)
	        {
	            if (pickup.ObjectHandle == objectHandle)
	            {
	                pc = pickup;
                    pc.Remove();
	                break;
	            }
	        }

	        if (pc != null) Pickups.Remove(pc);   
	    }

	    public static bool IsPickup(int entity)
	    {
	        return Pickups.Any(pickup => pickup.ObjectHandle == entity);
	    }

	    public static void RemoveEntity(int handle)
		{
		    var entity = handle != 0 ? Compat.Ent(handle) : null;
		    if (entity != null)
		        ReleaseModel(entity.Model);

	        if (IsPickup(handle))
	        {
	            var ourPickup = GetPickup(handle);
	            if (Pickups.Contains(ourPickup)) Pickups.Remove(ourPickup);
                ourPickup.Remove();
	        }
	        else
	        {
	            entity?.Delete();
	        }
	        if (Peds.Contains(handle)) Peds.Remove(handle);
			if (Vehicles.Contains(handle)) Vehicles.Remove(handle);
			if (StreamedInHandles.Contains(handle)) StreamedInHandles.Remove(handle);
		}

		internal static void AddProp(Prop prop, bool dynamic)
		{
			if (StreamedInHandles.Count > MAX_OBJECTS)
			{
				MemoryObjects.Add(new MapObject() {Dynamic = dynamic, Hash = prop.Model.Hash, Position = prop.Position, Quaternion = Quaternion.GetEntityQuaternion(prop), Rotation = prop.Rotation, Type = ObjectTypes.Prop});
				prop.Delete();
				return;
			}
			StreamedInHandles.Add(prop.Handle);
			if(!dynamic)
				StaticProps.Add(prop.Handle);
		}

		internal static void RemoveProp(Prop prop, bool dynamic)
		{
			if(StreamedInHandles.Contains(prop.Handle)) StreamedInHandles.Remove(prop.Handle);
			if(StaticProps.Contains(prop.Handle)) StaticProps.Remove(prop.Handle);
			if(MemoryObjects.Contains(new MapObject() { Dynamic = dynamic, Hash = prop.Model.Hash, Position = prop.Position, Quaternion = Quaternion.GetEntityQuaternion(prop), Rotation = prop.Rotation, Type = ObjectTypes.Prop })) 
				MemoryObjects.Remove(new MapObject() { Dynamic = dynamic, Hash = prop.Model.Hash, Position = prop.Position, Quaternion = Quaternion.GetEntityQuaternion(prop), Rotation = prop.Rotation, Type = ObjectTypes.Prop });
		}

		public static void RemoveAll()
		{
			StreamedInHandles.ForEach(i => Compat.Ent(i)?.Delete());
			StreamedInHandles.Clear();
			MemoryObjects.Clear();
			StaticProps.Clear();
			Vehicles.ForEach(v => Compat.Ent(v)?.Delete());
			Peds.ForEach(v => Compat.Ent(v)?.Delete());
            Pickups.ForEach(p => p.Remove());
			Vehicles.Clear();
			Peds.Clear();
            Pickups.Clear();
		}

		public static MapObject[] GetAllEntities()
		{
			var outList = new List<MapObject>();

			foreach (int handle in StreamedInHandles)
			{
				var prop = Compat.Ent(handle);
				if (prop == null) continue;
				outList.Add(new MapObject()
				{
					Dynamic = !StaticProps.Contains(handle),
					Hash = prop.Model.Hash,
					Position = prop.Position,
					Quaternion = Quaternion.GetEntityQuaternion(prop),
					Rotation = prop.Rotation,
					Type = ObjectTypes.Prop,
					Door = Doors.Contains(handle),
					Id = (Identifications.ContainsKey(handle) && !string.IsNullOrWhiteSpace(Identifications[handle])) ? Identifications[handle] : null,
				});
			}

			outList.AddRange(MemoryObjects);

			foreach (int v in Vehicles)
			{
				var veh = Compat.VehicleFrom(v);
				if (veh == null) continue;
				outList.Add(new MapObject()
				{
					Dynamic = !StaticProps.Contains(v),
					Hash = veh.Model.Hash,
					Position = veh.Position,
					Quaternion = Quaternion.GetEntityQuaternion(veh),
					Rotation = veh.Rotation,
					Type = ObjectTypes.Vehicle,
					Id = (Identifications.ContainsKey(v) && !string.IsNullOrWhiteSpace(Identifications[v])) ? Identifications[v] : null,
					SirensActive = ActiveSirens.Contains(v),
					PrimaryColor = (int)veh.Mods.PrimaryColor,
					SecondaryColor = (int)veh.Mods.SecondaryColor,
				});
			}

			foreach (int v in Peds)
			{
				var ped = Compat.PedFrom(v);
				if (ped == null) continue;
				outList.Add(new MapObject()
				{
					Dynamic = !StaticProps.Contains(v),
					Hash = ped.Model.Hash,
					Position = ped.Position,
					Quaternion = Quaternion.GetEntityQuaternion(ped),
					Rotation = ped.Rotation,
					Type = ObjectTypes.Ped,
					Action = ActiveScenarios.ContainsKey(v) ? ActiveScenarios[v] : "None",
					Id = (Identifications.ContainsKey(v) && !string.IsNullOrWhiteSpace(Identifications[v])) ? Identifications[v] : null,
					Relationship = ActiveRelationships.ContainsKey(v) ? ActiveRelationships[v] : null,
					Weapon = ActiveWeapons.ContainsKey(v) ? ActiveWeapons[v] : (WeaponHash?)null,
				});
			}

			foreach (DynamicPickup p in Pickups)
			{
				var pickupObject = Compat.Ent(p.ObjectHandle);
				outList.Add(new MapObject()
				{
					Dynamic = p.Dynamic,
					Hash = p.PickupHash,
					Position = p.RealPosition,
					Quaternion = pickupObject != null ? Quaternion.GetEntityQuaternion(pickupObject) : new Quaternion(),
					Rotation = pickupObject?.Rotation ?? new Vector3(),
					Type = ObjectTypes.Pickup,
					Amount = p.Amount,
					RespawnTimer = p.Timeout,
					Flag = p.Flag,
				});
			}

			return outList.ToArray();
		}

		public static int[] GetAllHandles()
		{
			List<int> outHandles = new List<int>();
			outHandles.AddRange(StreamedInHandles);
			outHandles.AddRange(Vehicles);
			outHandles.AddRange(Peds);
            outHandles.AddRange(Pickups.Select(p => p.ObjectHandle));
			return outHandles.ToArray();
		}

		[Obsolete("Prop streaming has been disabled since the object limit is 2048.")]
		public static void MoveToMemory(Entity i)
		{
			var obj = new MapObject()
			{
				Dynamic = !StaticProps.Contains(i.Handle),
				Hash = i.Model.Hash,
				Position = i.Position,
				Quaternion = Quaternion.GetEntityQuaternion(i),
				Rotation = i.Rotation,
				Type = ObjectTypes.Prop,
			};
            MemoryObjects.Add(obj);
			StreamedInHandles.Remove(i.Handle);
			StaticProps.Remove(i.Handle);
			i.Delete();
		}

		[Obsolete("Prop streaming has been disabled since the object limit is 2048.")]
		public static void MoveFromMemory(MapObject obj)
		{
			var prop = obj;
			Prop newProp = World.CreateProp(new Model(prop.Hash), prop.Position, prop.Rotation, false, false);
			newProp.IsPositionFrozen = !prop.Dynamic;
			StreamedInHandles.Add(newProp.Handle);
			if (!prop.Dynamic)
			{
				StaticProps.Add(newProp.Handle);
				newProp.IsPositionFrozen = true;
			}
			if (prop.Quaternion != null)
				Quaternion.SetEntityQuaternion(newProp, prop.Quaternion);
			newProp.Position = prop.Position;
			MemoryObjects.Remove(prop);
		}


	    private static bool _justTeleported;
		public static void Tick()
		{
			foreach (MapObject o in RemovedObjects)
			{
				Prop returnedProp = Function.Call<Prop>(Hash.GET_CLOSEST_OBJECT_OF_TYPE, o.Position.X, o.Position.Y, o.Position.Z, 1f, o.Hash, 0);
				if (returnedProp == null || returnedProp.Handle == 0 || StreamedInHandles.Contains(returnedProp.Handle)) continue;
				returnedProp.Delete();
			}
            
            foreach (Marker marker in Markers)
			{
                if (!marker.OnlyVisibleInEditor || marker.OnlyVisibleInEditor && MapEditor.IsInFreecam)
				Function.Call(Hash.DRAW_MARKER, (int) marker.Type, marker.Position.X, marker.Position.Y, marker.Position.Z, 0f, 0f, 0f,
				 marker.Rotation.X, marker.Rotation.Y, marker.Rotation.Z, marker.Scale.X, marker.Scale.Y, marker.Scale.Z,
				 marker.Red, marker.Green, marker.Blue, marker.Alpha, marker.BobUpAndDown, marker.RotateToCamera, 2, false, false, false);

			    if (marker.TeleportTarget.HasValue && Game.Player.Character.IsInRange(marker.Position, Math.Max(2f, marker.Scale.X)) && !_justTeleported)
			    {
			        if (!Game.Player.Character.IsInVehicle())
			            Game.Player.Character.Position = marker.TeleportTarget.Value;
			        else
			            Game.Player.Character.CurrentVehicle.Position = marker.TeleportTarget.Value;
			        _justTeleported = true;
			    }
			}

		    if (_justTeleported)
		    {
		        var isInRangeOfAny = Markers.Any(m =>
		        {
		            if (!m.TeleportTarget.HasValue) return false;
		            return Game.Player.Character.IsInRange(m.Position, Math.Max(2f, m.Scale.X));
		        });

		        if (!isInRangeOfAny) _justTeleported = false;
		    }

		    foreach (DynamicPickup pickup in Pickups)
		    {
		        pickup.Update();
		    }

			/*
			if(_lastPos == Game.Player.Character.Position)
				return;
			_lastPos = Game.Player.Character.Position;

			if (PropCount < MAX_OBJECTS)
			{
				if (MemoryObjects.Count != 0)
				{
					for (int i = MemoryObjects.Count - 1; i >= 0; i--)
					{
						var prop = MemoryObjects[i];
						Prop newProp = World.CreateProp(ObjectPreview.LoadObject(prop.Hash), prop.Position, prop.Rotation, false, false);
						newProp.IsPositionFrozen = !prop.Dynamic;
						StreamedInHandles.Add(newProp.Handle);
						if (!prop.Dynamic)
						{
							StaticProps.Add(newProp.Handle);
							newProp.FreezePosition = true;
						}
						if (prop.Quaternion != null)
							Quaternion.SetEntityQuaternion(newProp, prop.Quaternion);
						MemoryObjects.Remove(prop);
					}
				}
				return;
			}
			
			MapObject[] propsToRemove = StreamedInHandles.Select(i => new MapObject()
			{
				Dynamic = !StaticProps.Contains(i), Hash = new Prop(i).Model.Hash, Position = new Prop(i).Position, Quaternion = Quaternion.GetEntityQuaternion(new Prop(i)), Rotation = new Prop(i).Rotation, Type = ObjectTypes.Prop, Id = i
			}).OrderBy(obj => (obj.Position - Game.Player.Character.Position).Length()).ToArray();

			MapObject[] propsToReAdd = MemoryObjects.OrderBy(obj => (obj.Position - Game.Player.Character.Position).Length()).ToArray();


			int lastPropToRemove = 0;
			int lastPropToReAdd = 0;
			for (int i = 0; i < MAX_OBJECTS; i++)
			{
				if (propsToReAdd.Length <= lastPropToReAdd)
				{
					lastPropToRemove = MAX_OBJECTS - lastPropToReAdd;
					break;
				}
				if (propsToRemove.Length <= lastPropToRemove)
				{
					lastPropToReAdd = MAX_OBJECTS - lastPropToRemove;
					break;
				}
				float readdLen = (propsToReAdd[lastPropToReAdd].Position - Game.Player.Character.Position).Length();
				float removeLen = (propsToRemove[lastPropToRemove].Position - Game.Player.Character.Position).Length();
				if (readdLen < removeLen)
					lastPropToReAdd++;
				else
					lastPropToRemove++;
			}

			for (var i = lastPropToRemove; i < propsToRemove.Length; i++)
			{
				MoveToMemory(new Prop(propsToRemove[i].Id));
			}
			
			for (int i = 0; i < lastPropToReAdd; i++) // Have to spawn it in
			{
				var prop = propsToReAdd[i];
				MoveFromMemory(prop);
			}
			// */
		}
	}
}