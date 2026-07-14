using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GTA;
using GTA.Math;
using GTA.Native;
using LemonUI.Menus;

namespace MapEditor
{
    public partial class MapEditor
    {
        /// <summary>
        /// A scrollable numeric field. The old NativeUI build backed each of these with a
        /// pre-materialised list of every possible value (3,000,001 entries for a position
        /// axis); LemonUI's dynamic item computes the next value on demand instead.
        /// </summary>
        private static NativeDynamicItem<float> NumberItem(string title, float initial, Action<float> apply,
            float step, float min, float max)
        {
            var item = new NativeDynamicItem<float>(title, (float)Math.Round(initial, 2));
            item.ItemChanged += (sender, e) =>
            {
                float delta = e.Direction == Direction.Left ? -step : step;
                float value = (float)Math.Round(e.Object + delta, 2);
                if (value < min) value = min;
                if (value > max) value = max;
                apply(value);
                e.Object = value;
            };
            return item;
        }

        private const float PositionMin = -_possibleRangeUnits;
        private const float PositionMax = _possibleRangeUnits;
        private const float _possibleRangeUnits = 15000f; // _possibleRange * 0.01f

        private void RedrawObjectInfoMenu(Entity ent, bool refreshIndex)
        {
            if (ent == null) return;
            string name = "";

            if (IsProp(ent))
                name = ObjectDatabase.MainDb.ContainsValue(ent.Model.Hash) ? ObjectDatabase.MainDb.First(x => x.Value == ent.Model.Hash).Key.ToUpper() : "Unknown Prop";
            if (IsVehicle(ent))
                name = ObjectDatabase.VehicleDb.ContainsValue(ent.Model.Hash) ? ObjectDatabase.VehicleDb.First(x => x.Value == ent.Model.Hash).Key.ToUpper() : "Unknown Vehicle";
            if (IsPed(ent))
                name = ObjectDatabase.PedDb.ContainsValue(ent.Model.Hash) ? ObjectDatabase.PedDb.First(x => x.Value == ent.Model.Hash).Key.ToUpper() : "Unknown Ped";

            _objectInfoMenu.Name = "~b~" + name;
            _objectInfoMenu.Clear();

            var posXitem = NumberItem(Translation.Translate("Position X"), ent.Position.X,
                v => SetEntityPosition(ent, new Vector3(v, ent.Position.Y, ent.Position.Z)), ScrollStep, PositionMin, PositionMax);
            var posYitem = NumberItem(Translation.Translate("Position Y"), ent.Position.Y,
                v => SetEntityPosition(ent, new Vector3(ent.Position.X, v, ent.Position.Z)), ScrollStep, PositionMin, PositionMax);
            var posZitem = NumberItem(Translation.Translate("Position Z"), ent.Position.Z,
                v => SetEntityPosition(ent, new Vector3(ent.Position.X, ent.Position.Y, v)), ScrollStep, PositionMin, PositionMax);

            // Pitch/Roll/Yaw map onto Rotation Y/X/Z, matching what the old list handlers wrote.
            var rotXitem = NumberItem(Translation.Translate("Pitch"), ent.Rotation.Y,
                v => SetEntityRotation(ent, new Vector3(ent.Rotation.X, v, ent.Rotation.Z)), ScrollStep, -360f, 360f);
            var rotYitem = NumberItem(Translation.Translate("Roll"), ent.Rotation.X,
                v => SetEntityRotation(ent, new Vector3(v, ent.Rotation.Y, ent.Rotation.Z)), ScrollStep, -360f, 360f);
            var rotZitem = NumberItem(Translation.Translate("Yaw"), ent.Rotation.Z,
                v => SetEntityRotation(ent, new Vector3(ent.Rotation.X, ent.Rotation.Y, v)), ScrollStep, -360f, 360f);

            posXitem.Activated += (sender, item) =>
                SetObjectVector(ent, new Vector3(GetSafeFloat(Compat.GetUserInput(ent.Position.X.ToString(CultureInfo.InvariantCulture), 10), ent.Position.X), ent.Position.Y, ent.Position.Z));
            posYitem.Activated += (sender, item) =>
                SetObjectVector(ent, new Vector3(ent.Position.X, GetSafeFloat(Compat.GetUserInput(ent.Position.Y.ToString(CultureInfo.InvariantCulture), 10), ent.Position.Y), ent.Position.Z));
            posZitem.Activated += (sender, item) =>
                SetObjectVector(ent, new Vector3(ent.Position.X, ent.Position.Y, GetSafeFloat(Compat.GetUserInput(ent.Position.Z.ToString(CultureInfo.InvariantCulture), 10), ent.Position.Z)));

            rotXitem.Activated += (sender, item) =>
                SetObjectRotation(ent, new Vector3(ent.Rotation.X, GetSafeFloat(Compat.GetUserInput(ent.Rotation.Y.ToString(CultureInfo.InvariantCulture).Limit(10), 10), ent.Rotation.Y), ent.Rotation.Z));
            rotYitem.Activated += (sender, item) =>
                SetObjectRotation(ent, new Vector3(GetSafeFloat(Compat.GetUserInput(ent.Rotation.X.ToString(CultureInfo.InvariantCulture).Limit(10), 10), ent.Rotation.X), ent.Rotation.Y, ent.Rotation.Z));
            rotZitem.Activated += (sender, item) =>
                SetObjectRotation(ent, new Vector3(ent.Rotation.X, ent.Rotation.Y, GetSafeFloat(Compat.GetUserInput(ent.Rotation.Z.ToString(CultureInfo.InvariantCulture).Limit(10), 10), ent.Rotation.Z)));

            var dynamic = new NativeCheckboxItem(Translation.Translate("Dynamic"), !PropStreamer.StaticProps.Contains(ent.Handle));
            dynamic.CheckboxChanged += (ite, e) =>
            {
                var checkd = dynamic.Checked;
                if (checkd && PropStreamer.StaticProps.Contains(ent.Handle)) PropStreamer.StaticProps.Remove(ent.Handle);
                else if (!checkd && !PropStreamer.StaticProps.Contains(ent.Handle)) PropStreamer.StaticProps.Add(ent.Handle);

                ent.IsPositionFrozen = PropStreamer.StaticProps.Contains(ent.Handle);

                // A prop with its physics on will not hold the layout a generator places it in, so the two
                // rows that open them come and go with this one: the menu has to be built over again.
                if (!IsProp(ent)) return;

                int selected = _objectInfoMenu.SelectedIndex;
                RedrawObjectInfoMenu(ent, false);
                _objectInfoMenu.SelectedIndex = ClampIndex(selected, _objectInfoMenu.Items.Count);
            };

            var ident = new NativeItem("Identification", "Optional identification for easier access during scripting.");
            if (PropStreamer.Identifications.ContainsKey(ent.Handle))
                ident.AltTitle = PropStreamer.Identifications[ent.Handle];

            ident.Activated += (sender, item) =>
            {
                var hasId = PropStreamer.Identifications.ContainsKey(ent.Handle);
                var newLabel = hasId
                    ? Compat.GetUserInput(PropStreamer.Identifications[ent.Handle], 20)
                    : Compat.GetUserInput(20);

                if (newLabel == null) return;

                if (PropStreamer.Identifications.ContainsValue(newLabel))
                {
                    Compat.Notify(Translation.Translate("~r~~h~Map Editor~h~~w~~n~The identification must be unique!"));
                    return;
                }

                if (newLabel.Length > 0 && (Regex.IsMatch(newLabel, @"^\d") || newLabel.StartsWith(".") || newLabel.StartsWith(",") || newLabel.StartsWith("\\")))
                {
                    Compat.Notify(Translation.Translate("~r~~h~Map Editor~h~~w~~n~This identification is invalid!"));
                    return;
                }

                if (hasId)
                    PropStreamer.Identifications[ent.Handle] = newLabel;
                else
                    PropStreamer.Identifications.Add(ent.Handle, newLabel);

                ident.AltTitle = newLabel;
            };

            _objectInfoMenu.Add(posXitem);
            _objectInfoMenu.Add(posYitem);
            _objectInfoMenu.Add(posZitem);
            _objectInfoMenu.Add(rotXitem);
            _objectInfoMenu.Add(rotYitem);
            _objectInfoMenu.Add(rotZitem);

            // The generators lay copies out and expect them to stay put: a vehicle drives off, a ped walks
            // off, and a prop with its physics on falls over. Only a frozen prop holds what they build.
            if (IsProp(ent) && PropStreamer.StaticProps.Contains(ent.Handle))
            {
                var stackingItem = new NativeItem(Translation.Translate("Stacking Tool"), Translation.Translate(
                    "Copy this object along its own X, Y and Z axes, spaced by the model's own size."));
                stackingItem.Activated += (sender, item) => BeginStacking(ent);
                _objectInfoMenu.Add(stackingItem);

                var loopingItem = new NativeItem(Translation.Translate("Looping Generator"), Translation.Translate(
                    "Copy this object around a loop, each copy carried round and turned with it."));
                loopingItem.Activated += (sender, item) => BeginLooping(ent);
                _objectInfoMenu.Add(loopingItem);
            }

            _objectInfoMenu.Add(dynamic);
            _objectInfoMenu.Add(ident);

            if (IsProp(ent))
            {
                var doorItem = new NativeCheckboxItem("Door", Translation.Translate("This option overrides the \"Dynamic\" setting."), PropStreamer.Doors.Contains(ent.Handle));
                doorItem.CheckboxChanged += (sender, e) =>
                {
                    if (doorItem.Checked)
                    {
                        PropStreamer.Doors.Add(ent.Handle);
                        Function.Call(Hash.SET_ENTITY_DYNAMIC, ent.Handle, false);
                        ent.IsPositionFrozen = false;
                    }
                    else
                    {
                        PropStreamer.Doors.Remove(ent.Handle);
                        var isDynamic = !PropStreamer.StaticProps.Contains(ent.Handle);
                        Function.Call(Hash.SET_ENTITY_DYNAMIC, ent.Handle, isDynamic);
                        ent.IsPositionFrozen = !isDynamic;
                    }
                };
                _objectInfoMenu.Add(doorItem);
            }

            if (IsPed(ent))
            {
                var actions = new List<string> { "None", "Any - Walk", "Any - Warp", "Wander" };
                actions.AddRange(ObjectDatabase.ScrenarioDatabase.Keys);
                var scenarioItem = new NativeListItem<string>(Translation.Translate("Idle Action"), actions.ToArray())
                {
                    SelectedIndex = ClampIndex(actions.IndexOf(PropStreamer.ActiveScenarios[ent.Handle]), actions.Count),
                };
                scenarioItem.ItemChanged += (item, e) =>
                {
                    PropStreamer.ActiveScenarios[ent.Handle] = e.Object;
                    _changesMade++;
                };
                scenarioItem.Activated += (item, e) =>
                {
                    var scenarioName = PropStreamer.ActiveScenarios[ent.Handle];
                    if (scenarioName == "None")
                    {
                        ((Ped)ent).Task.ClearAll();
                        return;
                    }
                    if (scenarioName == "Any - Walk" || scenarioName == "Any")
                    {
                        Function.Call(Hash.TASK_USE_NEAREST_SCENARIO_TO_COORD, ent.Handle, ent.Position.X, ent.Position.Y, ent.Position.Z, 100f, -1);
                        return;
                    }
                    if (scenarioName == "Any - Warp")
                    {
                        Function.Call(Hash.TASK_USE_NEAREST_SCENARIO_TO_COORD_WARP, ent.Handle, ent.Position.X, ent.Position.Y, ent.Position.Z, 100f, -1);
                        return;
                    }
                    if (scenarioName == "Wander")
                    {
                        Function.Call(Hash.TASK_WANDER_STANDARD, ent.Handle, 0, 0);
                        return;
                    }
                    string scenario = ObjectDatabase.ScrenarioDatabase[scenarioName];
                    if (Function.Call<bool>(Hash.IS_PED_USING_SCENARIO, ent.Handle, scenario))
                        ((Ped)ent).Task.ClearAll();
                    else
                        Function.Call(Hash.TASK_START_SCENARIO_IN_PLACE, ent.Handle, scenario, 0, 0);
                };
                _objectInfoMenu.Add(scenarioItem);

                var rels = new List<string> { "Ballas", "Grove" };
                rels.AddRange(Enum.GetNames(typeof(Relationship)));
                var relItem = new NativeListItem<string>(Translation.Translate("Relationship"), rels.ToArray())
                {
                    SelectedIndex = ClampIndex(rels.IndexOf(PropStreamer.ActiveRelationships[ent.Handle]), rels.Count),
                };
                relItem.ItemChanged += (item, e) =>
                {
                    PropStreamer.ActiveRelationships[ent.Handle] = e.Object;
                    _changesMade++;
                };
                relItem.Activated += (item, e) =>
                {
                    ObjectDatabase.SetPedRelationshipGroup((Ped)ent, PropStreamer.ActiveRelationships[ent.Handle]);
                };
                _objectInfoMenu.Add(relItem);

                var weps = Enum.GetNames(typeof(WeaponHash));
                var wepItem = new NativeListItem<string>(Translation.Translate("Weapon"), weps)
                {
                    SelectedIndex = ClampIndex(weps.ToList().IndexOf(PropStreamer.ActiveWeapons[ent.Handle].ToString()), weps.Length),
                };
                wepItem.ItemChanged += (item, e) =>
                {
                    PropStreamer.ActiveWeapons[ent.Handle] = (WeaponHash)Enum.Parse(typeof(WeaponHash), e.Object);
                    _changesMade++;
                };
                wepItem.Activated += (item, e) =>
                {
                    ((Ped)ent).Weapons.RemoveAll();
                    if (PropStreamer.ActiveWeapons[ent.Handle] == WeaponHash.Unarmed) return;
                    ((Ped)ent).Weapons.Give(PropStreamer.ActiveWeapons[ent.Handle], 999, true, true);
                };
                _objectInfoMenu.Add(wepItem);

                RedrawPedComponentsMenu((Ped)ent);
                var componentsItem = _objectInfoMenu.AddSubMenu(_pedComponentsMenu);
                componentsItem.Title = Translation.Translate("Ped Components");
                componentsItem.Description = Translation.Translate("Change what the ped wears: its clothes, its hair and its face.");
            }

            if (IsVehicle(ent))
            {
                var veh = (Vehicle)ent;

                var sirentBool = new NativeCheckboxItem(Translation.Translate("Siren"), PropStreamer.ActiveSirens.Contains(ent.Handle));
                sirentBool.CheckboxChanged += (item, e) =>
                {
                    var check = sirentBool.Checked;
                    if (check && !PropStreamer.ActiveSirens.Contains(ent.Handle)) PropStreamer.ActiveSirens.Add(ent.Handle);
                    else if (!check && PropStreamer.ActiveSirens.Contains(ent.Handle)) PropStreamer.ActiveSirens.Remove(ent.Handle);
                    veh.IsSirenActive = check;
                    _changesMade++;
                };
                _objectInfoMenu.Add(sirentBool);

                var colors = (VehicleColor[])Enum.GetValues(typeof(VehicleColor));
                var colorNames = colors.Select(c => c.ToString()).ToArray();

                var primaryColor = new NativeListItem<string>(Translation.Translate("Primary Color"), colorNames)
                {
                    Description = Translation.Translate("The vehicle's main paint."),
                    SelectedIndex = ClampIndex(Array.IndexOf(colors, veh.Mods.PrimaryColor), colors.Length),
                };
                primaryColor.ItemChanged += (item, e) =>
                {
                    veh.Mods.PrimaryColor = colors[e.Index];
                    _changesMade++;
                };
                _objectInfoMenu.Add(primaryColor);

                var secondaryColor = new NativeListItem<string>(Translation.Translate("Secondary Color"), colorNames)
                {
                    Description = Translation.Translate("The vehicle's second paint, worn by its trim and its stripes."),
                    SelectedIndex = ClampIndex(Array.IndexOf(colors, veh.Mods.SecondaryColor), colors.Length),
                };
                secondaryColor.ItemChanged += (item, e) =>
                {
                    veh.Mods.SecondaryColor = colors[e.Index];
                    _changesMade++;
                };
                _objectInfoMenu.Add(secondaryColor);

                // Without a mod kit installed the game reports no liveries at all, even on the vehicles
                // that carry them.
                Function.Call(Hash.SET_VEHICLE_MOD_KIT, veh.Handle, 0);
                int liveryCount = veh.Mods.LiveryCount;

                if (liveryCount > 0)
                {
                    var liveries = new List<string> { Translation.Translate("None") };
                    for (int i = 0; i < liveryCount; i++)
                        liveries.Add((i + 1).ToString(CultureInfo.InvariantCulture));

                    var liveryItem = new NativeListItem<string>(Translation.Translate("Livery"), liveries.ToArray())
                    {
                        Description = Translation.Translate("The pattern painted over the vehicle, where its model has any."),
                        // The game counts the liveries from zero and calls "no livery" -1, but the row leads with None.
                        SelectedIndex = ClampIndex(veh.Mods.Livery + 1, liveries.Count),
                    };
                    liveryItem.ItemChanged += (item, e) =>
                    {
                        veh.Mods.Livery = e.Index - 1;
                        _changesMade++;
                    };
                    _objectInfoMenu.Add(liveryItem);
                }
            }

            if (PropStreamer.IsPickup(ent.Handle))
            {
                var pickup = PropStreamer.GetPickup(ent.Handle);

                var amountList = new NativeItem(Translation.Translate("Amount"));
                amountList.AltTitle = pickup.Amount.ToString();
                amountList.Activated += (sender, item) =>
                {
                    string playerInput = Compat.GetUserInput(10);
                    int newValue;
                    if (!int.TryParse(playerInput, out newValue) || newValue < -1)
                    {
                        Compat.Notify("~r~~h~Map Editor~h~~n~~w~" + Translation.Translate("Input was not in the correct format."));
                        return;
                    }
                    pickup.SetAmount(newValue);
                    amountList.AltTitle = pickup.Amount.ToString();
                    _selectedProp = Compat.Ent(pickup.ObjectHandle);
                    if (_settings.SnapCameraToSelectedObject)
                        _mainCamera.PointAt(_selectedProp, Vector3.Zero);
                };
                _objectInfoMenu.Add(amountList);

                var pickupTypesList = Enum.GetValues(typeof(ObjectDatabase.PickupHash)).Cast<ObjectDatabase.PickupHash>().ToList();
                var itemIndex = pickupTypesList.IndexOf((ObjectDatabase.PickupHash)pickup.PickupHash);

                var pickupTypeItem = new NativeListItem<string>("Type", pickupTypesList.Select(s => s.ToString()).ToArray())
                {
                    SelectedIndex = ClampIndex(itemIndex, pickupTypesList.Count),
                };
                pickupTypeItem.ItemChanged += (sender, e) =>
                {
                    pickup.SetPickupHash((int)pickupTypesList[e.Index]);
                    _selectedProp = Compat.Ent(pickup.ObjectHandle);
                    if (_settings.SnapCameraToSelectedObject)
                        _mainCamera.PointAt(_selectedProp, Vector3.Zero);
                };
                _objectInfoMenu.Add(pickupTypeItem);

                var timeoutTime = new NativeItem("Regeneration Time");
                timeoutTime.AltTitle = pickup.Timeout.ToString();
                timeoutTime.Activated += (sender, item) =>
                {
                    string playerInput = Compat.GetUserInput(10);
                    int newValue;
                    if (!int.TryParse(playerInput, out newValue) || newValue < 0)
                    {
                        Compat.Notify("~r~~h~Map Editor~h~~n~~w~" + Translation.Translate("Input was not in the correct format."));
                        return;
                    }
                    pickup.Timeout = newValue;
                    timeoutTime.AltTitle = newValue.ToString();
                };
                _objectInfoMenu.Add(timeoutTime);
            }

            if (refreshIndex && _objectInfoMenu.Items.Count > 0)
                _objectInfoMenu.SelectedIndex = 0;
        }

        private void SetEntityPosition(Entity ent, Vector3 pos)
        {
            if (!IsProp(ent))
                ent.Position = pos;
            else
                ent.PositionNoOffset = pos;

            SyncPickup(ent);
            _changesMade++;
        }

        private void SetEntityRotation(Entity ent, Vector3 rot)
        {
            ent.Quaternion = rot.ToQuaternion();
            _changesMade++;
        }

        public void SetObjectVector(Entity ent, Vector3 vect)
        {
            SetEntityPosition(ent, vect);
            RedrawObjectInfoMenu(ent, false);
        }

        public void SetObjectRotation(Entity ent, Vector3 rot)
        {
            SetEntityRotation(ent, rot);
            RedrawObjectInfoMenu(ent, false);
        }

        public void SetMarkerVector(Marker ent, Vector3 v)
        {
            ent.Position = v;
            RedrawObjectInfoMenu(ent, false);
        }

        public void SetMarkerRotation(Marker ent, Vector3 v)
        {
            ent.Rotation = v;
            RedrawObjectInfoMenu(ent, false);
        }

        public void SetMarkerScale(Marker ent, Vector3 v)
        {
            ent.Scale = v;
            RedrawObjectInfoMenu(ent, false);
        }

        private void RedrawObjectInfoMenu(Marker ent, bool refreshIndex)
        {
            if (ent == null) return;

            _objectInfoMenu.Name = "~b~" + ent.Type + " #" + ent.Id;
            _objectInfoMenu.Clear();

            var type = new NativeListItem<string>(Translation.Translate("Type"), _markersTypes)
            {
                SelectedIndex = ClampIndex(_markersTypes.ToList().IndexOf(ent.Type.ToString()), _markersTypes.Length),
            };
            type.ItemChanged += (ite, e) =>
            {
                MarkerType hash;
                Enum.TryParse(e.Object, out hash);
                ent.Type = hash;
            };

            var posXitem = NumberItem(Translation.Translate("Position X"), ent.Position.X,
                v => ent.Position = new Vector3(v, ent.Position.Y, ent.Position.Z), ScrollStep, PositionMin, PositionMax);
            var posYitem = NumberItem(Translation.Translate("Position Y"), ent.Position.Y,
                v => ent.Position = new Vector3(ent.Position.X, v, ent.Position.Z), ScrollStep, PositionMin, PositionMax);
            var posZitem = NumberItem(Translation.Translate("Position Z"), ent.Position.Z,
                v => ent.Position = new Vector3(ent.Position.X, ent.Position.Y, v), ScrollStep, PositionMin, PositionMax);

            var rotXitem = NumberItem(Translation.Translate("Rotation X"), ent.Rotation.X,
                v => ent.Rotation = new Vector3(v, ent.Rotation.Y, ent.Rotation.Z), ScrollStep, -360f, 360f);
            var rotYitem = NumberItem(Translation.Translate("Rotation Y"), ent.Rotation.Y,
                v => ent.Rotation = new Vector3(ent.Rotation.X, v, ent.Rotation.Z), ScrollStep, -360f, 360f);
            var rotZitem = NumberItem(Translation.Translate("Rotation Z"), ent.Rotation.Z,
                v => ent.Rotation = new Vector3(ent.Rotation.X, ent.Rotation.Y, v), ScrollStep, -360f, 360f);

            var scaleXitem = NumberItem(Translation.Translate("Scale X"), ent.Scale.X,
                v => ent.Scale = new Vector3(v, ent.Scale.Y, ent.Scale.Z), ScrollStep, 0f, 10f);
            var scaleYitem = NumberItem(Translation.Translate("Scale Y"), ent.Scale.Y,
                v => ent.Scale = new Vector3(ent.Scale.X, v, ent.Scale.Z), ScrollStep, 0f, 10f);
            var scaleZitem = NumberItem(Translation.Translate("Scale Z"), ent.Scale.Z,
                v => ent.Scale = new Vector3(ent.Scale.X, ent.Scale.Y, v), ScrollStep, 0f, 10f);

            posXitem.Activated += (sender, item) =>
                SetMarkerVector(ent, new Vector3(GetSafeFloat(Compat.GetUserInput(ent.Position.X.ToString(CultureInfo.InvariantCulture), 10), ent.Position.X), ent.Position.Y, ent.Position.Z));
            posYitem.Activated += (sender, item) =>
                SetMarkerVector(ent, new Vector3(ent.Position.X, GetSafeFloat(Compat.GetUserInput(ent.Position.Y.ToString(CultureInfo.InvariantCulture), 10), ent.Position.Y), ent.Position.Z));
            posZitem.Activated += (sender, item) =>
                SetMarkerVector(ent, new Vector3(ent.Position.X, ent.Position.Y, GetSafeFloat(Compat.GetUserInput(ent.Position.Z.ToString(CultureInfo.InvariantCulture), 10), ent.Position.Z)));

            rotXitem.Activated += (sender, item) =>
                SetMarkerRotation(ent, new Vector3(GetSafeFloat(Compat.GetUserInput(ent.Rotation.X.ToString(CultureInfo.InvariantCulture), 10), ent.Rotation.X), ent.Rotation.Y, ent.Rotation.Z));
            rotYitem.Activated += (sender, item) =>
                SetMarkerRotation(ent, new Vector3(ent.Rotation.X, GetSafeFloat(Compat.GetUserInput(ent.Rotation.Y.ToString(CultureInfo.InvariantCulture), 10), ent.Rotation.Y), ent.Rotation.Z));
            rotZitem.Activated += (sender, item) =>
                SetMarkerRotation(ent, new Vector3(ent.Rotation.X, ent.Rotation.Y, GetSafeFloat(Compat.GetUserInput(ent.Rotation.Z.ToString(CultureInfo.InvariantCulture), 10), ent.Rotation.Z)));

            scaleXitem.Activated += (sender, item) =>
                SetMarkerScale(ent, new Vector3(GetSafeFloat(Compat.GetUserInput(ent.Scale.X.ToString(CultureInfo.InvariantCulture), 10), ent.Scale.X), ent.Scale.Y, ent.Scale.Z));
            scaleYitem.Activated += (sender, item) =>
                SetMarkerScale(ent, new Vector3(ent.Scale.X, GetSafeFloat(Compat.GetUserInput(ent.Scale.Y.ToString(CultureInfo.InvariantCulture), 10), ent.Scale.Y), ent.Scale.Z));
            scaleZitem.Activated += (sender, item) =>
                SetMarkerScale(ent, new Vector3(ent.Scale.X, ent.Scale.Y, GetSafeFloat(Compat.GetUserInput(ent.Scale.Z.ToString(CultureInfo.InvariantCulture), 10), ent.Scale.Z)));

            var possibleColors = Enumerable.Range(0, 256).ToArray();

            var colorR = new NativeListItem<int>(Translation.Translate("Red Color"), possibleColors) { SelectedIndex = ClampIndex(ent.Red, 256) };
            var colorG = new NativeListItem<int>(Translation.Translate("Green Color"), possibleColors) { SelectedIndex = ClampIndex(ent.Green, 256) };
            var colorB = new NativeListItem<int>(Translation.Translate("Blue Color"), possibleColors) { SelectedIndex = ClampIndex(ent.Blue, 256) };
            var colorA = new NativeListItem<int>(Translation.Translate("Transparency"), possibleColors) { SelectedIndex = ClampIndex(ent.Alpha, 256) };

            colorR.ItemChanged += (item, e) => ent.Red = e.Object;
            colorG.ItemChanged += (item, e) => ent.Green = e.Object;
            colorB.ItemChanged += (item, e) => ent.Blue = e.Object;
            colorA.ItemChanged += (item, e) => ent.Alpha = e.Object;

            var bobItem = new NativeCheckboxItem(Translation.Translate("Bop Up And Down"), ent.BobUpAndDown);
            bobItem.CheckboxChanged += (ite, e) => ent.BobUpAndDown = bobItem.Checked;

            var faceCam = new NativeCheckboxItem(Translation.Translate("Face Camera"), ent.RotateToCamera);
            faceCam.CheckboxChanged += (ite, e) => ent.RotateToCamera = faceCam.Checked;

            var targetId = 0;
            if (ent.TeleportTarget.HasValue)
            {
                var ourMarkers = PropStreamer.Markers
                    .Where(m => (m.Position - ent.TeleportTarget.Value).Length() < 1f)
                    .OrderBy(m => (m.Position - ent.TeleportTarget.Value).Length())
                    .ToList();
                if (ourMarkers.Any())
                    targetId = ourMarkers.First().Id + 1;
            }

            var targetOptions = Enumerable.Range(-1, _markerCounter + 1).ToArray();
            var targetPos = new NativeListItem<int>(Translation.Translate("Teleport Marker Target"), targetOptions)
            {
                SelectedIndex = ClampIndex(targetId, Math.Max(1, targetOptions.Length)),
            };
            targetPos.ItemChanged += (sender, e) =>
            {
                if (e.Index == 0)
                {
                    ent.TeleportTarget = null;
                    return;
                }
                ent.TeleportTarget = PropStreamer.Markers.FirstOrDefault(n => n.Id == e.Index - 1)?.Position;
            };

            var loadPointItem = new NativeCheckboxItem(Translation.Translate("Mark as Loading Point"),
                Translation.Translate("Player will be teleported here BEFORE starting to load the map."),
                PropStreamer.CurrentMapMetadata.LoadingPoint.HasValue &&
                (PropStreamer.CurrentMapMetadata.LoadingPoint.Value - ent.Position).Length() < 1f);
            loadPointItem.CheckboxChanged += (sender, e) =>
            {
                PropStreamer.CurrentMapMetadata.LoadingPoint = loadPointItem.Checked ? ent.Position : (Vector3?)null;
            };

            var loadTeleportItem = new NativeCheckboxItem(Translation.Translate("Mark as Starting Point"),
                Translation.Translate("Player will be teleported here AFTER starting to load the map."),
                PropStreamer.CurrentMapMetadata.TeleportPoint.HasValue &&
                (PropStreamer.CurrentMapMetadata.TeleportPoint.Value - ent.Position).Length() < 1f);
            loadTeleportItem.CheckboxChanged += (sender, e) =>
            {
                PropStreamer.CurrentMapMetadata.TeleportPoint = loadTeleportItem.Checked ? ent.Position : (Vector3?)null;
            };

            var visiblityItem = new NativeCheckboxItem(Translation.Translate("Only Visible In Editor"), ent.OnlyVisibleInEditor);
            visiblityItem.CheckboxChanged += (sender, e) => ent.OnlyVisibleInEditor = visiblityItem.Checked;

            _objectInfoMenu.Add(type);
            _objectInfoMenu.Add(posXitem);
            _objectInfoMenu.Add(posYitem);
            _objectInfoMenu.Add(posZitem);
            _objectInfoMenu.Add(rotXitem);
            _objectInfoMenu.Add(rotYitem);
            _objectInfoMenu.Add(rotZitem);
            _objectInfoMenu.Add(scaleXitem);
            _objectInfoMenu.Add(scaleYitem);
            _objectInfoMenu.Add(scaleZitem);
            _objectInfoMenu.Add(colorR);
            _objectInfoMenu.Add(colorG);
            _objectInfoMenu.Add(colorB);
            _objectInfoMenu.Add(colorA);
            _objectInfoMenu.Add(bobItem);
            _objectInfoMenu.Add(faceCam);
            _objectInfoMenu.Add(targetPos);
            _objectInfoMenu.Add(loadPointItem);
            _objectInfoMenu.Add(loadTeleportItem);
            _objectInfoMenu.Add(visiblityItem);

            if (refreshIndex && _objectInfoMenu.Items.Count > 0)
                _objectInfoMenu.SelectedIndex = 0;
        }
    }
}
