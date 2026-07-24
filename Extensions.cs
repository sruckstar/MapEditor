using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using GTA;
using GTA.Math;
using GTA.Native;
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
            return GetUserInput("", maxLength);
        }

        /// <summary>
        /// Drop-in replacement for Game.GetUserInput that additionally supports pasting from the
        /// Windows clipboard with Ctrl+V. The stock on-screen keyboard ignores Ctrl+V, so we run
        /// the keyboard loop ourselves: while it is open we watch for Ctrl+V, and when it is pressed
        /// we read the clipboard and re-open the keyboard pre-filled with the pasted text appended.
        /// </summary>
        public static string GetUserInput(string defaultText, int maxLength)
        {
            // Matches SHVDN's Game.GetUserInput: "EnterMessage60" is the GXT key for the
            // "Enter Message (MAX 60 characters):" title, and key events are paused while typing so
            // the keystrokes don't leak into the script's own hotkey handlers.
            const string windowTitle = "EnterMessage60";
            string initialText = (defaultText ?? "").Limit(maxLength);
            bool pasteWasDown = false;

            PauseKeyEvents(true);
            try
            {
                while (true)
                {
                    Function.Call(Hash.DISPLAY_ONSCREEN_KEYBOARD, 1, windowTitle, "",
                        initialText, "", "", "", maxLength + 1);

                    bool reopen = false;
                    while (!reopen)
                    {
                        Script.Yield();

                        int status = Function.Call<int>(Hash.UPDATE_ONSCREEN_KEYBOARD);
                        if (status != 0) // 1 = accepted, 2 = cancelled
                        {
                            return Function.Call<string>(Hash.GET_ONSCREEN_KEYBOARD_RESULT);
                        }

                        bool pasteDown = IsPasteShortcutDown();
                        if (pasteDown && !pasteWasDown)
                        {
                            string clip = GetClipboardText();
                            if (!string.IsNullOrEmpty(clip))
                            {
                                // The game doesn't expose the live in-progress text while the keyboard
                                // is open, so we can't reliably append to whatever was typed/deleted by
                                // hand. Replacing the field with the clipboard is predictable and avoids
                                // the "pastes twice" bug that appending to a stale value caused.
                                initialText = clip.Limit(maxLength);
                                reopen = true;
                            }
                        }
                        pasteWasDown = pasteDown;
                    }
                }
            }
            finally
            {
                PauseKeyEvents(false);
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_CONTROL = 0x11;
        private const int VK_V = 0x56;

        private static bool IsPasteShortcutDown()
        {
            bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool v = (GetAsyncKeyState(VK_V) & 0x8000) != 0;
            return ctrl && v;
        }

        /// <summary>
        /// Reads clipboard text on a dedicated STA thread (WinForms Clipboard requires STA, and the
        /// SHVDN script thread is not guaranteed to be one). Newlines are flattened to spaces because
        /// the on-screen keyboard is single-line. Returns an empty string on any failure.
        /// </summary>
        private static string GetClipboardText()
        {
            string result = "";
            try
            {
                var t = new Thread(() =>
                {
                    try
                    {
                        if (System.Windows.Forms.Clipboard.ContainsText())
                            result = System.Windows.Forms.Clipboard.GetText();
                    }
                    catch { /* clipboard unavailable / locked */ }
                });
                t.SetApartmentState(ApartmentState.STA);
                t.IsBackground = true;
                t.Start();
                t.Join();
            }
            catch { /* thread setup failed */ }

            return (result ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        }

        /// <summary>
        /// Toggles SHVDN's key-event dispatch, mirroring what the built-in Game.GetUserInput does so
        /// keystrokes typed into the keyboard don't fire the script's KeyDown handlers afterwards.
        /// Uses reflection because the core type lives in the ScriptHookVDotNet.asi assembly; if it
        /// can't be reached the input still works, keys just aren't suppressed.
        /// </summary>
        private static void PauseKeyEvents(bool pause)
        {
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("SHVDN.ScriptDomain");
                    if (t == null) continue;

                    object domain = t.GetProperty("CurrentDomain",
                        BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (domain == null) return;

                    t.GetMethod("PauseKeyEvents")?.Invoke(domain, new object[] { pause });
                    return;
                }
            }
            catch { /* internal API changed; input still works without pausing */ }
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
