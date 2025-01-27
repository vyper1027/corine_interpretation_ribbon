using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace ProAppModule2.UI.Buttons
{
    internal class CltArea : Button
    {
        protected override void OnClick()
        {
            try
            {
                QueuedTask.Run(() =>
                {
                    const string layerName1 = "Vectores_Cambios_18_20";
                    const string layerName2 = "cobertura_tierra_2018_BUFFER_Edicion";

                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        Utils.SendMessageToDockPane("No hay un mapa activo.");
                        return;
                    }

                    // Obtener las capas
                    var featureLayer1 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName1));
                    var featureLayer2 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName2));

                    // Validar las capas
                    if (featureLayer1 == null && featureLayer2 == null)
                    {
                        Utils.SendMessageToDockPane($"No se encontraron las capas '{layerName1}' ni '{layerName2}'.");
                        return;
                    }

                    // Priorizar la capa activa y seleccionada
                    if (featureLayer1 != null && featureLayer1.GetSelection().GetCount() > 0)
                    {
                        CalcularArea(featureLayer1, layerName1);
                    }
                    else if (featureLayer2 != null && featureLayer2.GetSelection().GetCount() > 0)
                    {
                        CalcularArea(featureLayer2, layerName2);
                    }
                    else
                    {
                        Utils.SendMessageToDockPane("No hay selección en las capas activas.");
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
            }
        }

        private void CalcularArea(FeatureLayer featureLayer, string layerName)
        {
            try
            {
                var selectedOIDs = featureLayer.GetSelection().GetObjectIDs();
                if (selectedOIDs.Count == 0)
                {
                    Utils.SendMessageToDockPane($"No hay selección en la capa '{layerName}'.");
                    return;
                }

                double totalArea = 0;

                using (var rowCursor = featureLayer.Search(new QueryFilter { ObjectIDs = selectedOIDs }))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (var feat = rowCursor.Current as Feature)
                        {
                            if (feat != null)
                            {
                                var area = feat["Shape_Area"];
                                totalArea += Convert.ToDouble(area);
                            }
                        }
                    }
                }

                double totalAreaHa = totalArea / 10000; // Convertir a hectáreas
                string message = $"Capa: {layerName}\n" +
                                 $"Área total seleccionada: {Math.Round(totalAreaHa, 3)} ha\n" +
                                 $"{Math.Round(totalArea, 3)} m²";
                Utils.SendMessageToDockPane(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error al calcular el área");
            }
        }
    }
}
