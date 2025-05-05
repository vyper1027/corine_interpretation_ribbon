//Copyright 2015-2016 Esri

//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//       https://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Editing.Attributes;
using System;

namespace ProAppModule2.UI.MapTools
{
    class MergeTool : MapTool
    {
        public MergeTool() : base()
        {
            SketchType = SketchGeometryType.Line;
            IsSketchTool = true;
            SketchOutputMode = SketchOutputMode.Map;

            Utils.SendMessageToDockPane("Herramienta de unión activada. Dibuje una línea para seleccionar polígonos.");
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            Utils.SendMessageToDockPane("Selección completada. Iniciando proceso de unión...");
            return QueuedTask.Run(() => ExecuteMerge(geometry));
        }

        protected Task<bool> ExecuteMerge(Geometry geometry)
        {
            if (geometry == null)
            {
                Utils.SendMessageToDockPane("No se encontró geometría válida. Cancelando operación.");
                return Task.FromResult(false);
            }

            var editableLayers = ActiveMapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(lyr => lyr.CanEditData() && lyr.ShapeType == esriGeometryType.esriGeometryPolygon);

            if (!editableLayers.Any())
            {
                Utils.SendMessageToDockPane("No hay capas poligonales editables disponibles.");
                return Task.FromResult(false);
            }

            Utils.SendMessageToDockPane("Capas editables identificadas, comenzando el procesamiento...");

            EditOperation mergeOperation = new EditOperation()
            {
                Name = "Merge Features",
                ProgressMessage = "Uniendo polígonos...",
                CancelMessage = "Operación de unión cancelada.",
                ErrorMessage = "Error al unir polígonos",
                SelectModifiedFeatures = true
            };

            foreach (var editableLayer in editableLayers)
            {
                Table featureClass = editableLayer.GetTable();
                var selectedOIDs = new List<long>();

                var firstPolygonInfo = GetFirstIntersectedPolygon(featureClass, geometry);
                long firstFeatureOID = firstPolygonInfo.Key;
                Dictionary<string, object> firstPolygonAttributes = firstPolygonInfo.Value;

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
                            selectedOIDs.Add(feature.GetObjectID());
                        }
                    }
                }

                if (selectedOIDs.Count >= 2)
                {
                    Utils.SendMessageToDockPane($"{selectedOIDs.Count} polígonos seleccionados para la unión.");

                    var inspector = new Inspector();
                    inspector.Load(editableLayer, firstFeatureOID);

                    foreach (var kvp in firstPolygonAttributes)
                    {
                        if (!kvp.Key.Equals("cambio", StringComparison.OrdinalIgnoreCase))
                        {
                            inspector[kvp.Key] = kvp.Value;
                        }
                    }
                    // Establecer el atributo "cambio" en 2 explícitamente
                    inspector["cambio"] = 2;

                    mergeOperation.Merge(editableLayer, selectedOIDs, inspector);
                }

                else
                {
                    Utils.SendMessageToDockPane("No se encontraron suficientes polígonos para unir.");
                }
            }

            bool operationResult = mergeOperation.Execute();

            if (operationResult)
            {
                Utils.SendMessageToDockPane("Proceso de unión completado con éxito.");
            }
            else
            {
                Utils.SendMessageToDockPane("Error en el proceso de unión.");
            }

            return Task.FromResult(operationResult);
        }

        private KeyValuePair<long, Dictionary<string, object>> GetFirstIntersectedPolygon(Table featureClass, Geometry geometry)
        {
            Polyline polyline = geometry as Polyline;
            MapPoint firstPoint = polyline?.Points.FirstOrDefault();

            if (firstPoint == null)
            {
                return new KeyValuePair<long, Dictionary<string, object>>(-1, null);
            }

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
                        if (feature != null)
                        {
                            Polygon polygonGeometry = feature.GetShape() as Polygon;

                            if (polygonGeometry != null && GeometryEngine.Instance.Contains(polygonGeometry, firstPoint))
                            {
                                var attributes = new Dictionary<string, object>();

                                for (int i = 0; i < feature.GetFields().Count; i++)
                                {
                                    var field = feature.GetFields()[i];
                                    if (!field.Name.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase))
                                    {
                                        attributes[field.Name] = feature[i];
                                    }
                                }

                                return new KeyValuePair<long, Dictionary<string, object>>(feature.GetObjectID(), attributes);
                            }
                        }
                    }
                }
            }

            return new KeyValuePair<long, Dictionary<string, object>>(-1, null);
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
                            ColorFactory.Instance.CreateRGBColor(0, 255, 0, 50));
                        base.SketchSymbol = cimPolygonSymbol.MakeSymbolReference();
                    }
                    else
                    {
                        symbolReference.Symbol.SetColor(ColorFactory.Instance.CreateRGBColor(0, 255, 0, 50));
                        base.SketchSymbol = symbolReference;
                    }
                }
            });

            Utils.SendMessageToDockPane("Dibujando selección, ajuste si es necesario.");
            return true;
        }
    }
}