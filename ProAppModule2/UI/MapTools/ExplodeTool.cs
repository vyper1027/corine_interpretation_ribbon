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
            // Ejecutar la operación de limpieza de selección en el hilo adecuado.
            QueuedTask.Run(() =>
            {
                MapView mapView = MapView.Active;
                if (mapView != null)
                {
                    mapView.Map?.SetSelection(null); // Limpiar cualquier selección activa
                }
            });

            SketchType = SketchGeometryType.Rectangle;
            IsSketchTool = true;
            SketchOutputMode = SketchOutputMode.Map;

            Utils.SendMessageToDockPane("Herramienta de explosión activada. Dibuje una línea para seleccionar polígonos.");
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

                    var spatialQueryFilter = new SpatialQueryFilter
                    {
                        FilterGeometry = geometry,
                        SpatialRelationship = SpatialRelationship.Intersects
                    };

                    using (var rowCursor = featureClass.Search(spatialQueryFilter, false))
                    {
                        while (rowCursor.MoveNext())
                        {
                            using (var feature = rowCursor.Current as Feature)
                            {
                                var polyGeometry = feature.GetShape() as Polygon;
                                if (polyGeometry != null && polyGeometry.Parts.Count > 1) // Solo procesar si es multipart
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

                    // Filtrar solo las filas con los OID seleccionados
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
                                    .Where(f => !systemFields.Contains(f.Name))  // Excluir los campos del sistema
                                    .ToDictionary(f => f.Name, f => feature[f.Name]);

                                foreach (var part in polyGeometry.Parts)
                                {
                                    var newPolygon = PolygonBuilder.CreatePolygon(part);
                                    splitOperation.Create(editableLayer, newPolygon, originalAttributes);                                    
                                }

                                // Eliminar el polígono original después de crear los nuevos
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
