using System;
using System.IO;
using System.Reflection;
using GTA;
using GTA.Math;
using GTA.UI;

namespace MapEditor
{
    public static class Extensions
    {
        public static string Limit(this string s, int limit)
        {
            if (s == null) return null;
            if (s.Length > limit) return s.Substring(0, limit);
            return s;
        }

        /// <summary>
        /// Replaces NativeUI's MiscExtensions.LinearVectorLerp.
        /// </summary>
        public static Vector3 LinearVectorLerp(Vector3 start, Vector3 end, int currentTime, int duration)
        {
            return new Vector3
            {
                X = LinearFloatLerp(start.X, end.X, currentTime, duration),
                Y = LinearFloatLerp(start.Y, end.Y, currentTime, duration),
                Z = LinearFloatLerp(start.Z, end.Z, currentTime, duration),
            };
        }

        public static float LinearFloatLerp(float start, float end, int currentTime, int duration)
        {
            float change = end - start;
            return change * currentTime / duration + start;
        }
    }

    /// <summary>
    /// Small shims for the ScriptHookVDotNet 2 APIs that were removed in version 3.
    /// </summary>
    public static class Compat
    {
        /// <summary>
        /// SHVDN3 dropped UI.Notify.
        /// </summary>
        public static void Notify(string message)
        {
            Notification.Show(message);
        }

        /// <summary>
        /// SHVDN3 made the Prop/Ped/Vehicle constructors internal, so a handle can no longer be
        /// wrapped with `new Prop(handle)`. Entity.FromHandle returns the correct concrete type
        /// (or null when the handle no longer refers to a live entity).
        /// </summary>
        public static Entity Ent(int handle)
        {
            return Entity.FromHandle(handle);
        }

        public static Prop PropFrom(int handle)
        {
            return Entity.FromHandle(handle) as Prop;
        }

        public static Ped PedFrom(int handle)
        {
            return Entity.FromHandle(handle) as Ped;
        }

        public static Vehicle VehicleFrom(int handle)
        {
            return Entity.FromHandle(handle) as Vehicle;
        }

        /// <summary>
        /// SHVDN3 dropped the Game.GetUserInput(maxLength) overloads that took no window title.
        /// </summary>
        public static string GetUserInput(int maxLength)
        {
            return Game.GetUserInput(WindowTitle.EnterMessage60, "", maxLength);
        }

        public static string GetUserInput(string defaultText, int maxLength)
        {
            return Game.GetUserInput(WindowTitle.EnterMessage60, defaultText ?? "", maxLength);
        }

        /// <summary>
        /// SHVDN3 dropped NativeUI's Sprite.WriteFileFromResources.
        /// </summary>
        public static string WriteFileFromResources(Assembly assembly, string resourceName, string path)
        {
            using (Stream source = assembly.GetManifestResourceStream(resourceName))
            {
                if (source == null) return path;
                using (var destination = File.Create(path))
                    source.CopyTo(destination);
            }
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// The raycast flag set the editor has always used: map + mission entities + peds + ragdolls + objects + foliage.
        /// </summary>
        public const IntersectFlags EditorIntersectFlags =
            IntersectFlags.Map | IntersectFlags.Vehicles | IntersectFlags.PedCapsules |
            IntersectFlags.Ragdolls | IntersectFlags.Objects | IntersectFlags.Foliage;
    }
}
