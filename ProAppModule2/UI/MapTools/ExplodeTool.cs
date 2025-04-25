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
            Utils.SendMessageToDockPane("Selección completada. Iniciando proceso de explosión...");
            return QueuedTask.Run(() => ExecuteExplodeAsync(geometry));
        }

        protected async Task<bool> ExecuteExplodeAsync(Geometry geometry)
        {
            if (geometry == null)
            {
                Utils.SendMessageToDockPane("No se encontró geometría válida.");
                return false;
            }

            var editableLayers = ActiveMapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(lyr => lyr.CanEditData() && lyr.ShapeType == esriGeometryType.esriGeometryPolygon)
                .ToList();

            if (!editableLayers.Any())
            {
                Utils.SendMessageToDockPane("No hay capas poligonales editables disponibles.");
                return false;
            }

            return await QueuedTask.Run(() =>
            {
                foreach (var editableLayer in editableLayers)
                {
                    Table featureClass = editableLayer.GetTable();
                    var selectedOIDs = new List<long>();

                    // Ajuste dinámico según el tipo de geometría
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
                                var polyGeometry = feature.GetShape() as Polygon;
                                if (polyGeometry != null && polyGeometry.Parts.Count > 1)
                                {
                                    selectedOIDs.Add(feature.GetObjectID());
                                }
                            }
                        }
                    }

                    if (selectedOIDs.Count == 0)
                    {
                        Utils.SendMessageToDockPane($"No se encontraron polígonos multipart en la capa {editableLayer.Name}.");
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
                                var polyGeometry = feature.GetShape() as Polygon;
                                if (polyGeometry == null) continue;

                                var systemFields = new HashSet<string> { "OBJECTID", "Shape_Length", "Shape_Area", "Shape" };
                                var originalAttributes = feature.GetFields()
                                    .Where(f => !systemFields.Contains(f.Name))
                                    .ToDictionary(f => f.Name, f => feature[f.Name]);

                                foreach (var part in polyGeometry.Parts)
                                {
                                    var newPolygon = PolygonBuilder.CreatePolygon(part);

                                    // Verificar si se superpone con otras entidades
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
                                            using (var otherFeature = overlapCursor.Current as Feature)
                                            {
                                                var otherGeom = otherFeature.GetShape() as Polygon;
                                                if (GeometryEngine.Instance.Intersects(newPolygon, otherGeom))
                                                {
                                                    overlappingGeometries.Add(otherGeom);
                                                }
                                            }
                                        }
                                    }

                                    if (overlappingGeometries.Any())
                                    {
                                        var unionOverlap = GeometryEngine.Instance.Union(overlappingGeometries) as Polygon;
                                        var clippedPolygon = GeometryEngine.Instance.Difference(newPolygon, unionOverlap) as Polygon;

                                        if (clippedPolygon != null && !clippedPolygon.IsEmpty)
                                            splitOperation.Create(editableLayer, clippedPolygon, originalAttributes);
                                        else
                                            Utils.SendMessageToDockPane("Parte omitida por solapamiento completo.");
                                    }
                                    else
                                    {
                                        splitOperation.Create(editableLayer, newPolygon, originalAttributes);
                                    }
                                }

                                splitOperation.Delete(editableLayer, feature.GetObjectID());
                            }
                        }
                    }

                    bool result = splitOperation.Execute();
                    if (result)
                        Utils.SendMessageToDockPane($"Separación completada en la capa {editableLayer.Name}.");
                    else
                        Utils.SendMessageToDockPane($"Error en la separación de la capa {editableLayer.Name}.");
                }

                return true;
            });
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
