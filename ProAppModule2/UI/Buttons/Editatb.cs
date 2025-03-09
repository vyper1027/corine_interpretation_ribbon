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
    internal class Editatb : Button
    {
        protected override void OnClick()
        {

            if (MapView.Active == null)
            {
                MessageBox.Show("No MapView currently active. Exiting...", "Info");
                return;
            }
            QueuedTask.Run(async () =>
            {
                var layerName = "";
                // Get the layer selected in the Contents pane, and prompt if there is none:
                var map = MapView.Active?.Map;
                                
                if (map.Name == "Ventana1")
                {
                    var layer1 = await Utils.GetDynamicLayer("vectoresDeCambio");
                    layerName = layer1?.Name ?? "No se encontro la capa 1";

                } else if (map.Name == "Ventana2")
                {
                    var layer2 = await Utils.GetDynamicLayer("capaCorine");
                    layerName = layer2?.Name ?? "No se encontro la capa 1";
                }
                // Check to see if there is a selected feature layer                
                var featLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layerName));

                //var featLayer = MapView.Active.GetSelectedLayers().First() as FeatureLayer;
                if (featLayer == null)
                {
                    MessageBox.Show("No esta cargada la capa de cambios...", "Info");
                    return;
                }
                // Get the selected records, and check/exit if there are none:
                var featSelectionOIDs = featLayer.GetSelection().GetObjectIDs();
                if (featSelectionOIDs.Count == 0)
                {
                    MessageBox.Show("No ha seleccionado ningun registro, " + featLayer.Name + ". Proceda con la selecciòn...", "Info");
                    return;
                }

                if (featSelectionOIDs.Count > 1)
                {
                    MessageBox.Show("Solo debe seleccionar un poligono, " + featLayer.Name + ". Proceda con la selecciòn...", "Info");
                    return;
                }

                await Project.Current.SetIsEditingEnabledAsync(true);
                Module1.ToggleState("controls_atb");
            });
        }
    }
}
