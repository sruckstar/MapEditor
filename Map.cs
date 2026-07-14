using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;

namespace MapEditor
{
	public class Map
	{
        public List<MapObject> Objects = new List<MapObject>();
        public List<MapObject> RemoveFromWorld = new List<MapObject>();
        public List<Marker> Markers = new List<Marker>();
	    public MapMetadata Metadata;
	}

    public class MapMetadata
    {
        public MapMetadata()
        {
            Creator = Game.Player.Name;
            Name = "Nameless Map";
            Description = "";
        }

        public string Creator { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Filename { get; set; }

        /// <summary>
        /// Spawns this map on startup, without it becoming the map being edited. See <see cref="AutoloadedMaps"/>.
        /// </summary>
        public bool Autoload { get; set; }

        public Vector3? LoadingPoint { get; set; }
        public Vector3? TeleportPoint { get; set; }

        /// <summary>
        /// The file a map came from says where the map lives, not what it is, and it means nothing to whoever
        /// the file is passed on to, so it is normally left out of it. <see cref="SessionRestore"/> is the one
        /// exception: its file is not a map to be handed around but the editor's own state, waiting to be handed
        /// back whole to the instance of the script that comes after a reload.
        /// </summary>
        public static bool SerializeFilename;

        public bool ShouldSerializeFilename()
        {
            return SerializeFilename;
        }
    }
}