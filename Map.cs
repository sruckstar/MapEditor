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

        public bool ShouldSerializeFilename()
        {
            return false;
        }
    }
}