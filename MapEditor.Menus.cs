using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI.Menus;

namespace MapEditor
{
    public partial class MapEditor
    {
        /// <summary>
        /// Badges replacing NativeUI's BadgeStyle.Franklin / BadgeStyle.Alert.
        /// </summary>
        private static readonly BadgeSet FolderBadge = new BadgeSet("commonmenu", "shop_franklin_icon_a", "shop_franklin_icon_b");
        private static readonly BadgeSet AlertBadge = new BadgeSet("commonmenu", "mp_alerttriangle", "mp_alerttriangle");

        private enum EntityMenuKind
        {
            Entity,
            World,
            Marker,
            Pickup,
        }

        /// <summary>
        /// Identifies what a "Current Entities" row points at. The old code smuggled this
        /// through the item Description, which LemonUI renders to the player.
        /// </summary>
        private class EntityMenuTag
        {
            public EntityMenuKind Kind;
            public int Id;          // entity handle, marker id or pickup UID
            public string WorldId;  // MapObject.Id, for props removed from the world
        }

        private void BuildSettingsMenu()
        {
            var possibleLangauges = new List<string> { "Auto" };
            possibleLangauges.AddRange(Translation.Translations.Select(t => t.Language).ToList());

            var language = new NativeListItem<string>(Translation.Translate("Language"), possibleLangauges.ToArray())
            {
                SelectedIndex = Math.Max(0, possibleLangauges.IndexOf(_settings.Translation)),
            };
            language.ItemChanged += (sender, e) =>
            {
                var newLanguage = e.Object;
                Translation.SetLanguage(newLanguage);
                _settings.Translation = newLanguage;
                SaveSettings();
                if (newLanguage == "Auto")
                {
                    language.Description = "Use your game's language settings.";
                    return;
                }
                var descFile = Translation.Translations.FirstOrDefault(t => t.Language == newLanguage);
                if (descFile == null) return;
                language.Description = "~h~" + Translation.Translate("Translator") + ":~h~ " + descFile.Translator;
            };

            var crosshairNames = Enum.GetNames(typeof(CrosshairType));
            var checkem = new NativeListItem<string>(Translation.Translate("Marker"), crosshairNames)
            {
                SelectedIndex = Math.Max(0, crosshairNames.ToList().FindIndex(x => x == _settings.CrosshairType.ToString())),
            };
            checkem.ItemChanged += (sender, e) =>
            {
                CrosshairType outHash;
                Enum.TryParse(e.Object, out outHash);
                _settings.CrosshairType = outHash;
                SaveSettings();
            };

            var disableText = Translation.Translate("Disable");
            var autosaveList = new List<string> { disableText };
            for (int i = 5; i <= 60; i += 5)
                autosaveList.Add(i.ToString(CultureInfo.InvariantCulture));

            int aIndex = autosaveList.IndexOf(_settings.AutosaveInterval.ToString(CultureInfo.InvariantCulture));
            if (aIndex == -1) aIndex = 0;

            var autosaveItem = new NativeListItem<string>(Translation.Translate("Autosave Interval"),
                Translation.Translate("Interval in minutes between automatic autosaves."), autosaveList.ToArray())
            {
                SelectedIndex = aIndex,
            };
            autosaveItem.ItemChanged += (sender, e) =>
            {
                _settings.AutosaveInterval = e.Object == disableText ? -1 : Convert.ToInt32(e.Object, CultureInfo.InvariantCulture);
                SaveSettings();
            };

            var defaultText = Translation.Translate("Default");
            var possibleDrawDistances = new List<string> { defaultText, "50", "75" };
            for (int i = 100; i <= 3000; i += 100)
                possibleDrawDistances.Add(i.ToString(CultureInfo.InvariantCulture));

            int dIndex = possibleDrawDistances.IndexOf(_settings.DrawDistance.ToString(CultureInfo.InvariantCulture));
            if (dIndex == -1) dIndex = 0;

            var drawDistanceItem = new NativeListItem<string>(Translation.Translate("Draw Distance"),
                Translation.Translate("Draw distance for props, vehicles and peds. Reload the map for changes to take effect."),
                possibleDrawDistances.ToArray())
            {
                SelectedIndex = dIndex,
            };
            drawDistanceItem.ItemChanged += (sender, e) =>
            {
                _settings.DrawDistance = e.Object == defaultText ? -1 : Convert.ToInt32(e.Object, CultureInfo.InvariantCulture);
                SaveSettings();
            };

            var senslist = Enumerable.Range(1, 59).ToArray();

            var gamboy = new NativeListItem<int>(Translation.Translate("Mouse Camera Sensitivity"), senslist)
            {
                SelectedIndex = ClampIndex(_settings.CameraSensivity - 1, senslist.Length),
            };
            gamboy.ItemChanged += (sender, e) =>
            {
                _settings.CameraSensivity = e.Object;
                SaveSettings();
            };

            var gampadSens = new NativeListItem<int>(Translation.Translate("Gamepad Camera Sensitivity"), senslist)
            {
                SelectedIndex = ClampIndex(_settings.GamepadCameraSensitivity - 1, senslist.Length),
            };
            gampadSens.ItemChanged += (sender, e) =>
            {
                _settings.GamepadCameraSensitivity = e.Object;
                SaveSettings();
            };

            var keymovesens = new NativeListItem<int>(Translation.Translate("Keyboard Movement Sensitivity"), senslist)
            {
                SelectedIndex = ClampIndex(_settings.KeyboardMovementSensitivity - 1, senslist.Length),
            };
            keymovesens.ItemChanged += (sender, e) =>
            {
                _settings.KeyboardMovementSensitivity = e.Object;
                SaveSettings();
            };

            var gammovesens = new NativeListItem<int>(Translation.Translate("Gamepad Movement Sensitivity"), senslist)
            {
                SelectedIndex = ClampIndex(_settings.GamepadMovementSensitivity - 1, senslist.Length),
            };
            gammovesens.ItemChanged += (sender, e) =>
            {
                _settings.GamepadMovementSensitivity = e.Object;
                SaveSettings();
            };

            var butts = new NativeCheckboxItem(Translation.Translate("Instructional Buttons"), _settings.InstructionalButtons);
            butts.CheckboxChanged += (sender, e) =>
            {
                _settings.InstructionalButtons = butts.Checked;
                SaveSettings();
            };

            var gamepadItem = new NativeCheckboxItem(Translation.Translate("Enable Gamepad Shortcut"), _settings.Gamepad);
            gamepadItem.CheckboxChanged += (sender, e) =>
            {
                _settings.Gamepad = gamepadItem.Checked;
                SaveSettings();
            };

            var counterItem = new NativeCheckboxItem(Translation.Translate("Entity Counter"), _settings.PropCounterDisplay);
            counterItem.CheckboxChanged += (sender, e) =>
            {
                _settings.PropCounterDisplay = counterItem.Checked;
                SaveSettings();
            };

            var snapper = new NativeCheckboxItem(Translation.Translate("Follow Object With Camera"), _settings.SnapCameraToSelectedObject);
            snapper.CheckboxChanged += (sender, e) =>
            {
                _settings.SnapCameraToSelectedObject = snapper.Checked;
                SaveSettings();
            };

            var boundItem = new NativeCheckboxItem(Translation.Translate("Bounding Box"), _settings.BoundingBox.GetValueOrDefault(false));
            boundItem.CheckboxChanged += (sender, e) =>
            {
                _settings.BoundingBox = boundItem.Checked;
                SaveSettings();
            };

            var scriptItem = new NativeCheckboxItem(Translation.Translate("Execute Scripts"), _settings.LoadScripts);
            scriptItem.CheckboxChanged += (sender, e) =>
            {
                _settings.LoadScripts = scriptItem.Checked;
                SaveSettings();
            };

            var objectValidationItem = new NativeCheckboxItem(Translation.Translate("Skip Invalid Objects"), _settings.OmitInvalidObjects);
            objectValidationItem.CheckboxChanged += (sender, e) =>
            {
                _settings.OmitInvalidObjects = objectValidationItem.Checked;
                SaveSettings();
            };

            var validate = new NativeItem(Translation.Translate("Validate Object Database"), Translation.Translate(
                "This will update the current object database, removing any invalid objects. The changes will take effect after you restart the script." +
                " It will take a couple of minutes."));
            validate.Activated += (men, item) => ValidateDatabase();

            var resetGrps = new NativeItem(Translation.Translate("Reset Active Relationship Groups"),
                Translation.Translate("This will set all ped's relationship groups to Companion."));
            resetGrps.Activated += (men, item) =>
            {
                PropStreamer.Peds.ForEach(handle =>
                {
                    var ped = Compat.PedFrom(handle);
                    if (ped != null) ObjectDatabase.SetPedRelationshipGroup(ped, "Companion");
                });
            };

#if DEBUG
            var testItem = new NativeItem("Load Terrain");
            testItem.Activated += (sender, item) =>
            {
                if (!Game.IsWaypointActive)
                {
                    Function.Call(Hash.CLEAR_HD_AREA);
                    return;
                }
                var wpyPos = World.WaypointPosition;
                Function.Call(Hash.SET_HD_AREA, wpyPos.X, wpyPos.Y, wpyPos.Z, 400f);
            };
            _settingsMenu.Add(testItem);
#endif

            _settingsMenu.Add(language);
            _settingsMenu.Add(gamepadItem);
            _settingsMenu.Add(drawDistanceItem);
            _settingsMenu.Add(autosaveItem);
            _settingsMenu.Add(checkem);
            _settingsMenu.Add(boundItem);
            _settingsMenu.Add(gamboy);
            _settingsMenu.Add(gampadSens);
            _settingsMenu.Add(keymovesens);
            _settingsMenu.Add(gammovesens);
            _settingsMenu.Add(butts);
            _settingsMenu.Add(counterItem);
            _settingsMenu.Add(snapper);
            _settingsMenu.Add(scriptItem);
            _settingsMenu.Add(objectValidationItem);
            _settingsMenu.Add(validate);
            _settingsMenu.Add(resetGrps);
            _settingsMenu.SelectedIndex = 0;
            _settingsMenu.Buttons.Visible = false;
        }

        private static int ClampIndex(int index, int count)
        {
            if (index < 0) return 0;
            if (index >= count) return count - 1;
            return index;
        }

        private void OnIndexChange(object sender, SelectedEventArgs e)
        {
            var menu = (NativeMenu) sender;
            OnIndexChange(menu, e);
        }

        private void OnIndexChange(NativeMenu sender, SelectedEventArgs e)
        {
            int index = e.Index;
            if (index < 0 || index >= sender.Items.Count) return;

            int requestedHash;
            var title = sender.Items[index].Title;
            switch (_currentObjectType)
            {
                case ObjectTypes.Prop:
                    if (!ObjectDatabase.MainDb.TryGetValue(title, out requestedHash)) return;
                    break;
                case ObjectTypes.Vehicle:
                    if (!ObjectDatabase.VehicleDb.TryGetValue(title, out requestedHash)) return;
                    break;
                case ObjectTypes.Ped:
                    if (!ObjectDatabase.PedDb.TryGetValue(title, out requestedHash)) return;
                    break;
                default:
                    return;
            }

            if ((_previewProp == null || _previewProp.Model.Hash != requestedHash) &&
                ((!ObjectDatabase.InvalidHashes.Contains(requestedHash) && _settings.OmitInvalidObjects) || !_settings.OmitInvalidObjects))
            {
                _previewProp?.Delete();
                _previewProp = null;

                Model tmpModel = ObjectPreview.LoadObject(requestedHash);
                if (tmpModel == null)
                {
                    sender.Items[index].AltTitle = "~r~Invalid";
                    return;
                }

                switch (_currentObjectType)
                {
                    case ObjectTypes.Prop:
                        _previewProp = World.CreateProp(tmpModel, _objectPreviewPos, false, false);
                        break;
                    case ObjectTypes.Vehicle:
                        _previewProp = World.CreateVehicle(tmpModel, _objectPreviewPos);
                        break;
                    case ObjectTypes.Ped:
                        _previewProp = World.CreatePed(tmpModel, _objectPreviewPos);
                        break;
                }

                if (_previewProp != null)
                {
                    _previewProp.IsPositionFrozen = true;
                    _previewProp.Rotation = new Vector3(0, 0, 180f);
                    if (_previewProp.Model.IsPed)
                        _previewProp.Heading = 180f;
                }

                tmpModel.MarkAsNoLongerNeeded();
            }
        }

        private void OnObjectSelect(object sender, ItemActivatedArgs e)
        {
            var menu = (NativeMenu) sender;

            if (PropStreamer.EntityCount == 0)
                _lastAutosave = DateTime.Now;

            _quitWithSearchVisible = _searchMenu.Visible;

            var title = e.Item.Title;
            int objectHash;

            switch (_currentObjectType)
            {
                case ObjectTypes.Prop:
                    if (!ObjectDatabase.MainDb.TryGetValue(title, out objectHash)) return;
                    AddItemToEntityMenu(_snappedProp = PropStreamer.CreateProp(ObjectPreview.LoadObject(objectHash),
                        VectorExtensions.RaycastEverything(new Vector2(0f, 0f)), new Vector3(0, 0, 0), false, force: true,
                        drawDistance: _settings.DrawDistance));
                    break;
                case ObjectTypes.Vehicle:
                    if (!ObjectDatabase.VehicleDb.TryGetValue(title, out objectHash)) return;
                    AddItemToEntityMenu(_snappedProp = PropStreamer.CreateVehicle(ObjectPreview.LoadObject(objectHash),
                        VectorExtensions.RaycastEverything(new Vector2(0f, 0f)), 0f, true, drawDistance: _settings.DrawDistance));
                    break;
                case ObjectTypes.Ped:
                    if (!ObjectDatabase.PedDb.TryGetValue(title, out objectHash)) return;
                    AddItemToEntityMenu(_snappedProp = PropStreamer.CreatePed(ObjectPreview.LoadObject(objectHash),
                        VectorExtensions.RaycastEverything(new Vector2(0f, 0f)), 0f, true, drawDistance: _settings.DrawDistance));
                    if (_snappedProp != null)
                    {
                        PropStreamer.ActiveScenarios.Add(_snappedProp.Handle, "None");
                        PropStreamer.ActiveRelationships.Add(_snappedProp.Handle, DefaultRelationship.ToString());
                        PropStreamer.ActiveWeapons.Add(_snappedProp.Handle, WeaponHash.Unarmed);
                    }
                    break;
            }

            _isChoosingObject = false;
            _searchResultsOn = false;
            SetMenuVisible(_objectsMenu, false);
            SetMenuVisible(_searchMenu, false);
            _previewProp?.Delete();
            _previewProp = null;
            _changesMade++;
        }

        private void RedrawObjectsMenu(ObjectTypes type = ObjectTypes.Prop)
        {
            _objectsMenu.Clear();
            switch (type)
            {
                case ObjectTypes.Prop:
                    foreach (var u in ObjectDatabase.MainDb)
                    {
                        var object1 = new NativeItem(u.Key);
                        if (ObjectDatabase.InvalidHashes.Contains(u.Value))
                            object1.AltTitle = "~r~Invalid";
                        _objectsMenu.Add(object1);
                    }
                    break;
                case ObjectTypes.Vehicle:
                    foreach (var u in ObjectDatabase.VehicleDb)
                        _objectsMenu.Add(new NativeItem(u.Key));
                    break;
                case ObjectTypes.Ped:
                    foreach (var u in ObjectDatabase.PedDb)
                        _objectsMenu.Add(new NativeItem(u.Key));
                    break;
            }
            if (_objectsMenu.Items.Count > 0)
                _objectsMenu.SelectedIndex = 0;
        }

        private bool ApplySearchQuery(string searchQuery, string modelName)
        {
            var q = searchQuery.ToLower();
            if (q.Contains(" or "))
            {
                var queries = Regex.Split(q, "\\s+or\\s+");
                return queries.Aggregate(false, (current, query) => current || ApplySearchQuery(query, modelName));
            }

            if (q.Contains(" and "))
            {
                var queries = Regex.Split(q, "\\s+and\\s+");
                return queries.Aggregate(true, (current, query) => current && ApplySearchQuery(query, modelName));
            }

            return modelName.ToLower().Contains(q);
        }

        private void RedrawSearchMenu(string searchQuery, ObjectTypes type = ObjectTypes.Prop)
        {
            _searchMenu.Clear();

            switch (type)
            {
                case ObjectTypes.Prop:
                    foreach (var u in ObjectDatabase.MainDb.Where(pair => ApplySearchQuery(searchQuery, pair.Key)))
                    {
                        var object1 = new NativeItem(u.Key);
                        if (ObjectDatabase.InvalidHashes.Contains(u.Value))
                            object1.AltTitle = "~r~Invalid";
                        _searchMenu.Add(object1);
                    }
                    break;
                case ObjectTypes.Vehicle:
                    foreach (var u in ObjectDatabase.VehicleDb.Where(pair => ApplySearchQuery(searchQuery, pair.Key)))
                        _searchMenu.Add(new NativeItem(u.Key));
                    break;
                case ObjectTypes.Ped:
                    foreach (var u in ObjectDatabase.PedDb.Where(pair => ApplySearchQuery(searchQuery, pair.Key)))
                        _searchMenu.Add(new NativeItem(u.Key));
                    break;
            }
            if (_searchMenu.Items.Count > 0)
                _searchMenu.SelectedIndex = 0;
        }

        private string GetSafeShortReverseString(string input, int limit)
        {
            if (input == null) return null;
            if (input.Length > limit)
                return "..." + input.Substring(input.Length - limit, limit);
            return input;
        }

        private string GetSafeShortString(string input, int limit)
        {
            if (input == null) return null;
            if (input.Length > limit)
                return input.Substring(0, limit) + "...";
            return input;
        }

        private void RedrawFilepickerMenu(string folder = null)
        {
            if (folder == null) folder = Directory.GetCurrentDirectory();
            _filepicker.Clear();
            _filepicker.Name = "~b~" + GetSafeShortReverseString(folder, 30);

            var backup = new NativeItem("..");
            backup.LeftBadgeSet = FolderBadge;
            backup.Activated += (sender, item) =>
            {
                RedrawFilepickerMenu(Directory.GetParent(folder).ToString());
            };

            if (Directory.GetParent(folder) == null)
                backup.Enabled = false;

            _filepicker.Add(backup);

            foreach (var directory in Directory.EnumerateDirectories(folder))
            {
                var dirItem = new NativeItem(GetSafeShortString(Path.GetFileName(directory), 40));
                dirItem.LeftBadgeSet = FolderBadge;
                dirItem.Activated += (sender, item) =>
                {
                    RedrawFilepickerMenu(directory);
                };

                _filepicker.Add(dirItem);
            }

            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var item = new NativeItem(GetSafeShortString(Path.GetFileName(file), 40));

                MapSerializer.Format mapFormat;
                string description = "";

                if (file.EndsWith(".ini"))
                {
                    mapFormat = MapSerializer.Format.SimpleTrainer;
                }
                else if (file.EndsWith(".SP00N"))
                {
                    mapFormat = MapSerializer.Format.SpoonerLegacy;
                }
                else if (file.EndsWith(".xml"))
                {
                    mapFormat = MapSerializer.Format.NormalXml;
                    Map map = null;

                    try
                    {
                        var ser = new XmlSerializer(typeof(Map));
                        using (var stream = File.OpenRead(file))
                            map = (Map) ser.Deserialize(stream);
                    }
                    catch (Exception) {}

                    if (map == null)
                    {
                        try
                        {
                            var spReader = new XmlSerializer(typeof(MenyooCompatibility.SpoonerPlacements));
                            MenyooCompatibility.SpoonerPlacements newMap;
                            using (var stream = File.OpenRead(file))
                                newMap = (MenyooCompatibility.SpoonerPlacements)spReader.Deserialize(stream);

                            if (newMap != null)
                            {
                                description = "~h~Format:~h~ Menyoo Trainer";
                                mapFormat = MapSerializer.Format.Menyoo;
                            }
                        }
                        catch (Exception) { }
                    }

                    if (map?.Metadata != null)
                    {
                        description = "~h~Format:~h~ Map Editor\n~h~Name:~h~ " + map.Metadata.Name + "\n~h~Author:~h~ " +
                                      map.Metadata.Creator + "\n~h~Description:~h~ " + map.Metadata.Description;
                    }
                }
                else
                {
                    continue;
                }

                item.Description = description;

                var chosenFormat = mapFormat;
                item.Activated += (sender, selectedItem) =>
                {
                    SetMenuVisible(_filepicker, false);
                    LoadMap(file, chosenFormat);
                };

                _filepicker.Add(item);
            }

            if (_filepicker.Items.Count > 0)
                _filepicker.SelectedIndex = 0;
        }

        private void RedrawMetadataMenu()
        {
            _metadataMenu.Clear();

            var saveItem = new NativeItem(Translation.Translate("Save Map"));

            saveItem.Activated += (sender, item) =>
            {
                SaveMap(PropStreamer.CurrentMapMetadata.Filename, MapSerializer.Format.NormalXml);
                SetMenuVisible(_metadataMenu, false);
            };

            if (string.IsNullOrWhiteSpace(PropStreamer.CurrentMapMetadata.Filename))
                saveItem.Enabled = false;

            {
                var filenameItem = new NativeItem(Translation.Translate("File Path"));

                if (string.IsNullOrWhiteSpace(PropStreamer.CurrentMapMetadata.Filename))
                    filenameItem.RightBadgeSet = AlertBadge;
                else
                    filenameItem.AltTitle = GetSafeShortReverseString(PropStreamer.CurrentMapMetadata.Filename, 20);

                filenameItem.Activated += (sender, item) =>
                {
                    var newName = Compat.GetUserInput(PropStreamer.CurrentMapMetadata.Filename ?? "", 255);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    if (!newName.EndsWith(".xml")) newName += ".xml";
                    PropStreamer.CurrentMapMetadata.Filename = newName;
                    saveItem.Enabled = true;

                    filenameItem.RightBadgeSet = null;
                    filenameItem.AltTitle = GetSafeShortReverseString(newName, 20);
                };

                _metadataMenu.Add(filenameItem);
            }

            {
                var nameItem = new NativeItem(Translation.Translate("Map Name"));

                if (!string.IsNullOrWhiteSpace(PropStreamer.CurrentMapMetadata.Name))
                    nameItem.AltTitle = GetSafeShortString(PropStreamer.CurrentMapMetadata.Name, 20);

                nameItem.Activated += (sender, item) =>
                {
                    var newName = Compat.GetUserInput(PropStreamer.CurrentMapMetadata.Name ?? "", 30);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    PropStreamer.CurrentMapMetadata.Name = newName;
                    nameItem.AltTitle = GetSafeShortString(newName, 20);
                };

                _metadataMenu.Add(nameItem);
            }

            {
                var authorItem = new NativeItem(Translation.Translate("Author"));

                if (!string.IsNullOrWhiteSpace(PropStreamer.CurrentMapMetadata.Creator))
                    authorItem.AltTitle = GetSafeShortString(PropStreamer.CurrentMapMetadata.Creator, 20);

                authorItem.Activated += (sender, item) =>
                {
                    var newName = Compat.GetUserInput(PropStreamer.CurrentMapMetadata.Creator ?? "", 30);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    PropStreamer.CurrentMapMetadata.Creator = newName;
                    authorItem.AltTitle = GetSafeShortString(newName, 20);
                };

                _metadataMenu.Add(authorItem);
            }

            {
                var descItem = new NativeItem(Translation.Translate("Description"));

                if (!string.IsNullOrWhiteSpace(PropStreamer.CurrentMapMetadata.Description))
                    descItem.Description = PropStreamer.CurrentMapMetadata.Description;

                descItem.Activated += (sender, item) =>
                {
                    var newName = Compat.GetUserInput(PropStreamer.CurrentMapMetadata.Description ?? "", 255);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    PropStreamer.CurrentMapMetadata.Description = newName;
                    descItem.Description = newName;
                };

                _metadataMenu.Add(descItem);
            }

            _metadataMenu.Add(saveItem);

            _metadataMenu.SelectedIndex = saveItem.Enabled ? _metadataMenu.Items.IndexOf(saveItem) : 0;
        }

        private void RedrawFormatMenu()
        {
            _formatMenu.Clear();
            _formatMenu.Add(new NativeItem("XML", Translation.Translate("Default format for Map Editor. Choose this one if you have no idea. This saves props, vehicles and peds.")));
            _formatMenu.Add(new NativeItem("Simple Trainer",
                Translation.Translate("Format used in Simple Trainer mod (objects.ini). Only saves props.")));
            if (_savingMap)
            {
                _formatMenu.Add(new NativeItem(Translation.Translate("C# Code"),
                    Translation.Translate("Directly outputs to C# code to spawn your entities. Saves props, vehicles and peds.")));
                _formatMenu.Add(new NativeItem(Translation.Translate("Raw"),
                    Translation.Translate("Writes the entity and their position and rotation. Useful for taking coordinates.")));
            }
            _formatMenu.Add(new NativeItem("Spooner (Legacy)",
                Translation.Translate("Format used in Object Spooner mod (.SP00N).")));
            _formatMenu.Add(new NativeItem("Menyoo", Translation.Translate("Format used in Meynoo mod (.xml).")));

            if (!_savingMap)
                _formatMenu.Add(new NativeItem(Translation.Translate("File Chooser...")));

            _formatMenu.SelectedIndex = 0;
        }

        public void AddItemToEntityMenu(MapObject obj)
        {
            if (obj == null) return;
            var name = ObjectDatabase.MainDb.ContainsValue(obj.Hash) ? ObjectDatabase.MainDb.First(pair => pair.Value == obj.Hash).Key : "Unknown World Prop";
            _currentObjectsMenu.Add(new NativeItem("~h~[WORLD]~h~ " + name)
            {
                Tag = new EntityMenuTag { Kind = EntityMenuKind.World, WorldId = obj.Id },
            });
        }

        public void AddItemToEntityMenu(Marker mark)
        {
            if (mark == null) return;
            _currentObjectsMenu.Add(new NativeItem("~h~[MARK]~h~ " + mark.Type)
            {
                Tag = new EntityMenuTag { Kind = EntityMenuKind.Marker, Id = mark.Id },
            });
        }

        public void AddItemToEntityMenu(DynamicPickup pickup)
        {
            if (pickup == null) return;
            _currentObjectsMenu.Add(new NativeItem("~h~[PICKUP]~h~ " + pickup.PickupName)
            {
                Tag = new EntityMenuTag { Kind = EntityMenuKind.Pickup, Id = pickup.UID },
            });
        }

        public void AddItemToEntityMenu(Entity ent)
        {
            if (ent == null) return;
            var name = "";
            var type = "";
            if (ent is Prop)
            {
                name = ObjectDatabase.MainDb.ContainsValue(ent.Model.Hash) ? ObjectDatabase.MainDb.First(pair => pair.Value == ent.Model.Hash).Key : "Unknown Prop";
                type = "~h~[PROP]~h~ ";
            }
            else if (ent is Vehicle)
            {
                name = ObjectDatabase.VehicleDb.ContainsValue(ent.Model.Hash) ? ObjectDatabase.VehicleDb.First(x => x.Value == ent.Model.Hash).Key.ToUpper() : "Unknown Vehicle";
                type = "~h~[VEH]~h~ ";
            }
            else if (ent is Ped)
            {
                name = ObjectDatabase.PedDb.ContainsValue(ent.Model.Hash) ? ObjectDatabase.PedDb.First(x => x.Value == ent.Model.Hash).Key.ToUpper() : "Unknown Ped";
                type = "~h~[PED]~h~ ";
            }
            _currentObjectsMenu.Add(new NativeItem(type + name)
            {
                Tag = new EntityMenuTag { Kind = EntityMenuKind.Entity, Id = ent.Handle },
            });
        }

        private void RemoveFromEntityMenu(Func<EntityMenuTag, bool> predicate)
        {
            var found = _currentObjectsMenu.Items.FirstOrDefault(item => item.Tag is EntityMenuTag tag && predicate(tag));
            if (found == null) return;
            _currentObjectsMenu.Remove(found);
            if (_currentObjectsMenu.Items.Count == 0)
                SetMenuVisible(_currentObjectsMenu, false);
        }

        public void RemoveItemFromEntityMenu(Entity ent)
        {
            if (PropStreamer.IsPickup(ent.Handle))
            {
                var uid = PropStreamer.GetPickup(ent.Handle).UID;
                RemoveFromEntityMenu(tag => tag.Kind == EntityMenuKind.Pickup && tag.Id == uid);
            }
            else
            {
                RemoveFromEntityMenu(tag => tag.Kind == EntityMenuKind.Entity && tag.Id == ent.Handle);
            }
        }

        public void RemoveItemFromEntityMenu(string id)
        {
            RemoveFromEntityMenu(tag => tag.Kind == EntityMenuKind.World && tag.WorldId == id);
        }

        public void RemoveMarkerFromEntityMenu(int id)
        {
            RemoveFromEntityMenu(tag => tag.Kind == EntityMenuKind.Marker && tag.Id == id);
        }

        public void OnEntityTeleport(object sender, ItemActivatedArgs e)
        {
            if (!IsInFreecam) return;
            if (!(e.Item.Tag is EntityMenuTag tag)) return;

            switch (tag.Kind)
            {
                case EntityMenuKind.Pickup:
                {
                    var pickup = PropStreamer.GetPickupByUID(tag.Id);
                    if (pickup == null) return;
                    if (_settings.SnapCameraToSelectedObject)
                    {
                        _mainCamera.Position = pickup.RealPosition + new Vector3(5f, 5f, 10f);
                        _mainCamera.PointAt(pickup.RealPosition);
                    }
                    CloseAllMenus();
                    Script.Wait(300);
                    _selectedProp = Compat.Ent(pickup.ObjectHandle);
                    RedrawObjectInfoMenu(_selectedProp, true);
                    SetMenuVisible(_objectInfoMenu, true);
                    return;
                }
                case EntityMenuKind.World:
                {
                    var mapObj = PropStreamer.RemovedObjects.FirstOrDefault(obj => obj.Id == tag.WorldId);
                    if (mapObj == null) return;
                    var t = World.CreateProp(mapObj.Hash, mapObj.Position, mapObj.Rotation, true, false);
                    t.PositionNoOffset = mapObj.Position;
                    CloseAllMenus();
                    RemoveItemFromEntityMenu(mapObj.Id);
                    PropStreamer.RemovedObjects.Remove(mapObj);
                    return;
                }
                case EntityMenuKind.Marker:
                {
                    Marker tmpM = PropStreamer.Markers.FirstOrDefault(m => m.Id == tag.Id);
                    if (tmpM == null) return;
                    _mainCamera.Position = tmpM.Position + new Vector3(5f, 5f, 10f);
                    if (_settings.SnapCameraToSelectedObject)
                        _mainCamera.PointAt(tmpM.Position);
                    CloseAllMenus();
                    _selectedMarker = tmpM;
                    RedrawObjectInfoMenu(_selectedMarker, true);
                    SetMenuVisible(_objectInfoMenu, true);
                    return;
                }
                default:
                {
                    var prop = Compat.Ent(tag.Id);
                    if (prop == null || !prop.Exists()) return;
                    if (_settings.SnapCameraToSelectedObject)
                    {
                        _mainCamera.Position = prop.Position + new Vector3(5f, 5f, 10f);
                        _mainCamera.PointAt(prop, Vector3.Zero);
                    }
                    CloseAllMenus();
                    _selectedProp = prop;
                    RedrawObjectInfoMenu(_selectedProp, true);
                    SetMenuVisible(_objectInfoMenu, true);
                    return;
                }
            }
        }
    }
}
