using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Events;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Editing.Events;
using System;

namespace ProAppModule2.UI.MapTools
{
    class SplitTool : MapTool
    {
        private SubscriptionToken _rowCreatedToken;
        private Dictionary<long, long> _newOidToParentOid = new();
        private Dictionary<long, Dictionary<string, object>> _originalAttributes = new();
        private HashSet<long> _currentCutOids = new();
        private Table _currentTable;

        public SplitTool()
        {
            SketchType = SketchGeometryType.Line;
            IsSketchTool = true;
            UseSnapping = true;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task OnToolActivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Dibuje la sección de corte...");
            Application.Current.MainWindow.PreviewKeyDown += OnKeyDown;
            return base.OnToolActivateAsync(hasMapViewChanged);
        }

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Seleccione una herramienta para comenzar...");
            Application.Current.MainWindow.PreviewKeyDown -= OnKeyDown;

            if (_rowCreatedToken != null)
            {
                RowCreatedEvent.Unsubscribe(_rowCreatedToken);
                _rowCreatedToken = null;
            }

            _newOidToParentOid.Clear();
            _originalAttributes.Clear();
            _currentCutOids.Clear();
            _currentTable = null;

            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.D6)
            {
                Utils.SendMessageToDockPane("F6 detectado: Realizando Snap to Vertex...");
                SnapToVertex();
                e.Handled = true;
            }
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return QueuedTask.Run(() => ExecuteCut(geometry));
        }

        private bool ExecuteCut(Geometry sketchGeometry)
        {
            if (sketchGeometry == null)
            {
                Utils.SendMessageToDockPane("❌ Geometría de corte no válida.");
                return false;
            }

            var editableLayers = ActiveMapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(l => l.CanEditData() && l.ShapeType == esriGeometryType.esriGeometryPolygon);

            if (!editableLayers.Any())
            {
                Utils.SendMessageToDockPane("❌ No hay capas de polígonos editables.");
                return false;
            }

            foreach (var layer in editableLayers)
            {                
                if (!layer.Name.Contains("_asignacion"))
                    continue;

                _currentTable = layer.GetTable();
                _newOidToParentOid.Clear();
                _originalAttributes.Clear();
                _currentCutOids.Clear();

                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = sketchGeometry,
                    SpatialRelationship = SpatialRelationship.Intersects,
                    SubFields = "*"
                };

                using (var cursor = _currentTable.Search(spatialFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        if (feature == null) continue;

                        var originalGeometry = feature.GetShape();
                        var projectedSketch = GeometryEngine.Instance.Project(sketchGeometry, originalGeometry.SpatialReference);

                        if (GeometryEngine.Instance.Intersects(projectedSketch, originalGeometry))
                        {
                            long oid = feature.GetObjectID();
                            _currentCutOids.Add(oid);

                            var attributes = new Dictionary<string, object>();
                            var fields = feature.GetTable().GetDefinition().GetFields();
                            foreach (var field in fields)
                            {
                                if (!field.IsEditable || field.FieldType == FieldType.OID || field.FieldType == FieldType.Geometry)
                                    continue;

                                attributes[field.Name] = feature[field.Name];
                            }

                            _originalAttributes[oid] = attributes;
                        }
                    }
                }

                if (_currentCutOids.Count == 0)
                {
                    Utils.SendMessageToDockPane($"⚠️ No se encontraron polígonos para cortar en: {layer.Name}");
                    continue;
                }

                var layerFields = layer.GetFeatureClass().GetDefinition().GetFields()
                    .Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

                bool hasCambioField = layerFields.Contains("cambio");
                if (!hasCambioField)
                {
                    Utils.SendMessageToDockPane($"⚠️ El campo 'cambio' no está presente en la capa {layer.Name}. No se actualizará.");
                }

                _rowCreatedToken = RowCreatedEvent.Subscribe((args) =>
                {
                    if (args.Row is Feature newFeature && newFeature.GetTable().GetName() == _currentTable.GetName())
                    {
                        var shape = newFeature.GetShape();
                        foreach (var parentOid in _currentCutOids)
                        {
                            var queryFilter = new QueryFilter { WhereClause = $"OBJECTID = {parentOid}" };

                            using (var cursor = _currentTable.Search(queryFilter, false))
                            {
                                if (cursor.MoveNext())
                                {
                                    var row = cursor.Current as Feature;
                                    if (row != null && GeometryEngine.Instance.Contains(row.GetShape(), shape))
                                    {
                                        _newOidToParentOid[newFeature.GetObjectID()] = parentOid;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }, _currentTable);

                var op = new EditOperation
                {
                    Name = "Corte de polígonos",
                    SelectModifiedFeatures = false,
                    SelectNewFeatures = false
                };

                op.Split(layer, _currentCutOids.ToList(), sketchGeometry);

                if (!op.Execute())
                {
                    Utils.SendMessageToDockPane($"❌ Error al cortar en capa {layer.Name}: {op.ErrorMessage}");
                    continue;
                }

                RowCreatedEvent.Unsubscribe(_rowCreatedToken);
                _rowCreatedToken = null;

                var updateOp = new EditOperation
                {
                    Name = "Actualizar atributos",
                    SelectNewFeatures = true
                };

                foreach (var kvp in _newOidToParentOid)
                {
                    long newOid = kvp.Key;
                    long parentOid = kvp.Value;

                    if (_originalAttributes.TryGetValue(parentOid, out var attrs))
                    {
                        var copyAttrs = new Dictionary<string, object>(attrs);
                        if (hasCambioField)
                            copyAttrs["cambio"] = 2;

                        updateOp.Modify(layer, newOid, copyAttrs);
                    }
                    else
                    {
                        Utils.SendMessageToDockPane($"⚠️ No se encontraron atributos originales para OID {parentOid}");
                    }
                }

                // Solo actualizar polígonos padres que realmente fueron cortados
                var trulyCutParentOids = _newOidToParentOid.Values.Distinct();

                foreach (var parentOid in trulyCutParentOids)
                {
                    if (_originalAttributes.TryGetValue(parentOid, out var parentAttrs))
                    {
                        var updatedAttrs = new Dictionary<string, object>(parentAttrs);
                        if (hasCambioField)
                            updatedAttrs["cambio"] = 2;

                        updateOp.Modify(layer, parentOid, updatedAttrs);
                    }
                    else
                    {
                        Utils.SendMessageToDockPane($"⚠️ No se encontraron atributos del polígono padre OID {parentOid}");
                    }
                }


                if (!updateOp.IsEmpty)
                {
                    if (!updateOp.Execute())
                        Utils.SendMessageToDockPane($"❌ Error al actualizar atributos en capa {layer.Name}: {updateOp.ErrorMessage}");
                }

                _newOidToParentOid.Clear();
                _originalAttributes.Clear();
                _currentCutOids.Clear();
                _currentTable = null;
            }

            Utils.SendMessageToDockPane("✅ Corte completado.");
            return true;
        }

        protected override async Task<bool> OnSketchModifiedAsync()
        {
            var cutGeometry = await base.GetCurrentSketchAsync() as Polyline;

            await QueuedTask.Run(() =>
            {
                if (cutGeometry?.PointCount > 2)
                {
                    var currentSymbol = base.SketchSymbol;
                    if (currentSymbol == null)
                    {
                        var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                            ColorFactory.Instance.RedRGB, 3, SimpleLineStyle.DashDotDot);
                        base.SketchSymbol = lineSymbol.MakeSymbolReference();
                    }
                    else
                    {
                        currentSymbol.Symbol.SetColor(ColorFactory.Instance.RedRGB);
                        base.SketchSymbol = currentSymbol;
                    }
                }
            });

            return true;
        }

        private void SnapToVertex()
        {
            var map = MapView.Active.Map;
            var layerSnapModes = Snapping.GetLayerSnapModes(map);
            bool vertexSnappingActive = layerSnapModes.Values.Any(modes => modes.Vertex);

            if (vertexSnappingActive)
            {
                Snapping.IsEnabled = false;
                Snapping.SnapChipEnabled = false;

                foreach (var kvp in layerSnapModes)
                {
                    kvp.Value.Vertex = false;
                    kvp.Value.Point = false;
                }

                Snapping.SetLayerSnapModes(layerSnapModes, true);
                Utils.SendMessageToDockPane("Snapping a vértices desactivado.");
            }
            else
            {
                Snapping.IsEnabled = true;
                Snapping.SnapChipEnabled = true;

                foreach (var kvp in layerSnapModes)
                {
                    kvp.Value.Vertex = true;
                    kvp.Value.Point = false;
                }

                Snapping.SetLayerSnapModes(layerSnapModes, true);
                Utils.SendMessageToDockPane("Snapping a vértices activado.");
            }
        }
    }
}
