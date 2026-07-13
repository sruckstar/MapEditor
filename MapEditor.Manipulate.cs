using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
using Control = GTA.Control;

namespace MapEditor
{
    public partial class MapEditor
    {
        private void ProcessFreelook(Entity hitEnt, float mouseX, float mouseY, float movementModifier, float modifier)
        {
            if (!_menuPool.AreAnyVisible || Game.LastInputMethod == InputMethod.GamePad)
                _mainCamera.Rotation = new Vector3(_mainCamera.Rotation.X + mouseY, _mainCamera.Rotation.Y, _mainCamera.Rotation.Z + mouseX);

            var dir = VectorExtensions.RotationToDirection(_mainCamera.Rotation);
            var rotLeft = _mainCamera.Rotation + new Vector3(0, 0, -10);
            var rotRight = _mainCamera.Rotation + new Vector3(0, 0, 10);
            var right = VectorExtensions.RotationToDirection(rotRight) - VectorExtensions.RotationToDirection(rotLeft);

            var newPos = _mainCamera.Position;
            if (Game.IsControlPressed(Control.MoveUpOnly))
                newPos += dir * movementModifier;
            if (Game.IsControlPressed(Control.MoveDownOnly))
                newPos -= dir * movementModifier;
            if (Game.IsControlPressed(Control.MoveLeftOnly))
                newPos += right * movementModifier;
            if (Game.IsControlPressed(Control.MoveRightOnly))
                newPos -= right * movementModifier;
            _mainCamera.Position = newPos;
            Game.Player.Character.PositionNoOffset = _mainCamera.Position - dir * 8f;

            if (_snappedProp != null)
            {
                if (!IsProp(_snappedProp))
                    _snappedProp.Position = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, _snappedProp);
                else
                    _snappedProp.PositionNoOffset = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, _snappedProp);

                if (Game.IsControlPressed(Control.CursorScrollUp) || Game.IsControlPressed(Control.FrontendRb))
                {
                    _snappedProp.Rotation = _snappedProp.Rotation - new Vector3(0f, 0f, modifier);
                    if (IsPed(_snappedProp))
                        _snappedProp.Heading = _snappedProp.Rotation.Z;
                }

                if (Game.IsControlPressed(Control.CursorScrollDown) || Game.IsControlPressed(Control.FrontendLb))
                {
                    _snappedProp.Rotation = _snappedProp.Rotation + new Vector3(0f, 0f, modifier);
                    if (IsPed(_snappedProp))
                        _snappedProp.Heading = _snappedProp.Rotation.Z;
                }

                if (Game.IsControlJustPressed(Control.CreatorDelete))
                {
                    RemoveItemFromEntityMenu(_snappedProp);
                    if (PropStreamer.IsPickup(_snappedProp.Handle))
                    {
                        PropStreamer.RemovePickup(_snappedProp.Handle);
                    }
                    else
                    {
                        PropStreamer.RemoveEntity(_snappedProp.Handle);
                        if (PropStreamer.Identifications.ContainsKey(_snappedProp.Handle))
                            PropStreamer.Identifications.Remove(_snappedProp.Handle);
                    }
                    _snappedProp = null;
                    _changesMade++;
                }

                if (_snappedProp != null && Game.IsControlJustPressed(Control.Attack))
                {
                    if (PropStreamer.IsPickup(_snappedProp.Handle))
                        PropStreamer.GetPickup(_snappedProp.Handle).UpdatePos();
                    _snappedProp = null;
                    _changesMade++;
                }

                DrawButtons(_snappedButtons);
            }
            else if (_snappedMarker != null)
            {
                _snappedMarker.Position = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, Game.Player.Character);

                if (Game.IsControlPressed(Control.CursorScrollUp) || Game.IsControlPressed(Control.FrontendRb))
                    _snappedMarker.Rotation = _snappedMarker.Rotation - new Vector3(0f, 0f, modifier);

                if (Game.IsControlPressed(Control.CursorScrollDown) || Game.IsControlPressed(Control.FrontendLb))
                    _snappedMarker.Rotation = _snappedMarker.Rotation + new Vector3(0f, 0f, modifier);

                if (Game.IsControlJustPressed(Control.CreatorDelete))
                {
                    RemoveMarkerFromEntityMenu(_snappedMarker.Id);
                    PropStreamer.Markers.Remove(_snappedMarker);
                    _snappedMarker = null;
                    _changesMade++;
                }

                if (Game.IsControlJustPressed(Control.Attack))
                {
                    _snappedMarker = null;
                    _changesMade++;
                }

                DrawButtons(_snappedButtons);
            }
            else
            {
                if (_settings.CrosshairType == CrosshairType.Orb)
                {
                    var pos = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, Game.Player.Character);
                    var color = Color.FromArgb(255, 200, 20, 20);
                    if (hitEnt != null && hitEnt.Handle != 0 && !PropStreamer.GetAllHandles().Contains(hitEnt.Handle))
                        color = Color.FromArgb(255, 20, 20, 255);
                    else if (hitEnt != null && hitEnt.Handle != 0 && PropStreamer.GetAllHandles().Contains(hitEnt.Handle))
                        color = Color.FromArgb(255, 200, 200, 20);
                    Function.Call(Hash.DRAW_MARKER, 28, pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 0f, 0f, 0f, 0.20f, 0.20f, 0.20f, color.R, color.G, color.B, color.A, false, true, 2, false, false, false, false);
                }

                if (Game.IsControlJustPressed(Control.Aim))
                {
                    if (hitEnt != null && PropStreamer.GetAllHandles().Contains(hitEnt.Handle))
                    {
                        _snappedProp = WrapEntity(hitEnt);
                        _changesMade++;
                    }
                    else
                    {
                        var pos = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, Game.Player.Character);
                        Marker mark = PropStreamer.Markers.FirstOrDefault(m => (m.Position - pos).Length() < 2f);
                        if (mark != null)
                        {
                            _snappedMarker = mark;
                            _changesMade++;
                        }
                    }
                }

                if (Game.IsControlJustPressed(Control.Attack))
                {
                    if (hitEnt != null && PropStreamer.GetAllHandles().Contains(hitEnt.Handle))
                    {
                        _selectedProp = WrapEntity(hitEnt);
                        RedrawObjectInfoMenu(_selectedProp, true);
                        CloseAllMenus();
                        SetMenuVisible(_objectInfoMenu, true);
                        if (_settings.SnapCameraToSelectedObject)
                            _mainCamera.PointAt(_selectedProp, Vector3.Zero);
                        _changesMade++;
                    }
                    else
                    {
                        var pos = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, Game.Player.Character);
                        Marker mark = PropStreamer.Markers.FirstOrDefault(m => (m.Position - pos).Length() < 2f);
                        if (mark != null)
                        {
                            _selectedMarker = mark;
                            RedrawObjectInfoMenu(_selectedMarker, true);
                            CloseAllMenus();
                            SetMenuVisible(_objectInfoMenu, true);
                            _changesMade++;
                        }
                    }
                }

                if (Game.IsControlJustReleased(Control.LookBehind))
                {
                    if (hitEnt != null)
                    {
                        CopyEntity(hitEnt);
                        _changesMade++;
                    }
                    else
                    {
                        var pos = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, Game.Player.Character);
                        Marker mark = PropStreamer.Markers.FirstOrDefault(m => (m.Position - pos).Length() < 2f);
                        if (mark != null)
                        {
                            var tmpMark = CloneMarker(mark);
                            AddItemToEntityMenu(tmpMark);
                            PropStreamer.Markers.Add(tmpMark);
                            _snappedMarker = tmpMark;
                            _changesMade++;
                        }
                    }
                }

                if (Game.IsControlJustPressed(Control.CreatorDelete))
                {
                    if (hitEnt != null && PropStreamer.GetAllHandles().Contains(hitEnt.Handle))
                    {
                        RemoveItemFromEntityMenu(hitEnt);
                        if (PropStreamer.Identifications.ContainsKey(hitEnt.Handle))
                            PropStreamer.Identifications.Remove(hitEnt.Handle);
                        if (PropStreamer.ActiveScenarios.ContainsKey(hitEnt.Handle))
                            PropStreamer.ActiveScenarios.Remove(hitEnt.Handle);
                        if (PropStreamer.ActiveRelationships.ContainsKey(hitEnt.Handle))
                            PropStreamer.ActiveRelationships.Remove(hitEnt.Handle);
                        if (PropStreamer.ActiveWeapons.ContainsKey(hitEnt.Handle))
                            PropStreamer.ActiveWeapons.Remove(hitEnt.Handle);
                        PropStreamer.RemoveEntity(hitEnt.Handle);
                        _changesMade++;
                    }
                    else if (hitEnt != null && !PropStreamer.GetAllHandles().Contains(hitEnt.Handle) && IsProp(hitEnt))
                    {
                        MapObject tmpObj = new MapObject()
                        {
                            Hash = hitEnt.Model.Hash,
                            Position = hitEnt.Position,
                            Rotation = hitEnt.Rotation,
                            Quaternion = Quaternion.GetEntityQuaternion(hitEnt),
                            Type = ObjectTypes.Prop,
                            Id = _mapObjCounter.ToString(),
                        };
                        _mapObjCounter++;
                        PropStreamer.RemovedObjects.Add(tmpObj);
                        AddItemToEntityMenu(tmpObj);
                        hitEnt.Delete();
                        _changesMade++;
                    }
                    else
                    {
                        var pos = VectorExtensions.RaycastEverything(new Vector2(0f, 0f), _mainCamera.Position, _mainCamera.Rotation, Game.Player.Character);
                        Marker mark = PropStreamer.Markers.FirstOrDefault(m => (m.Position - pos).Length() < 2f);
                        if (mark != null)
                        {
                            PropStreamer.Markers.Remove(mark);
                            RemoveMarkerFromEntityMenu(mark.Id);
                            _changesMade++;
                        }
                    }
                }

                DrawButtons(_freelookButtons);
            }
        }

        /// <summary>
        /// The raycast already hands back the concrete Prop/Vehicle/Ped subclass, so the entity
        /// can be used as-is.
        /// </summary>
        private static Entity WrapEntity(Entity hitEnt)
        {
            return hitEnt;
        }

        private Marker CloneMarker(Marker mark)
        {
            var tmpMark = new Marker()
            {
                BobUpAndDown = mark.BobUpAndDown,
                Red = mark.Red,
                Green = mark.Green,
                Blue = mark.Blue,
                Alpha = mark.Alpha,
                Position = mark.Position,
                RotateToCamera = mark.RotateToCamera,
                Rotation = mark.Rotation,
                Scale = mark.Scale,
                Type = mark.Type,
                Id = _markerCounter,
            };
            _markerCounter++;
            return tmpMark;
        }

        /// <summary>
        /// Duplicates the entity under the crosshair and snaps the copy to the cursor.
        /// </summary>
        private void CopyEntity(Entity hitEnt)
        {
            if (PropStreamer.IsPickup(hitEnt.Handle))
            {
                var oldPickup = PropStreamer.GetPickup(hitEnt.Handle);
                var oldRotation = Compat.Ent(oldPickup.ObjectHandle)?.Rotation.Z ?? 0f;
                var newPickup = PropStreamer.CreatePickup(new Model(oldPickup.PickupHash), oldPickup.Position,
                    oldRotation, oldPickup.Amount, oldPickup.Dynamic);
                AddItemToEntityMenu(newPickup);
                _snappedProp = Compat.Ent(newPickup.ObjectHandle);
                return;
            }

            if (IsProp(hitEnt))
            {
                var isDoor = PropStreamer.Doors.Contains(hitEnt.Handle);
                AddItemToEntityMenu(_snappedProp = PropStreamer.CreateProp(hitEnt.Model, hitEnt.Position, hitEnt.Rotation,
                    (!PropStreamer.StaticProps.Contains(hitEnt.Handle) && !isDoor), q: Quaternion.GetEntityQuaternion(hitEnt),
                    force: true, drawDistance: _settings.DrawDistance));
                if (isDoor && _snappedProp != null)
                {
                    _snappedProp.IsPositionFrozen = false;
                    PropStreamer.Doors.Add(_snappedProp.Handle);
                }
                return;
            }

            if (IsVehicle(hitEnt))
            {
                AddItemToEntityMenu(_snappedProp = PropStreamer.CreateVehicle(hitEnt.Model, hitEnt.Position, hitEnt.Rotation.Z,
                    !PropStreamer.StaticProps.Contains(hitEnt.Handle), drawDistance: _settings.DrawDistance));
                return;
            }

            if (IsPed(hitEnt))
            {
                AddItemToEntityMenu(_snappedProp = ((Ped)hitEnt).Clone(hitEnt.Rotation.Z));
                if (_snappedProp == null) return;

                PropStreamer.Peds.Add(_snappedProp.Handle);

                if (_settings.DrawDistance != -1)
                    _snappedProp.LodDistance = _settings.DrawDistance;

                if (PropStreamer.StaticProps.Contains(hitEnt.Handle))
                {
                    _snappedProp.IsPositionFrozen = true;
                    PropStreamer.StaticProps.Add(_snappedProp.Handle);
                }

                if (!PropStreamer.ActiveScenarios.ContainsKey(_snappedProp.Handle))
                    PropStreamer.ActiveScenarios.Add(_snappedProp.Handle, "None");

                if (PropStreamer.ActiveRelationships.ContainsKey(hitEnt.Handle))
                    PropStreamer.ActiveRelationships.Add(_snappedProp.Handle, PropStreamer.ActiveRelationships[hitEnt.Handle]);
                else if (!PropStreamer.ActiveRelationships.ContainsKey(_snappedProp.Handle))
                    PropStreamer.ActiveRelationships.Add(_snappedProp.Handle, DefaultRelationship.ToString());

                if (PropStreamer.ActiveWeapons.ContainsKey(hitEnt.Handle))
                    PropStreamer.ActiveWeapons.Add(_snappedProp.Handle, PropStreamer.ActiveWeapons[hitEnt.Handle]);
                else if (!PropStreamer.ActiveWeapons.ContainsKey(_snappedProp.Handle))
                    PropStreamer.ActiveWeapons.Add(_snappedProp.Handle, WeaponHash.Unarmed);
            }
        }

        private void ProcessSelectedProp(float modifier)
        {
            var tmp = _controlsRotate ? Color.FromArgb(200, 200, 20, 20) : Color.FromArgb(200, 200, 200, 10);
            var (min, max) = _selectedProp.Model.Dimensions;
            var modelDims = max - min;
            Function.Call(Hash.DRAW_MARKER, 0, _selectedProp.Position.X, _selectedProp.Position.Y, _selectedProp.Position.Z + modelDims.Z + 2f, 0f, 0f, 0f, 0f, 0f, 0f, 2f, 2f, 2f, tmp.R, tmp.G, tmp.B, tmp.A, 1, 0, 2, 2, 0, 0, 0);

            DrawEntityBox(_selectedProp, tmp);

            if (Game.IsControlJustReleased(Control.Duck))
                _controlsRotate = !_controlsRotate;

            if (Game.IsControlPressed(Control.FrontendRb))
            {
                float pedMod = _selectedProp is Ped ? -1f : 0f;
                if (!_controlsRotate)
                    MoveSelectedProp(new Vector3(0f, 0f, (modifier / 4) + pedMod));
                else
                {
                    _selectedProp.Quaternion = new Vector3(_selectedProp.Rotation.X, _selectedProp.Rotation.Y, _selectedProp.Rotation.Z - (modifier / 4)).ToQuaternion();
                    if (IsPed(_selectedProp))
                        _selectedProp.Heading = _selectedProp.Rotation.Z;
                }
                SyncPickup(_selectedProp);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.FrontendLb))
            {
                float pedMod = _selectedProp is Ped ? 1f : 0f;
                if (!_controlsRotate)
                    MoveSelectedProp(new Vector3(0f, 0f, -((modifier / 4) + pedMod)));
                else
                {
                    _selectedProp.Quaternion = new Vector3(_selectedProp.Rotation.X, _selectedProp.Rotation.Y, _selectedProp.Rotation.Z + (modifier / 4)).ToQuaternion();
                    if (IsPed(_selectedProp))
                        _selectedProp.Heading = _selectedProp.Rotation.Z;
                }
                SyncPickup(_selectedProp);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveUpOnly))
            {
                float pedMod = IsPed(_selectedProp) ? -1f : 0f;
                if (!_controlsRotate)
                {
                    var dir = VectorExtensions.RotationToDirection(_mainCamera.Rotation) * (modifier / 4);
                    MoveSelectedProp(new Vector3(dir.X, dir.Y, pedMod));
                }
                else
                    _selectedProp.Quaternion = new Vector3(_selectedProp.Rotation.X + (modifier / 4), _selectedProp.Rotation.Y, _selectedProp.Rotation.Z).ToQuaternion();
                SyncPickup(_selectedProp);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveDownOnly))
            {
                float pedMod = _selectedProp is Ped ? 1f : 0f;
                if (!_controlsRotate)
                {
                    var dir = VectorExtensions.RotationToDirection(_mainCamera.Rotation) * (modifier / 4);
                    MoveSelectedProp(new Vector3(-dir.X, -dir.Y, -pedMod));
                }
                else
                    _selectedProp.Quaternion = new Vector3(_selectedProp.Rotation.X - (modifier / 4), _selectedProp.Rotation.Y, _selectedProp.Rotation.Z).ToQuaternion();
                SyncPickup(_selectedProp);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveLeftOnly))
            {
                float pedMod = _selectedProp is Ped ? -1f : 0f;
                if (!_controlsRotate)
                {
                    var right = CameraRight(modifier);
                    MoveSelectedProp(new Vector3(right.X, right.Y, pedMod));
                }
                else
                    _selectedProp.Quaternion = new Vector3(_selectedProp.Rotation.X, _selectedProp.Rotation.Y + (modifier / 4), _selectedProp.Rotation.Z).ToQuaternion();
                SyncPickup(_selectedProp);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveRightOnly))
            {
                float pedMod = _selectedProp is Ped ? 1f : 0f;
                if (!_controlsRotate)
                {
                    var right = CameraRight(modifier);
                    MoveSelectedProp(new Vector3(-right.X, -right.Y, -pedMod));
                }
                else
                    _selectedProp.Quaternion = new Vector3(_selectedProp.Rotation.X, _selectedProp.Rotation.Y - (modifier / 4), _selectedProp.Rotation.Z).ToQuaternion();
                SyncPickup(_selectedProp);
                _changesMade++;
            }

            if (Game.IsControlJustReleased(Control.MoveLeftOnly) ||
                Game.IsControlJustReleased(Control.MoveRightOnly) ||
                Game.IsControlJustReleased(Control.MoveUpOnly) ||
                Game.IsControlJustReleased(Control.MoveDownOnly) ||
                Game.IsControlJustReleased(Control.FrontendLb) ||
                Game.IsControlJustReleased(Control.FrontendRb))
            {
                RedrawObjectInfoMenu(_selectedProp, false);
            }

            if (Game.IsControlJustReleased(Control.LookBehind))
            {
                Entity mainProp = CopySelectedProp();
                _changesMade++;
                _selectedProp = mainProp;
                if (_settings.SnapCameraToSelectedObject && _selectedProp != null)
                    _mainCamera.PointAt(_selectedProp, Vector3.Zero);
                if (_selectedProp != null) RedrawObjectInfoMenu(_selectedProp, true);
            }

            if (_selectedProp != null && Game.IsControlJustPressed(Control.CreatorDelete))
            {
                if (PropStreamer.Identifications.ContainsKey(_selectedProp.Handle))
                    PropStreamer.Identifications.Remove(_selectedProp.Handle);
                if (PropStreamer.ActiveScenarios.ContainsKey(_selectedProp.Handle))
                    PropStreamer.ActiveScenarios.Remove(_selectedProp.Handle);
                if (PropStreamer.ActiveRelationships.ContainsKey(_selectedProp.Handle))
                    PropStreamer.ActiveRelationships.Remove(_selectedProp.Handle);
                if (PropStreamer.ActiveWeapons.ContainsKey(_selectedProp.Handle))
                    PropStreamer.ActiveWeapons.Remove(_selectedProp.Handle);
                RemoveItemFromEntityMenu(_selectedProp);
                PropStreamer.RemoveEntity(_selectedProp.Handle);
                _selectedProp = null;
                SetMenuVisible(_objectInfoMenu, false);
                _mainCamera.StopPointing();
                _changesMade++;
            }

            if (_selectedProp != null && (Game.IsControlJustPressed(Control.PhoneCancel) || Game.IsControlJustPressed(Control.Attack)))
            {
                SyncPickup(_selectedProp);
                _selectedProp = null;
                SetMenuVisible(_objectInfoMenu, false);
                _mainCamera.StopPointing();
                _changesMade++;
            }

            DrawButtons(_selectedButtons);
        }

        private Vector3 CameraRight(float modifier)
        {
            var rotLeft = _mainCamera.Rotation + new Vector3(0, 0, -10);
            var rotRight = _mainCamera.Rotation + new Vector3(0, 0, 10);
            return (VectorExtensions.RotationToDirection(rotRight) - VectorExtensions.RotationToDirection(rotLeft)) * (modifier / 2);
        }

        /// <summary>
        /// Props need PositionNoOffset so they don't get shifted by their model's bounding origin.
        /// </summary>
        private void MoveSelectedProp(Vector3 delta)
        {
            var target = _selectedProp.Position + delta;
            if (!IsProp(_selectedProp))
                _selectedProp.Position = target;
            else
                _selectedProp.PositionNoOffset = target;
        }

        private static void SyncPickup(Entity ent)
        {
            if (ent != null && PropStreamer.IsPickup(ent.Handle))
                PropStreamer.GetPickup(ent.Handle).UpdatePos();
        }

        private Entity CopySelectedProp()
        {
            if (PropStreamer.IsPickup(_selectedProp.Handle))
            {
                var oldPickup = PropStreamer.GetPickup(_selectedProp.Handle);
                var oldRotation = Compat.Ent(oldPickup.ObjectHandle)?.Rotation.Z ?? 0f;
                var newPickup = PropStreamer.CreatePickup(new Model(oldPickup.PickupHash), oldPickup.Position,
                    oldRotation, oldPickup.Amount, oldPickup.Dynamic);
                AddItemToEntityMenu(newPickup);
                return Compat.Ent(newPickup.ObjectHandle);
            }

            if (_selectedProp is Prop)
            {
                var isDoor = PropStreamer.Doors.Contains(_selectedProp.Handle);
                Entity mainProp;
                AddItemToEntityMenu(mainProp = PropStreamer.CreateProp(_selectedProp.Model, _selectedProp.Position, _selectedProp.Rotation,
                    (!PropStreamer.StaticProps.Contains(_selectedProp.Handle) && !isDoor), force: true,
                    q: Quaternion.GetEntityQuaternion(_selectedProp), drawDistance: _settings.DrawDistance));
                if (isDoor && mainProp != null)
                {
                    mainProp.IsPositionFrozen = false;
                    PropStreamer.Doors.Add(mainProp.Handle);
                }
                return mainProp;
            }

            if (_selectedProp is Vehicle)
            {
                Entity mainProp;
                AddItemToEntityMenu(mainProp = PropStreamer.CreateVehicle(_selectedProp.Model, _selectedProp.Position,
                    _selectedProp.Rotation.Z, !PropStreamer.StaticProps.Contains(_selectedProp.Handle), drawDistance: _settings.DrawDistance));
                return mainProp;
            }

            if (_selectedProp is Ped ped)
            {
                Entity mainProp;
                AddItemToEntityMenu(mainProp = ped.Clone(_selectedProp.Rotation.Z));
                if (mainProp == null) return _selectedProp;

                PropStreamer.Peds.Add(mainProp.Handle);
                if (!PropStreamer.ActiveScenarios.ContainsKey(mainProp.Handle))
                    PropStreamer.ActiveScenarios.Add(mainProp.Handle, "None");

                if (_settings.DrawDistance != -1)
                    mainProp.LodDistance = _settings.DrawDistance;

                if (PropStreamer.ActiveRelationships.ContainsKey(_selectedProp.Handle))
                    PropStreamer.ActiveRelationships.Add(mainProp.Handle, PropStreamer.ActiveRelationships[_selectedProp.Handle]);
                else if (!PropStreamer.ActiveRelationships.ContainsKey(mainProp.Handle))
                    PropStreamer.ActiveRelationships.Add(mainProp.Handle, DefaultRelationship.ToString());

                if (PropStreamer.ActiveWeapons.ContainsKey(_selectedProp.Handle))
                    PropStreamer.ActiveWeapons.Add(mainProp.Handle, PropStreamer.ActiveWeapons[_selectedProp.Handle]);
                else if (!PropStreamer.ActiveWeapons.ContainsKey(mainProp.Handle))
                    PropStreamer.ActiveWeapons.Add(mainProp.Handle, WeaponHash.Unarmed);

                return mainProp;
            }

            return _selectedProp;
        }

        private void ProcessSelectedMarker(float modifier)
        {
            if (Game.IsControlJustReleased(Control.Duck))
                _controlsRotate = !_controlsRotate;

            if (Game.IsControlPressed(Control.FrontendRb))
            {
                if (!_controlsRotate)
                    _selectedMarker.Position += new Vector3(0f, 0f, (modifier / 4));
                else
                    _selectedMarker.Rotation += new Vector3(0f, 0f, modifier);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.FrontendLb))
            {
                if (!_controlsRotate)
                    _selectedMarker.Position -= new Vector3(0f, 0f, (modifier / 4));
                else
                    _selectedMarker.Rotation -= new Vector3(0f, 0f, modifier);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveUpOnly))
            {
                if (!_controlsRotate)
                {
                    var dir = VectorExtensions.RotationToDirection(_mainCamera.Rotation) * (modifier / 4);
                    _selectedMarker.Position += new Vector3(dir.X, dir.Y, 0f);
                }
                else
                    _selectedMarker.Rotation += new Vector3(modifier, 0f, 0f);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveDownOnly))
            {
                if (!_controlsRotate)
                {
                    var dir = VectorExtensions.RotationToDirection(_mainCamera.Rotation) * (modifier / 4);
                    _selectedMarker.Position -= new Vector3(dir.X, dir.Y, 0f);
                }
                else
                    _selectedMarker.Rotation -= new Vector3(modifier, 0f, 0f);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveLeftOnly))
            {
                if (!_controlsRotate)
                {
                    var right = CameraRight(modifier);
                    _selectedMarker.Position += new Vector3(right.X, right.Y, 0f);
                }
                else
                    _selectedMarker.Rotation += new Vector3(0f, modifier, 0f);
                _changesMade++;
            }

            if (Game.IsControlPressed(Control.MoveRightOnly))
            {
                if (!_controlsRotate)
                {
                    var right = CameraRight(modifier);
                    _selectedMarker.Position -= new Vector3(right.X, right.Y, 0f);
                }
                else
                    _selectedMarker.Rotation -= new Vector3(0f, modifier, 0f);
                _changesMade++;
            }

            if (Game.IsControlJustReleased(Control.MoveLeftOnly) ||
                Game.IsControlJustReleased(Control.MoveRightOnly) ||
                Game.IsControlJustReleased(Control.MoveUpOnly) ||
                Game.IsControlJustReleased(Control.MoveDownOnly) ||
                Game.IsControlJustReleased(Control.FrontendLb) ||
                Game.IsControlJustReleased(Control.FrontendRb))
            {
                RedrawObjectInfoMenu(_selectedMarker, false);
            }

            if (Game.IsControlJustReleased(Control.LookBehind))
            {
                var tmpMark = CloneMarker(_selectedMarker);
                PropStreamer.Markers.Add(tmpMark);
                AddItemToEntityMenu(tmpMark);
                _selectedMarker = tmpMark;
                RedrawObjectInfoMenu(_selectedMarker, true);
                _changesMade++;
            }

            if (Game.IsControlJustPressed(Control.CreatorDelete))
            {
                PropStreamer.Markers.Remove(_selectedMarker);
                RemoveMarkerFromEntityMenu(_selectedMarker.Id);
                _selectedMarker = null;
                SetMenuVisible(_objectInfoMenu, false);
                _mainCamera.StopPointing();
                _changesMade++;
            }

            if (_selectedMarker != null && (Game.IsControlJustPressed(Control.PhoneCancel) || Game.IsControlJustPressed(Control.Attack)))
            {
                _selectedMarker = null;
                SetMenuVisible(_objectInfoMenu, false);
                _mainCamera.StopPointing();
                _changesMade++;
            }

            DrawButtons(_selectedButtons);
        }
    }
}
