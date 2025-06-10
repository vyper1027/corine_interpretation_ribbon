using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.CIM;
using System;
using ArcGIS.Core.Internal.Geometry;

namespace ProAppModule2.UI.MapTools
{
    class ExplodeTool : MapTool
    {
        public ExplodeTool() : base()
        {
            QueuedTask.Run(() =>
            {
                MapView mapView = MapView.Active;
                if (mapView != null)
                {
                    mapView.Map?.SetSelection(null);
                }
            });

            SketchType = SketchGeometryType.Point | SketchGeometryType.Rectangle;
            IsSketchTool = true;
            SketchOutputMode = SketchOutputMode.Map;

            Utils.SendMessageToDockPane("Herramienta de explosión activada. Haga clic o dibuje un rectángulo para seleccionar polígonos.");
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            Utils.SendMessageToDockPane("Selección completada. Iniciando proceso de explosión...\n");
            return QueuedTask.Run(() => ExecuteExplodeAsync(geometry));
        }

        protected async Task<bool> ExecuteExplodeAsync(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                Utils.SendMessageToDockPane("⚠️ Geometría no válida.");
                return false;
            }

            var editableLayers = ActiveMapView?.Map?.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(lyr => lyr.CanEditData() && lyr.ShapeType == esriGeometryType.esriGeometryPolygon)
                .ToList();

            if (editableLayers == null || !editableLayers.Any())
            {
                Utils.SendMessageToDockPane("❌ No hay capas poligonales editables disponibles.\n");
                return false;
            }

            return await QueuedTask.Run(() =>
            {
                try
                {
                    foreach (var editableLayer in editableLayers)
                    {
                        Table featureClass = editableLayer.GetTable();
                        var selectedOIDs = new List<long>();

                        var spatialQueryFilter = new SpatialQueryFilter
                        {
                            FilterGeometry = geometry,
                            SpatialRelationship = geometry.GeometryType == GeometryType.Point
                                ? SpatialRelationship.Contains
                                : SpatialRelationship.Intersects
                        };

                        using (var rowCursor = featureClass.Search(spatialQueryFilter, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (var feature = rowCursor.Current as Feature)
                                {
                                    if (feature?.GetShape() is Polygon poly && poly.Parts.Count > 1)
                                        selectedOIDs.Add(feature.GetObjectID());
                                }
                            }
                        }

                        if (selectedOIDs.Count == 0)
                        {
                            Utils.SendMessageToDockPane($"ℹ️ No se encontraron polígonos multipart en la capa {editableLayer.Name}.\n", true);
                            continue;
                        }

                        EditOperation splitOperation = new EditOperation()
                        {
                            Name = "Dividir polígono multipart en entidades separadas",
                            ProgressMessage = "Separando polígonos...",
                            CancelMessage = "Operación cancelada.",
                            ErrorMessage = "Error al separar polígonos",
                            SelectModifiedFeatures = true
                        };

                        var queryFilter = new QueryFilter
                        {
                            WhereClause = $"OBJECTID IN ({string.Join(",", selectedOIDs)})"
                        };

                        using (var rowCursor = featureClass.Search(queryFilter, false))
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (var feature = rowCursor.Current as Feature)
                                {
                                    var polyGeometry = feature?.GetShape() as Polygon;
                                    if (polyGeometry == null || polyGeometry.Parts.Count == 0) continue;

                                    // Validar campos válidos (evitar nulos o dominios restringidos)
                                    var systemFields = new HashSet<string> { "OBJECTID", "Shape_Length", "Shape_Area", "Shape" };
                                    var originalAttributes = new Dictionary<string, object>();
                                    foreach (var field in feature.GetFields())
                                    {
                                        string name = field.Name;
                                        if (systemFields.Contains(name)) continue;

                                        var value = feature[name];
                                        originalAttributes[name] = (value == DBNull.Value) ? null : value;
                                    }


                                    foreach (var part in polyGeometry.Parts)
                                    {
                                        Polygon newPolygon = null;
                                        try
                                        {
                                            newPolygon = PolygonBuilder.CreatePolygon(part, polyGeometry.SpatialReference);
                                        }
                                        catch (Exception ex)
                                        {
                                            Utils.SendMessageToDockPane($"⚠️ Error al crear polígono: {ex.Message}", true);
                                            continue;
                                        }

                                        // Verificar superposición
                                        var overlapFilter = new SpatialQueryFilter
                                        {
                                            FilterGeometry = newPolygon,
                                            SpatialRelationship = SpatialRelationship.Intersects,
                                            WhereClause = $"OBJECTID <> {feature.GetObjectID()}"
                                        };

                                        var overlappingGeometries = new List<Polygon>();
                                        using (var overlapCursor = featureClass.Search(overlapFilter, false))
                                        {
                                            while (overlapCursor.MoveNext())
                                            {
                                                using (var other = overlapCursor.Current as Feature)
                                                {
                                                    if (other?.GetShape() is Polygon otherGeom &&
                                                        GeometryEngine.Instance.Intersects(newPolygon, otherGeom))
                                                    {
                                                        overlappingGeometries.Add(otherGeom);
                                                    }
                                                }
                                            }
                                        }

                                        try
                                        {
                                            if (overlappingGeometries.Any())
                                            {
                                                var unionOverlap = GeometryEngine.Instance.Union(overlappingGeometries) as Polygon;
                                                var clippedPolygon = GeometryEngine.Instance.Difference(newPolygon, unionOverlap) as Polygon;

                                                if (clippedPolygon != null && !clippedPolygon.IsEmpty)
                                                    splitOperation.Create(editableLayer, clippedPolygon, originalAttributes);
                                            }
                                            else
                                            {
                                                splitOperation.Create(editableLayer, newPolygon, originalAttributes);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Utils.SendMessageToDockPane($"⚠️ Error en operación geométrica: {ex.Message}", true);
                                            continue;
                                        }
                                    }

                                    // Eliminar el original
                                    //Utils.SendMessageToDockPane($"Eliminando OID {feature.GetObjectID()}", true);
                                    splitOperation.Delete(editableLayer, feature.GetObjectID());

                                }
                            }
                        }

                        if (!splitOperation.Execute())
                            Utils.SendMessageToDockPane($"❌ Error en la separación de la capa {editableLayer.Name}.", true);
                        else
                            Utils.SendMessageToDockPane($"✅ Separación completada en la capa {editableLayer.Name}.\n", true);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❗ Error general: {ex.Message}", true);
                    return false;
                }
            });
        }

        // Función auxiliar para valores por defecto en campos
        private object GetDefaultValue(Field f)
        {
            return f.FieldType switch
            {
                FieldType.String => "",
                FieldType.Integer => 0,
                FieldType.Double => 0.0,
                FieldType.Single => 0.0f,
                FieldType.Date => DateTime.Now,
                FieldType.SmallInteger => (short)0,
                _ => null
            };
        }


        protected override async Task<bool> OnSketchModifiedAsync()
        {
            Polygon sketchGeometry = await base.GetCurrentSketchAsync() as Polygon;

            await QueuedTask.Run(() =>
            {
                if (sketchGeometry != null && sketchGeometry.PointCount > 3)
                {
                    var symbolReference = base.SketchSymbol;
                    if (symbolReference == null)
                    {
                        var cimPolygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                            ColorFactory.Instance.CreateRGBColor(255, 0, 0, 50));
                        base.SketchSymbol = cimPolygonSymbol.MakeSymbolReference();
                    }
                    else
                    {
                        symbolReference.Symbol.SetColor(ColorFactory.Instance.CreateRGBColor(255, 0, 0, 50));
                        base.SketchSymbol = symbolReference;
                    }
                }
            });

            Utils.SendMessageToDockPane("Dibujando selección, ajuste si es necesario.");
            return true;
        }
    }
}
