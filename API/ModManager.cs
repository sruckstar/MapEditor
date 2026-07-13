using System.Collections.Generic;
using LemonUI.Menus;

namespace MapEditor.API
{
	public delegate void MapSavedEvent(Map currentMap, string filename);
	public delegate void ModSelectedEvent();

	public delegate void ModDisconnectedEvent();

	public static class ModManager
	{
		internal static List<ModListener> Mods = new List<ModListener>();
		internal static ModListener CurrentMod;
	    internal static NativeMenu ModMenu;

		internal static void InitMenu()
		{
		    ModMenu = new NativeMenu("Map Editor", "~b~" + Translation.Translate("EXTERNAL MODS"));

            ModMenu.Buttons.Visible = false;
			ModMenu.ItemActivated += (menu, e) =>
			{
				var index = ModMenu.Items.IndexOf(e.Item);
				if (index < 0 || index >= Mods.Count) return;

				var tmpMod = Mods[index];
				if (CurrentMod == tmpMod)
				{
					Compat.Notify("~b~~h~Map Editor~h~~n~~w~Mod ~h~" + tmpMod.Name + "~h~ " + Translation.Translate("has been disconnected."));
					CurrentMod.ModDisconnectInvoker();
					CurrentMod = null;
				}
				else
				{
					Compat.Notify("~b~~h~Map Editor~h~~n~~w~Mod ~h~" + tmpMod.Name + "~h~ " + Translation.Translate("has been connected."));
					tmpMod.ModSelectInvoker();
					CurrentMod = tmpMod;
				}
			};
		}

		public static void SuscribeMod(ModListener mod)
		{
			Mods.Add(mod);
			ModMenu.Add(new NativeItem(mod.ButtonString, mod.Description));
		}
	}

	public class ModListener
	{
		public event MapSavedEvent OnMapSaved;
		public event ModSelectedEvent OnModSelect;
		public event ModDisconnectedEvent OnModDisconnect;

		public string Name;
		public string Description;
		public string ButtonString;

		internal void MapSavedInvoker(Map map, string filename)
		{
			OnMapSaved?.Invoke(map, filename);
		}

		internal void ModSelectInvoker()
		{
			OnModSelect?.Invoke();
		}

		internal void ModDisconnectInvoker()
		{
			OnModDisconnect?.Invoke();
		}
	}
}
