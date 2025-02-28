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

                await Project.Current.SetIsEditingEnabledAsync(true);

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
                        // 📌 Ahora usamos los nuevos ObjectIDs en el recorte
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
                try
                {
                    List<RowToken> newRowTokens = new List<RowToken>(); // 📌 Lista de RowTokens temporales

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
                                var shape = row["SHAPE"] as Geometry; 
                                var codigo = row["C2020_clc"]; 

                                // 📌 Diccionario con atributos para la nueva entidad
                                var attributes = new Dictionary<string, object>
                        {
                            { "SHAPE", shape },
                            { "apoyo", null },
                            { "area_ha", null },
                            { "cambio", 2 },
                            { "codigo", codigo },
                            { "confiabilidad", null },                                   
                            { "insumo", null },
                            { "leyenda", null },
                            { "nivel_1", null },
                            { "nivel_2", null },
                            { "nivel_3", null },
                            { "nivel_4", null },
                            { "nivel_5", null },
                            { "nivel_6", null },
                            { "h_aprob", DateTime.Now }
                        };

                                // 📌 Crear la entidad en la capa destino y capturar el RowToken
                                RowToken newRow = createFeatures.Create(targetTable, attributes);
                                if (newRow != null)
                                {
                                    newRowTokens.Add(newRow);
                                }
                            }
                        }

                        // 📌 Ejecutar la operación de inserción
                        if (!createFeatures.IsEmpty)
                        {
                            var success = createFeatures.Execute();
                            if (!success)
                            {
                                Utils.SendMessageToDockPane("❌ Error al copiar los polígonos en la capa destino.");
                                return new List<long>(); // Retornar lista vacía si falla la inserción
                            }
                        }

                        // 📌 Obtener los ObjectIDs reales de los nuevos features
                        List<long> newObjectIDs = new List<long>();
                        foreach (var token in newRowTokens)
                        {
                            var newOID = token.ObjectID;
                            if (newOID > 0)
                                newObjectIDs.Add((long)newOID);
                        }

                        Utils.SendMessageToDockPane($"✅ Se copiaron {newObjectIDs.Count} polígonos a la capa Corine.");
                        return newObjectIDs;
                    }
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error en la inserción: {ex.Message}");
                }

                return new List<long>(); // 📌 Si hay un problema, devolver lista vacía
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
                    using (var rowCursor = targetTable.Search(new QueryFilter { ObjectIDs = oids }, false))
                    {
                        var editOp = new EditOperation() { Name = "Recortar nuevas geometrías superpuestas" };

                        // 📌 Obtener la referencia espacial de la capa destino
                        var spatialReference = targetLayer.GetSpatialReference();

                        while (rowCursor.MoveNext())
                        {
                            using var row = rowCursor.Current;
                            Geometry newGeometry = row["SHAPE"] as Geometry;
                            if (newGeometry == null) continue;

                            // 📌 Asegurar que la nueva geometría esté en la misma referencia espacial
                            if (newGeometry.SpatialReference.Wkid != spatialReference.Wkid)
                            {
                                newGeometry = GeometryEngine.Instance.Project(newGeometry, spatialReference);
                            }

                            // 📌 Filtrar solo las geometrías cercanas con SpatialQueryFilter
                            var spatialFilter = new SpatialQueryFilter()
                            {
                                FilterGeometry = newGeometry, // Filtrar geometrías que intersectan
                                SpatialRelationship = SpatialRelationship.Intersects,
                                SubFields = "*"  // Asegurar que obtenemos todos los atributos
                            };

                            List<long> affectedFeatureIds = new List<long>(); // Para mostrar los IDs modificados
                            using (var targetCursor = targetTable.Search(spatialFilter, false))
                            {
                                while (targetCursor.MoveNext())
                                {
                                    using (var targetFeature = targetCursor.Current as Feature)
                                    {
                                        Geometry existingGeometry = targetFeature.GetShape();
                                        if (existingGeometry != null && !oids.Contains(targetFeature.GetObjectID()))
                                        {
                                            // 📌 Asegurar que las geometrías están en el mismo SR
                                            if (existingGeometry.SpatialReference.Wkid != spatialReference.Wkid)
                                            {
                                                existingGeometry = GeometryEngine.Instance.Project(existingGeometry, spatialReference);
                                            }

                                            // 📌 Recortar el polígono existente con la nueva geometría
                                            Geometry clippedGeometry = GeometryEngine.Instance.Difference(existingGeometry, newGeometry);

                                            // 📌 Verificar si el recorte es válido
                                            if (clippedGeometry != null && !clippedGeometry.IsEmpty)
                                            {
                                                editOp.Modify(targetTable, targetFeature.GetObjectID(),
                                                    new Dictionary<string, object> { { "SHAPE", clippedGeometry } });

                                                affectedFeatureIds.Add(targetFeature.GetObjectID()); // Agregar ID afectado
                                            }
                                        }
                                    }
                                }
                            }

                            // 📌 Mensaje con los IDs de las geometrías afectadas
                            if (affectedFeatureIds.Count > 0)
                            {
                                Utils.SendMessageToDockPane($"✅ Se recortaron los siguientes IDs: {string.Join(", ", affectedFeatureIds)}", true);
                            }
                        }

                        // Ejecutar la operación de recorte
                        if (!editOp.IsEmpty)
                        {
                            var success = editOp.Execute();
                            if (!success)
                            {
                                Utils.SendMessageToDockPane("❌ Error al recortar los polígonos insertados.", true);
                            }
                            else
                            {
                                Utils.SendMessageToDockPane("✅ Recorte completado con éxito. No hay superposición.", true);
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
