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
using GeoprocessingExecuteAsync;
using ArcGIS.Core.Data.Exceptions;


namespace ProAppModule2.UI.Buttons
{
    internal class Approvefeat : Button
    {
        private readonly CorineAnalysisService _analysisService;

        /// <summary>
        /// Constructor de la clase, inicializa el servicio de análisis
        /// </summary>
        public Approvefeat()
        {
            _analysisService = new CorineAnalysisService();
        }
        protected override void OnClick()        {
            
            ApproveValues();            
           
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

                var activateEditing = await Project.Current.SetIsEditingEnabledAsync(true);

                //selectedFeatureID = featSelectionOIDs.ToList()[0];
                // Get the name of the attribute to update, and the value to set:
                string attributename = "Estado";
                string setvalue = "Aprobado";

                // 📌 Mensaje inicial con la información de los registros a actualizar
                Utils.SendMessageToDockPane($"Registro a actualizar:  " +
                    "\r\n Capa: " + featLayer.Name +
                    "\r\n ID: " + featSelectionOIDs.ToList()[0] +
                    "\r\n Numero de registros: " + Convert.ToString(featSelectionOIDs.Count) +
                    "\r\n Valor a actualizar: " + Convert.ToString(setvalue));

                try
                {
                    // 📌 Crear un inspector para validar los atributos antes de la edición
                    var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
                    inspector.Load(featLayer, featSelectionOIDs);

                    // 📌 Verificar si el campo "Estado" existe
                    if (!inspector.HasAttributes || inspector.Count(a => a.FieldName == attributename) == 0)
                    {
                        Utils.SendMessageToDockPane("❌ Error: El campo 'Estado' no es válido o no existe en la capa.", true);
                        return;
                    }

                    // 📌 Verificar que todos los registros seleccionados tengan estado "Por Revisar"
                    bool allFeaturesValid = featSelectionOIDs.All(oid => inspector[attributename]?.ToString() == "Por Revisar");

                    if (!allFeaturesValid)
                    {
                        Utils.SendMessageToDockPane("❌ No se pueden copiar los polígonos porque uno o mas poligonos contienen estado Aprobado", true);
                        return;
                    }

                    // 📌 Actualizar el estado de los registros a "Aprobado"
                    inspector[attributename] = setvalue;
                    var editOp = new EditOperation
                    {
                        Name = "Editar estado en " + featLayer.Name + " (" + featSelectionOIDs.Count + " registros)"
                    };
                    editOp.Modify(inspector);
                    await editOp.ExecuteAsync();

                    var targetLayer = await Utils.GetDynamicLayer("capaCorine");

                    // ✅ Verificar si el CheckBox de Validar Topología está activado
                    bool isTopologyValidationEnabled = FrameworkApplication.State.Contains("Control_Topology_cond");
                                        
                    // 📌 Insertar los polígonos en la capa destino y obtener los nuevos ObjectIDs
                    List<long> newFeatureOIDs = await InsertSelectedFeaturesIntoCorine(featLayer, targetLayer, featSelectionOIDs);

                    if (newFeatureOIDs.Count > 0)                    
                    {
                        await ClipInsertedFeatures(targetLayer, newFeatureOIDs);
                    }
                    else
                    {
                        Utils.SendMessageToDockPane("⚠ No se encontraron nuevos IDs para recortar.");
                    }

                    // 📌 Validar la topología solo si el checkbox está activado
                    if (isTopologyValidationEnabled)
                    {
                        Utils.SendMessageToDockPane("🔍 Ejecutando validación de topología...");
                        await CorineAnalysisService.ValidateCurrentExtentTopology();
                    }
                }
                catch (Exception exc)
                {
                    Utils.SendMessageToDockPane($"❌ Error al intentar actualizar: {exc.Message}", true);
                }

            });
        }
        public async Task<List<long>> InsertSelectedFeaturesIntoCorine(FeatureLayer sourceLayer, FeatureLayer targetLayer, IReadOnlyList<long> oids)
        {
            return await QueuedTask.Run(() =>
            {
                List<long> newObjectIDs = new List<long>();
                string errorMessage = string.Empty;

                try
                {
                    using (var sourceTable = sourceLayer.GetTable())
                    using (var targetTable = targetLayer.GetTable())
                    using (var rowCursor = sourceTable.Search(new QueryFilter { ObjectIDs = oids }, false))
                    {
                        FeatureClassDefinition targetDefinition = (FeatureClassDefinition)targetTable.GetDefinition();

                        // Crear operación de edición
                        EditOperation editOperation = new EditOperation
                        {
                            Name = "Copiar entidades a Corine"
                        };

                        editOperation.Callback(context =>
                        {
                            while (rowCursor.MoveNext())
                            {
                                using (var row = rowCursor.Current)
                                using (RowBuffer rowBuffer = targetTable.CreateRowBuffer())
                                {
                                    // Copiar atributos
                                    rowBuffer[targetDefinition.GetShapeField()] = row[targetDefinition.GetShapeField()];
                                    rowBuffer["area_ha"] = row["area_ha"];
                                    rowBuffer["cambio"] = 2;
                                    rowBuffer["codigo"] = row["codigo"];
                                    rowBuffer["confiabili"] = null;
                                    rowBuffer["insumo"] = row["insumo"];
                                    rowBuffer["horaAprobacion"] = DateTime.Now;
                                    rowBuffer["apoyo"] = null;

                                    using (Feature newFeature = (Feature)targetTable.CreateRow(rowBuffer))
                                    {
                                        // Indicar que se debe actualizar la tabla
                                        context.Invalidate(newFeature);

                                        // Obtener el ObjectID de la nueva entidad
                                        long newOID = newFeature.GetObjectID();
                                        if (newOID > 0)
                                        {
                                            newObjectIDs.Add(newOID);
                                        }
                                    }
                                }
                            }
                        }, targetTable);

                        // Ejecutar la operación de edición
                        if (!editOperation.Execute())
                        {
                            errorMessage = editOperation.ErrorMessage;
                        }
                    }
                }
                catch (GeodatabaseException ex)
                {
                    errorMessage = ex.Message;
                }

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    Utils.SendMessageToDockPane($"❌ Error en la inserción: {errorMessage}");
                }
                else
                {
                    Utils.SendMessageToDockPane($"✅ Se copiaron {newObjectIDs.Count} polígonos a la capa Corine.");
                }

                return newObjectIDs;
            });
        }


        /// <summary>
        /// Recorta los polígonos insertados para evitar superposición (Overlap)
        /// </summary>
        public async Task ClipInsertedFeatures(FeatureLayer targetLayer, IReadOnlyList<long> oids)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var targetTable = targetLayer.GetTable())
                    {
                        var editOp = new EditOperation() { Name = "Recortar nuevas geometrías superpuestas" };

                        // 📌 Obtener la referencia espacial de la capa destino
                        var spatialReference = targetLayer.GetSpatialReference();

                        List<long> affectedFeatureIds = new List<long>(); // Para mostrar los IDs modificados

                        // 📌 Diccionario para acumular recortes por FeatureID
                        Dictionary<long, Geometry> geometriesToModify = new Dictionary<long, Geometry>();

                        // 📌 Iterar sobre cada OID recibido (geometrías insertadas)
                        foreach (long oid in oids)
                        {
                            // Obtener la nueva geometría insertada
                            var queryFilter = new QueryFilter() { ObjectIDs = new List<long> { oid } };
                            using (var rowCursor = targetTable.Search(queryFilter, false))
                            {
                                if (!rowCursor.MoveNext()) continue; // Si no encuentra la geometría, continuar

                                using (var row = rowCursor.Current)
                                {
                                    Geometry newGeometry = row["SHAPE"] as Geometry;
                                    if (newGeometry == null) continue;

                                    // 📌 Asegurar que la nueva geometría esté en la misma referencia espacial
                                    if (newGeometry.SpatialReference.Wkid != spatialReference.Wkid)
                                    {
                                        newGeometry = GeometryEngine.Instance.Project(newGeometry, spatialReference);
                                    }

                                    // 📌 Buscar geometrías existentes que se intersectan con `newGeometry`
                                    var spatialFilter = new SpatialQueryFilter()
                                    {
                                        FilterGeometry = newGeometry,
                                        SpatialRelationship = SpatialRelationship.Intersects,
                                        SubFields = "*"
                                    };

                                    using (var targetCursor = targetTable.Search(spatialFilter, false))
                                    {
                                        while (targetCursor.MoveNext())
                                        {
                                            using (var targetFeature = targetCursor.Current as Feature)
                                            {
                                                long featureId = targetFeature.GetObjectID();
                                                if (oids.Contains(featureId)) continue; // Evitar recortar la geometría recién insertada

                                                Geometry existingGeometry = targetFeature.GetShape();
                                                if (existingGeometry == null) continue;

                                                // 📌 Asegurar que `existingGeometry` esté en la misma referencia espacial
                                                if (existingGeometry.SpatialReference.Wkid != spatialReference.Wkid)
                                                {
                                                    existingGeometry = GeometryEngine.Instance.Project(existingGeometry, spatialReference);
                                                }

                                                // 📌 Acumular geometría a recortar
                                                if (geometriesToModify.ContainsKey(featureId))
                                                {
                                                    // Aplicar Difference acumulativamente
                                                    geometriesToModify[featureId] = GeometryEngine.Instance.Difference(
                                                        geometriesToModify[featureId], newGeometry);
                                                }
                                                else
                                                {
                                                    geometriesToModify[featureId] = GeometryEngine.Instance.Difference(
                                                        existingGeometry, newGeometry);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // 📌 Aplicar todas las modificaciones acumuladas
                        foreach (var entry in geometriesToModify)
                        {
                            if (entry.Value != null && !entry.Value.IsEmpty)
                            {
                                editOp.Modify(targetTable, entry.Key, new Dictionary<string, object> { { "SHAPE", entry.Value } });
                                affectedFeatureIds.Add(entry.Key);
                            }
                        }

                        // 📌 Ejecutar la operación de edición si hay cambios
                        if (!editOp.IsEmpty)
                        {
                            var success = editOp.Execute();
                            if (success)
                            {
                                Utils.SendMessageToDockPane($"✅ Recorte completado con éxito. IDs afectados: {string.Join(", ", affectedFeatureIds)}", true);
                            }
                            else
                            {
                                Utils.SendMessageToDockPane("❌ Error al recortar los polígonos insertados.", true);
                            }
                        }
                        else
                        {
                            Utils.SendMessageToDockPane("⚠ No hubo geometrías superpuestas para recortar.", true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error en el recorte: {ex.Message}", true);
                }
            });
        }





    }
}
