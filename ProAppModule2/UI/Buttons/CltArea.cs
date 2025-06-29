﻿using ArcGIS.Core.CIM;
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
using ArcGIS.Desktop.Internal.Mapping.CommonControls;
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
                QueuedTask.Run(async () =>
                {
                    var layer1 = await Utils.GetDynamicLayer("vectoresDeCambio");
                    var layer2 = await Utils.GetDynamicLayer("capaCorine");
                    var layer3 = await Utils.GetDynamicLayer("recorteEntrega");

                    var layerName1 = layer1?.Name ?? "No se encontro la capa 1";
                    var layerName2 = layer2?.Name ?? "No se encontro la capa 2";
                    var layerName3 = layer3?.Name ?? "No se encontro la capa 2";


                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        Utils.SendMessageToDockPane("No hay un mapa activo.");
                        return;
                    }

                    var featureLayer1 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName1));
                    var featureLayer2 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName2));
                    var featureLayer3 = map.GetLayersAsFlattenedList()
                                           .OfType<FeatureLayer>()
                                           .FirstOrDefault(fl => fl.Name.Equals(layerName3));

                    if (featureLayer1 == null && featureLayer2 == null && featureLayer3 == null)
                    {
                        Utils.SendMessageToDockPane($"No se encontraron las capas '{layerName1}' ni '{layerName2}'.");
                        return;
                    }

                    if (featureLayer1 != null && featureLayer1.GetSelection().GetCount() > 0)
                    {
                        CalcularArea(featureLayer1, layerName1);
                    }
                    else if (featureLayer2 != null && featureLayer2.GetSelection().GetCount() > 0)
                    {
                        CalcularArea(featureLayer2, layerName2);
                    }
                    else if (featureLayer3 != null && featureLayer3.GetSelection().GetCount() > 0)
                    {
                        CalcularArea(featureLayer3, layerName3);
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
                SpatialReference targetSR = SpatialReferenceBuilder.CreateSpatialReference(103599);

                using (var rowCursor = featureLayer.Search(new QueryFilter { ObjectIDs = selectedOIDs }))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (var feat = rowCursor.Current as Feature)
                        {
                            if (feat != null)
                            {
                                var shape = feat.GetShape();
                                if (shape == null) continue;

                                var sr = shape.SpatialReference;
                                if (sr == null || sr.Wkid != 7399)
                                {
                                    shape = GeometryEngine.Instance.Project(shape, targetSR);
                                }

                                double area = GeometryEngine.Instance.Area(shape);
                                totalArea += area;
                            }
                        }
                    }
                }

                double totalAreaHa = totalArea / 10000;
                string message = $"Capa: {layerName}\n" +
                                 $"Área total seleccionada: {Math.Round(totalAreaHa, 3)} ha\n" +
                                 $"{Math.Round(totalArea, 3)} m²" + "\nWKID 9377";
                Utils.SendMessageToDockPane(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error al calcular el área");
            }
        }
    }
}
