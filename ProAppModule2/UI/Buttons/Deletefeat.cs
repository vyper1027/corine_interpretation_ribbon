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
    internal class Deletefeat : Button
    {
        protected override void OnClick()
        {
            UpdateValues();
        }

        public void UpdateValues()
        {

            //  This sample is intended for use with a featureclass with a default text field named "Description".
            //  You can replace "Description" with any field name that works for your dataset
            if (MapView.Active == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No MapView currently active. Exiting...", "Info");
                return;
            }
            QueuedTask.Run(async () =>
            {               
                // Check to see if there is a selected feature layer
                var featLayer = await Utils.GetDynamicLayer("vectoresDeCambio");

                //var featLayer = MapView.Active.GetSelectedLayers().First() as FeatureLayer;
                if (featLayer == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No esta cargada la capa de cambios...", "Info");
                    return;
                }

                // Get the selected records, and check/exit if there are none:
                var featSelectionOIDs = featLayer.GetSelection().GetObjectIDs();
                if (featSelectionOIDs.Count == 0)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No ha seleccionado ningun registro, " + featLayer.Name + ". Proceda con la selecciòn...", "Info");
                    return;
                }               

                await Project.Current.SetIsEditingEnabledAsync(true);
                // Get the name of the attribute to update, and the value to set:
                string attributename = "Estado";
                //attributename = attributename.ToUpper();
                string setvalue = "Eliminado";

                // Display all the parameters for the update:              

                Utils.SendMessageToDockPane($"Registro a actualizar:  " +
                    "\r\n Capa: " + featLayer.Name +
                    "\r\n ID: " + featSelectionOIDs.ToList()[0] +
                    "\r\n Numero de registros: " + Convert.ToString(featSelectionOIDs.Count) +
                    "\r\n Valor a actualizar: " + Convert.ToString(setvalue));

                try
                {
                    var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                    inspector.Load(featLayer, featSelectionOIDs);
                    if (inspector.HasAttributes && inspector.Count(a => a.FieldName == attributename) > 0)
                    {
                        inspector[attributename] = setvalue;
                        var editOp = new EditOperation();
                        editOp.Name = "Edit " + featLayer.Name + ", " + Convert.ToString(featSelectionOIDs.Count) + " records.";
                        editOp.Modify(inspector);
                        await editOp.ExecuteAsync();

                        Utils.SendMessageToDockPane("Actualizacion Completada.", true);
                        Utils.SendMessageToDockPane("El registro fue eliminado", true);
                        MapView.Active.Map.ClearSelection();
                    }
                    else
                    {
                        Utils.SendMessageToDockPane("The Attribute provided is not valid.\r\nEnsure your attribute name is correct.");
                    }
                }
                catch (Exception exc)
                {
                    // Catch any exception found and display a message box.
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Exception caught while trying to perform update: " + exc.Message);
                    return;
                }
            });
        }
    }
}
