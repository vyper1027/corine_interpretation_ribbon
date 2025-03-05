using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ProAppModule2.UI.DockPanes;

namespace ProAppModule2.UI.MapTools
{
    internal class MapTool1 : MapTool
    {
        public MapTool1()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;
            SketchOutputMode = SketchOutputMode.Screen;
        }

        protected override Task OnToolActivateAsync(bool hasMapViewChanged)
        {
            CustomDockpaneViewModel.Show();
            return Task.FromResult(true);
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            CustomDockpaneViewModel.Show();
            QueuedTask.Run(async () =>
            {
                var activeMapView = MapView.Active;
                if (activeMapView == null)
                {
                    Utils.SendMessageToDockPane("No hay una ventana de mapa activa.");
                    return;
                }

                var activeMap = activeMapView.Map;
                if (activeMap == null)
                {
                    Utils.SendMessageToDockPane("No se encontró el mapa activo.");
                    return;
                }

                activeMapView.SelectFeatures(geometry);
                var results = activeMapView.GetFeatures(geometry);

                if (results.Count == 0)
                {
                    Utils.SendMessageToDockPane("Seleccione uno o varios polígonos para comenzar...");
                    return;
                }

                var layer1 = await Utils.GetDynamicLayer("vectoresDeCambio");
                var layer2 = await Utils.GetDynamicLayer("capaCorine");

                var layerName1 = layer1?.Name ?? "No se encontró la capa 1";
                var layerName2 = layer2?.Name ?? "No se encontró la capa 2";

                foreach (var kvp in results.ToDictionary())
                {
                    var featLyr = kvp.Key as BasicFeatureLayer;
                    if (featLyr == null) continue;

                    if (featLyr.Name == layerName1 || featLyr.Name == layerName2)
                    {
                        var nc = kvp.Value.Count();
                        if (nc >= 1 && nc <= 90)
                        {
                            var featureLayer = activeMap.GetLayersAsFlattenedList()
                                                       .OfType<FeatureLayer>()
                                                       .FirstOrDefault(fl => fl.Name.Equals(featLyr.Name));

                            if (featureLayer == null)
                            {
                                Utils.SendMessageToDockPane($"No se encontró la capa {featLyr.Name} en el mapa activo.");
                                continue;
                            }

                            var qf = new QueryFilter() { ObjectIDs = kvp.Value };
                            var rowCursor = featLyr.Search(qf);

                            while (rowCursor.MoveNext())
                            {
                                using (var feat = rowCursor.Current as Feature)
                                {
                                    var oids = kvp.Value.ToList();
                                    Utils.SendMessageToDockPane($"Ha seleccionado {nc} polígonos, continúe con el proceso...");

                                    var inspector = Module1.AttributeInspector;
                                    inspector.Load(featLyr, kvp.Value);
                                    Module1.AttributeViewModel.Heading = $"{featLyr.Name} - {oids[0]}";

                                    foreach (var attrib in inspector)
                                    {
                                        var value = attrib.CurrentValue.ToString();
                                        Debug.WriteLine(value);
                                    }
                                }
                            }
                        }
                        else
                        {
                            Utils.SendMessageToDockPane($"En la capa {featLyr.Name} fueron seleccionados {nc} registros. Por favor, seleccione entre 1 y 90.");
                        }
                    }
                }
            });
            return Task.FromResult(true);
        }
    }
}
