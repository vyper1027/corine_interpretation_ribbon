using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Contracts;
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

                var layer1 = await Utils.GetDynamicLayer("vectoresDeCambio") as BasicFeatureLayer;
                var layer2 = await Utils.GetDynamicLayer("capaCorine") as BasicFeatureLayer;

                var layerName1 = layer1?.Name ?? "No se encontró la capa 1";
                var layerName2 = layer2?.Name ?? "No se encontró la capa 2";

                var keyboard = System.Windows.Input.Keyboard.Modifiers;
                var combinationMethod =
                    keyboard.HasFlag(System.Windows.Input.ModifierKeys.Shift) ? SelectionCombinationMethod.Add :
                    keyboard.HasFlag(System.Windows.Input.ModifierKeys.Control) ? SelectionCombinationMethod.Subtract :
                    SelectionCombinationMethod.New;

                // Seleccionar entidades en las capas visibles con el método combinado
                activeMapView.SelectFeatures(geometry, combinationMethod);

                // Procesar la selección
                var selection = activeMap.GetSelection();

                foreach (var kvp in selection.ToDictionary())
                {
                    var layer = kvp.Key;
                    var oids = kvp.Value;

                    if (layer is BasicFeatureLayer featLyr &&
                        (featLyr.Name == layerName1 || featLyr.Name == layerName2))
                    {
                        int nc = oids.Count;

                        if (nc >= 1 && nc <= 90)
                        {
                            Utils.SendMessageToDockPane($"Ha seleccionado {nc} polígonos, continúe con el proceso...");

                            var inspector = Module1.AttributeInspector;
                            inspector.Load(featLyr, oids);
                            Module1.AttributeViewModel.Heading = $"{featLyr.Name} - {oids.First()}";

                            using (var cursor = featLyr.Search(new QueryFilter { ObjectIDs = oids }))
                            {
                                while (cursor.MoveNext())
                                {
                                    using (var feat = cursor.Current as Feature)
                                    {
                                        foreach (var attrib in inspector)
                                        {
                                            var value = attrib.CurrentValue?.ToString();
                                            Debug.WriteLine(value);
                                        }
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
