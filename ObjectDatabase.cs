using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GTA;
using GTA.Native;

namespace MapEditor
{
    public static class ObjectDatabase
    {
        public static Dictionary<string, int> MainDb;

	    public static HashSet<int> InvalidHashes = new HashSet<int>();

		/// <summary>
		/// Mirrors <see cref="Settings.OmitInvalidObjects"/>. When off, models that fail to resolve are neither
		/// flagged in the list nor remembered in InvalidObjects.ini.
		/// </summary>
		public static bool TrackInvalidObjects = true;

	    public static Dictionary<string, int> VehicleDb;

		public static Dictionary<string, int> PedDb;

		public static Dictionary<Relationship, RelationshipGroup> RelationshipDb = new Dictionary<Relationship, RelationshipGroup>();

	    public static RelationshipGroup BallasGroup;

	    public static RelationshipGroup GroveGroup;

        public enum PickupHash
        {
            Pistol = -105925489,
            CombatPistol = -1989692173,
            APPIstol = 996550793,
            MicroSMG = 496339155,
            SMG = 978070226,
            AssaultRifle = -214137936,
            CarbineRifle = -546236071,
            AdvancedRifle = -1296747938,
            SawnOffShotgun = -1766583645,
            PumpShotgun = -1456120371,
            AssaultShotgun = -1835415205,
            SniperRifle = -30788308,
            HeavySniper = 1765114797,
            MachineGun = -2050315855,
            CombatMachineGun = -1298986476,
            GrenadeLauncher = 779501861,
            RPG = 1295434569,
            Minigun = 792114228,
            Grenade = 1577485217,
            StickyBomb = 2081529176,
            Molotov = 768803961,
            PetrolCan = -962731009,
            SmokeGrenade = 483787975,
            Knife = 663586612,
            Bat = -2115084258,
            Hammer = 693539241,
            Crowbar = -2027042680,
            GolfClub = -1997886297,
            Nightstick = 1587637620,
            Parachute = 1735599485,
            Armour = 1274757841,
            Health = -1888453608,
            VehiclePistol = -1521817673,
            VehicleCombatPistol = -794112265,
            VehicleAPPistol = -863291131,
            VehicleMicroSMG = -1200951717,
            VehicleSMG = -864236261,
            VehicleSawnOffShotgun = 772217690,
            VehicleGrenade = -1491601256,
            VehicleMolotov = -2066319660,
            VehicleSmokeGrenade = 1705498857,
            VehicleStickyGrenade = 746606563,
            VehicleHealth = 160266735,
            VehicleArmour = 1125567497,
            MoneyCase = -831529621,
            MoneyBag = 545862290,
            MoneyMediumBag = 341217064,
            MoneyPaperBag = 1897726628,
            CrateUnfixed = 1852930709,
            Package = -2136239332,
            BulletAmmo = 1426343849,
            MissleAmmo = -107080240,
            Camera = -482507216,
            Snack = 483577702,
            Purse = 513448440,
            SecurityCase = -562499202,
            Money = -31919185,
            Wallet = 1575005502,
        }

        internal static void LoadEnumDatabases()
	    {
			VehicleDb = new Dictionary<string, int>();
			PedDb = new Dictionary<string, int>();

		    // Vehicles.
		    foreach (string veh in Enum.GetNames(typeof(VehicleHash)))
		    {
			    VehicleHash hash;
			    Enum.TryParse(veh, out hash);
				if(VehicleDb.ContainsKey(veh)) continue;
				VehicleDb.Add(veh, (int)hash);
		    }

			// Peds
			foreach (string ped in Enum.GetNames(typeof(PedHash)))
			{
				PedHash hash;
				Enum.TryParse(ped, out hash);
				if (PedDb.ContainsKey(ped)) continue;
				PedDb.Add(ped, (int)hash);
			}

            WriteEnumDatabase();
		}

        internal static void WriteEnumDatabase()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var pair in PedDb)
            {
                sb.Append(pair.Key + "=" + pair.Value + Environment.NewLine);
            }
            File.WriteAllText("scripts\\PedList.ini", sb.ToString());

            sb = new StringBuilder();

            foreach (var pair in VehicleDb)
            {
                sb.Append(pair.Key + "=" + pair.Value + Environment.NewLine);
            }
            File.WriteAllText("scripts\\VehicleList.ini", sb.ToString());
        }

		internal static void LoadFromFile(string path, ref Dictionary<string, int> dictToLoadto)
        {
            dictToLoadto = new Dictionary<string, int>();
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                string[] s = line.Split('=');
                if(dictToLoadto.ContainsKey(s[0])) continue;

                if (s.Length == 1)
                {
                    dictToLoadto.Add(s[0], new Model(s[0]).Hash);
                }
                else
                {
                    int val = Convert.ToInt32(s[1], CultureInfo.InvariantCulture);
                    dictToLoadto.Add(s[0], val);
                }
            }
        }

	    internal static void SetupRelationships()
	    {
		    foreach (var s in Enum.GetNames(typeof(Relationship)))
		    {
			    var hash = (Relationship) Enum.Parse(typeof(Relationship), s);
			    var group = World.AddRelationshipGroup("MAPEDITOR_" + s);
				// SHVDN3 moved this off World; the last argument makes the relationship bidirectional.
				group.SetRelationshipBetweenGroups(Game.Player.Character.RelationshipGroup, hash, true);
				RelationshipDb.Add(hash, group);
		    }

		    BallasGroup = World.AddRelationshipGroup("MAPEDITOR_BALLAS");
		    GroveGroup = World.AddRelationshipGroup("MAPEDITOR_GROVE");

			BallasGroup.SetRelationshipBetweenGroups(GroveGroup, Relationship.Hate, true);
		}

		private const string InvalidObjectsPath = "scripts\\InvalidObjects.ini";

		internal static void LoadInvalidHashes()
	    {
			if(!File.Exists(InvalidObjectsPath)) return;
		    string[] lines = File.ReadAllLines(InvalidObjectsPath);
		    foreach (string line in lines)
		    {
			    int val;
			    // A single blank or malformed line used to throw out of the script's constructor.
			    if (!int.TryParse(line.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val)) continue;
				InvalidHashes.Add(val);
		    }
	    }

		internal static void SaveInvalidHashes()
	    {
		    File.WriteAllText(InvalidObjectsPath, string.Join("\r\n", InvalidHashes.Select(hash => hash.ToString(CultureInfo.InvariantCulture))));
	    }

		/// <summary>
		/// Remembers that a model could not be resolved, so that browsing the list does not stall on it again.
		/// </summary>
		internal static void MarkHashInvalid(int hash)
		{
			if (!TrackInvalidObjects) return;
			if (!InvalidHashes.Add(hash)) return;
			SaveInvalidHashes();
		}

		/// <summary>
		/// Clears a stale blacklist entry once the model has been proven to load after all. Entries can outlive
		/// the reason they were added (a game update or a DLC that ships the model), so they must be revocable.
		/// </summary>
		internal static void MarkHashValid(int hash)
		{
			if (!InvalidHashes.Remove(hash)) return;
			SaveInvalidHashes();
		}

		internal static void ClearInvalidHashes()
		{
			InvalidHashes.Clear();
			if (File.Exists(InvalidObjectsPath))
				File.Delete(InvalidObjectsPath);
		}

	    internal static void SetPedRelationshipGroup(Ped ped, string group)
	    {
		    if (group == "Ballas")
		    {
			    ped.RelationshipGroup = BallasGroup;
				return;
		    }
		    if (group == "Grove")
		    {
			    ped.RelationshipGroup = GroveGroup;
				return;
		    }
		    Relationship outHash;
			if(!Enum.TryParse(group, out outHash)) return;
		    ped.RelationshipGroup = RelationshipDb[outHash];
	    }

		internal static Dictionary<string, string> ScrenarioDatabase = new Dictionary<string, string>
		{
			{"Drink Coffee",  "WORLD_HUMAN_AA_COFFEE"},
			{"Smoke", "WORLD_HUMAN_AA_SMOKE" },
			{"Smoke 2", "WORLD_HUMAN_SMOKING" },
			{"Binoculars",  "WORLD_HUMAN_BINOCULARS"},
			{"Bum", "WORLD_HUMAN_BUM_FREEWAY" },
			{"Cheering", "WORLD_HUMAN_CHEERING" },
			{"Clipboard", "WORLD_HUMAN_CLIPBOARD" },
			{"Drilling",  "WORLD_HUMAN_CONST_DRILL"},
			{"Drinking", "WORLD_HUMAN_DRINKING" },
			{"Drug Dealer", "WORLD_HUMAN_DRUG_DEALER"},
			{"Drug Dealer Hard", "WORLD_HUMAN_DRUG_DEALER_HARD" },
			{"Traffic Signaling",  "WORLD_HUMAN_CAR_PARK_ATTENDANT"},
			{"Filming", "WORLD_HUMAN_MOBILE_FILM_SHOCKING" },
			{"Leaf Blower", "WORLD_HUMAN_GARDENER_LEAF_BLOWER" },
			{"Golf Player", "WORLD_HUMAN_GOLF_PLAYER" },
			{"Guard Patrol", "WORLD_HUMAN_GUARD_PATROL" },
			{"Hammering", "WORLD_HUMAN_HAMMERING" },
			{"Janitor", "WORLD_HUMAN_JANITOR" },
			{"Musician", "WORLD_HUMAN_MUSICIAN" },
			{"Paparazzi", "WORLD_HUMAN_PAPARAZZI" },
			{"Party", "WORLD_HUMAN_PARTYING" },
			{"Picnic", "WORLD_HUMAN_PICNIC" },
			{"Push Ups", "WORLD_HUMAN_PUSH_UPS"},
			{"Shine Torch", "WORLD_HUMAN_SECURITY_SHINE_TORCH" },
			{"Sunbathe", "WORLD_HUMAN_SUNBATHE" },
			{"Sunbathe Back", "WORLD_HUMAN_SUNBATHE_BACK"},
			{"Tourist", "WORLD_HUMAN_TOURIST_MAP" },
			{"Mechanic", "WORLD_HUMAN_VEHICLE_MECHANIC" },
			{"Welding", "WORLD_HUMAN_WELDING" },
			{"Yoga", "WORLD_HUMAN_YOGA" },
		};
    }
}