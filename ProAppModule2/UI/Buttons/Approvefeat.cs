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
using ProAppModule2.Geoprocessing;

namespace ProAppModule2.UI.Buttons
{
    internal class Approvefeat : Button
    {
        private long selectedFeatureID;
        protected override void OnClick()
        {            
            ApproveValues();
            
            //FeatureUtils.InsertGeometryToCorineLayer();
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
            QueuedTask.Run(async () =>
            {
                // Check to see if there is a selected feature layer               
                var featLayer = await Utils.GetDynamicLayer("vectoresDeCambio");

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

                if (featSelectionOIDs.Count > 90)
                {
                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("Solo debe seleccionar un poligono, " + featLayer.Name + ". Proceda con la selecciòn...", "Info");
                    Utils.SendMessageToDockPane($"Solo se puedan aprobar grupos de maximo 90 poligonos, " + featLayer.Name + ". Proceda con la selecciòn...");
                    return;
                }

                await Project.Current.SetIsEditingEnabledAsync(true);

                selectedFeatureID = featSelectionOIDs.ToList()[0];
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
                        await editOp.ExecuteAsync();


                        //Utils.SendMessageToDockPane("Actualizacion Completada.", true);
                        //Utils.SendMessageToDockPane("Registro actualizado", true);

                        //MapView.Active.Map.ClearSelection();

                        var targetLayer = await Utils.GetDynamicLayer("capaCorine");

                        await InsertSelectedFeaturesIntoCorine(featLayer, targetLayer, featSelectionOIDs);
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
        public async Task InsertSelectedFeaturesIntoCorine(FeatureLayer sourceLayer, FeatureLayer targetLayer, IReadOnlyList<long> oids)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var sourceTable = sourceLayer.GetTable())
                    using (var targetTable = targetLayer.GetTable())
                    using (var rowCursor = sourceTable.Search(new QueryFilter { ObjectIDs = oids }, false))
                    {
                        var createFeatures = new EditOperation() { Name = "Copiar entidades a Corine" };

                        // Iterar sobre las entidades seleccionadas en la capa de origen
                        while (rowCursor.MoveNext())
                        {
                            using (var row = rowCursor.Current)
                            {
                                var shape = row["SHAPE"] as Geometry; // Obtener la geometría de la entidad
                                var codigo = row["Codigo_cobertura"]; //Obtener equivalencia del codigo de cobertura
                                // Crear el diccionario de atributos con valores null por defecto
                                var attributes = new Dictionary<string, object>
                                {
                                    { "SHAPE", shape },
                                    { "apoyo", null },
                                    { "area_ha", null },
                                    { "cambio", null },
                                    { "codigo", codigo },
                                    { "confiabili", null },
                                    { "insumo", null },
                                    { "leyenda", null },
                                    { "nivel_1", null },
                                    { "nivel_2", null },
                                    { "nivel_3", null },
                                    { "nivel_4", null },
                                    { "nivel_5", null },
                                    { "nivel_6", null }
                                };

                                // Crear la nueva entidad en la capa Corine
                                createFeatures.Create(targetTable, attributes);
                            }
                        }

                        // Ejecutar la operación de creación
                        if (!createFeatures.IsEmpty)
                        {
                            var success = createFeatures.Execute();
                            if (!success)
                            {
                                Utils.SendMessageToDockPane("Error al copiar los polígonos.");
                            }
                            else
                            {
                                Utils.SendMessageToDockPane("Polígonos copiados exitosamente a capa Corine.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"Error: {ex.Message}");
                }
            });
        }


    }
}
