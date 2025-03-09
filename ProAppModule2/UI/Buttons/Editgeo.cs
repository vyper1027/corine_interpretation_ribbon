using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ProAppModule2.UI.Buttons
{
    internal class Editgeo : Button
    {
        protected override void OnClick()
        {
            try
            {
                QueuedTask.Run(async () =>
                {
                    var layer1 = await Utils.GetDynamicLayer("vectoresDeCambio");
                    var layer2 = await Utils.GetDynamicLayer("capaCorine");

                    var layerName1 = layer1?.Name ?? "No se encontró la capa 1";
                    var layerName2 = layer2?.Name ?? "No se encontró la capa 2";

                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        MessageBox.Show("No hay un mapa activo.", "Info");
                        return;
                    }

                    // Obtener las capas
                    var featureLayer1 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName1));
                    var featureLayer2 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName2));

                    // Validar las capas con selección
                    FeatureLayer activeLayer = null;
                    if (featureLayer1 != null && featureLayer1.GetSelection().GetCount() > 0)
                    {
                        activeLayer = featureLayer1;
                    }
                    else if (featureLayer2 != null && featureLayer2.GetSelection().GetCount() > 0)
                    {
                        activeLayer = featureLayer2;
                    }

                    if (activeLayer == null)
                    {
                        MessageBox.Show("No hay selección en ninguna capa activa.", "Info");
                        return;
                    }

                    var selectedOIDs = activeLayer.GetSelection().GetObjectIDs();
                    if (selectedOIDs.Count == 0)
                    {
                        MessageBox.Show($"No ha seleccionado ningún registro en {activeLayer.Name}.", "Info");
                        return;
                    }

                    if (selectedOIDs.Count > 1)
                    {
                        MessageBox.Show($"Debe seleccionar solo un polígono en {activeLayer.Name}.", "Info");
                        return;
                    }

                    // Habilitar la edición
                    await Project.Current.SetIsEditingEnabledAsync(true);
                    Module1.ToggleState("controls_state");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
            }
        }
    }
}
