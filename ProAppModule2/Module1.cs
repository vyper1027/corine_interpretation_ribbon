using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ProAppModule2.UI.Buttons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProAppModule2
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        //public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("ProAppModule2_Module");

        public static Module1 Current
        {
            get
            {
                return _this ?? (_this = (Module1)FrameworkApplication.FindModule("ProAppModule2_Module"));
            }
        }

        #region Static Properties

        private static Inspector _attributeInspector = null;

        internal static Inspector AttributeInspector
        {
            get { return _attributeInspector; }
            set { _attributeInspector = value; }
        }

        private static CustomDockpaneViewModel _attributeViewModel = null;

        internal static CustomDockpaneViewModel AttributeViewModel
        {
            get { return _attributeViewModel; }
            set { _attributeViewModel = value; }
        }

        #endregion Static Properties

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

        #region Toggle State
        /// <summary>
        /// Activate or Deactivate the specified state. State is identified via
        /// its name. Listen for state changes via the DAML <b>condition</b> attribute
        /// </summary>
        /// <param name="stateID"></param>
        public static void ToggleState(string stateID)
        {
            if (FrameworkApplication.State.Contains(stateID))
            {
                FrameworkApplication.State.Deactivate(stateID);
            }
            else
            {
                FrameworkApplication.State.Activate(stateID);
            }
        }

        #endregion Toggle State

        #region Business Logic
        private ModValueToSetcl2018 _attributEditBox = null;
        public ModValueToSetcl2018 ModValueToSetcl20181
        {
            get; set;
            //get { return _attributEditBox; }
            //set { _attributEditBox = value; }

        }
        private Reviewer _attributEditBoxr = null;
        public Reviewer Reviewer1
        {
            //get; set;
            get { return _attributEditBoxr; }
            set { _attributEditBoxr = value; }

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
            QueuedTask.Run(() =>
            {
                //Project.Current.SetIsEditingEnabledAsync(true);
                // Get the layer selected in the Contents pane, and prompt if there is none:
                //if (MapView.Active.GetSelectedLayers().Count == 0)
                //{
                //    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No feature layer selected in Contents pane. Exiting...", "Info");
                //    return;
                //}
                // Check to see if there is a selected feature layer
                const string layer = "Vectores_Cambios_18_20";
                var featLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layer));

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

                //if (featSelectionOIDs.Count > 1)
                //{
                //    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Solo debe seleccionar un poligono, " + featLayer.Name + ". Proceda con la selecciòn...", "Info");
                //    return;
                //}

                Project.Current.SetIsEditingEnabledAsync(true);
                // Get the name of the attribute to update, and the value to set:
                string attributename = "Estado";
                //attributename = attributename.ToUpper();
                string setvalue = "Eliminar";

                // Display all the parameters for the update:              

                Utils.SendMessageToDockPane($"Registro a actualizar:  " +
                    "\r\n Capa: " + featLayer.Name +
                    "\r\n ID: " + featSelectionOIDs.ToList()[0] +
                    "\r\n Numero de registros: " + Convert.ToString(featSelectionOIDs.Count) +
                    "\r\n Valor a actualizar: " + Convert.ToString(setvalue));

                try
                {
                    // Now ready to do the actual editing:
                    // 1. Create a new edit operation and a new inspector for working with the attributes
                    // 2. Check to see if a valid field name was chosen for the feature layer
                    // 3. If so, apply the edit

                    //
                    var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                    inspector.Load(featLayer, featSelectionOIDs);
                    if (inspector.HasAttributes && inspector.Count(a => a.FieldName == attributename) > 0)
                    {
                        inspector[attributename] = setvalue;
                        var editOp = new EditOperation();
                        editOp.Name = "Edit " + featLayer.Name + ", " + Convert.ToString(featSelectionOIDs.Count) + " records.";
                        editOp.Modify(inspector);
                        editOp.ExecuteAsync();

                        Utils.SendMessageToDockPane("Actualizacion Completada.", true);
                        Utils.SendMessageToDockPane("El registro fue eliminado", true);
                        MapView.Active.Map.ClearSelection();
                    }
                    else
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("The Attribute provided is not valid. " +
                            "\r\n Ensure your attribute name is correct.", "Invalid attribute");
                        // return;
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


        public void ApproveValues()
        {
            
            //  This sample is intended for use with a featureclass with a default text field named "Description".
            //  You can replace "Description" with any field name that works for your dataset
            if (MapView.Active == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No MapView currently active. Exiting...", "Info");
                return;
            }
            QueuedTask.Run(() =>
            {              
                // Check to see if there is a selected feature layer
                const string layer = "Vectores_Cambios_18_20";
                var featLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layer));
                
                if (featLayer == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No esta cargada la capa de cambios...", "Info");
                    return;
                }

                // Get the selected records, and check/exit if there are none:
                var featSelectionOIDs = featLayer.GetSelection().GetObjectIDs();
                if (featSelectionOIDs.Count == 0)
                {                    
                    Utils.SendMessageToDockPane($"No ha seleccionado ningún registro en {featLayer.Name}. Proceda con la selección...");
                    return;
                }

                if (featSelectionOIDs.Count > 1)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Solo debe seleccionar un poligono, " + featLayer.Name + ". Proceda con la selecciòn...", "Info");
                    Utils.SendMessageToDockPane($"Solo debe seleccionar un poligono, " + featLayer.Name + ". Proceda con la selecciòn...");
                    return;
                }

                Project.Current.SetIsEditingEnabledAsync(true);
                // Get the name of the attribute to update, and the value to set:
                string attributename = "Estado";                
                string setvalue = "Aprobado";                

                Utils.SendMessageToDockPane($"Registro a actualizar:  " +
                    "\r\n Capa: " + featLayer.Name +
                    "\r\n ID: " + featSelectionOIDs.ToList()[0] +
                    "\r\n Numero de registros: " + Convert.ToString(featSelectionOIDs.Count) +
                    "\r\n Valor a actualizar: " + Convert.ToString(setvalue));

                try
                {
                    // Now ready to do the actual editing:
                    // 1. Create a new edit operation and a new inspector for working with the attributes
                    // 2. Check to see if a valid field name was chosen for the feature layer
                    // 3. If so, apply the edit
                    
                    var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                    inspector.Load(featLayer, featSelectionOIDs);
                    if (inspector.HasAttributes && inspector.Count(a => a.FieldName == attributename) > 0)
                    {
                        inspector[attributename] = setvalue;
                        var editOp = new EditOperation();
                        editOp.Name = "Edit " + featLayer.Name + ", " + Convert.ToString(featSelectionOIDs.Count) + " records.";
                        editOp.Modify(inspector);
                        editOp.ExecuteAsync();


                        Utils.SendMessageToDockPane("Actualizacion Completada.", true);
                        Utils.SendMessageToDockPane("Registro actualizado", true);

                        MapView.Active.Map.ClearSelection();
                    }
                    else
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("The Attribute provided is not valid. " +
                            "\r\n Ensure your attribute name is correct.", "Invalid attribute");
                        // return;
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
        #endregion Business Logic

    }
}
