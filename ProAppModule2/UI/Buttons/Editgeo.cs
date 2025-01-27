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

namespace ProAppModule2.UI.Buttons
{
    internal class Editgeo : Button
    {
        private bool isButtonVisible = true;
        protected override void OnClick()
        {

            if (MapView.Active == null)
            {
                MessageBox.Show("No MapView currently active. Exiting...", "Info");
                return;
            }
            QueuedTask.Run(() =>
            {

                // Get the layer selected in the Contents pane, and prompt if there is none:              
                // Check to see if there is a selected feature layer
                const string layer = "Vectores_Cambios_18_20";
                var featLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layer));

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

                Project.Current.SetIsEditingEnabledAsync(true);
                Module1.ToggleState("controls_state");

            });

        }
    }
}
