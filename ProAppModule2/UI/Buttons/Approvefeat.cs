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
using ArcGIS.Core.Internal.Geometry;


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
        protected override void OnClick()
        {

            ApproveValues();

        }

        public void ApproveValues()
        {            
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
                string attributename = "estado";
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
                        //await ExplodeMultipartFeatures(targetLayer, newFeatureOIDs);
                    }
                    else
                    {
                        Utils.SendMessageToDockPane("⚠ No se encontraron nuevos IDs para recortar.");
                    }

                    if (isTopologyValidationEnabled)
                    {
                        if (Project.Current.HasEdits)
                        {
                            // Mostrar advertencia al usuario
                            var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                                "⚠ Hay ediciones pendientes. Para validar la topología, es necesario guardar los cambios.\n\n" +
                                "Una vez guardados, no podrá deshacer las ediciones.\n\n¿Desea guardar y continuar?",
                                "Guardar ediciones antes de validar",
                                System.Windows.MessageBoxButton.YesNo,
                                System.Windows.MessageBoxImage.Warning);

                            if (result == System.Windows.MessageBoxResult.Yes)
                            {
                                var saveResult = await Project.Current.SaveEditsAsync();
                                if (!saveResult)
                                {
                                    Utils.SendMessageToDockPane("❌ Error al guardar ediciones. No se pudo validar la topología.", true);
                                    return;
                                }

                                Utils.SendMessageToDockPane("🔍 Ejecutando validación de topología...");
                                await CorineAnalysisService.ValidateCurrentExtentTopology();
                            }
                            else
                            {
                                Utils.SendMessageToDockPane("ℹ Validación de topología cancelada por el usuario.");
                            }
                        }
                        else
                        {
                            Utils.SendMessageToDockPane("🔍 Ejecutando validación de topología...");
                            await CorineAnalysisService.ValidateCurrentExtentTopology();
                        }
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
                                    rowBuffer["apoyo"] = row["apoyo"];
                                    //rowBuffer["horaAprobacion"] = DateTime.Now;
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
                            var cleanedGeometry = entry.Value;

                            if (cleanedGeometry != null && !cleanedGeometry.IsEmpty)
                            {
                                // Filtrar por área mínima de 5 ha (50,000 m²)
                                double area = GeometryEngine.Instance.Area(cleanedGeometry);

                                if (area >= 0)
                                {
                                    editOp.Modify(targetTable, entry.Key, new Dictionary<string, object> {
                                        { "SHAPE", cleanedGeometry },
                                        { "cambio", 2 }
                                    });

                                    affectedFeatureIds.Add(entry.Key);
                                }
                                else
                                {                                    
                                    Utils.SendMessageToDockPane($"⚠ Polígono {entry.Key} omitido: área < 5 ha.", true);
                                }
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

        public async Task ExplodeMultipartFeatures(FeatureLayer targetLayer, IReadOnlyList<long> oids)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    using (var table = targetLayer.GetTable())
                    {
                        var editOp = new EditOperation { Name = "Explotar multipartes en capa Corine" };
                        var nuevosOIDs = new List<long>();
                        var tableDef = table.GetDefinition();

                        foreach (long oid in oids)
                        {
                            using (var rowCursor = table.Search(new QueryFilter { ObjectIDs = new List<long> { oid } }, false))
                            {
                                if (!rowCursor.MoveNext())
                                    continue;

                                using (var row = rowCursor.Current as Feature)
                                {
                                    var shape = row.GetShape();
                                    if (shape is not Polygon polygon || polygon.PartCount <= 1)
                                        continue;

                                    foreach (var part in polygon.Parts)
                                    {
                                        var newPolygon = PolygonBuilder.CreatePolygon(part, polygon.SpatialReference);
                                        var buffer = table.CreateRowBuffer();

                                        foreach (var field in tableDef.GetFields())
                                        {
                                            if (!field.IsEditable || field.Name.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase))
                                                continue;

                                            buffer[field.Name] = row[field.Name];
                                        }

                                        buffer["SHAPE"] = newPolygon;

                                        var newFeature = table.CreateRow(buffer) as Feature;
                                        editOp.Callback(ctx => ctx.Invalidate(newFeature), table);
                                        nuevosOIDs.Add(newFeature.GetObjectID());
                                    }

                                    // Eliminar la geometría original
                                    editOp.Delete(table, oid);
                                }
                            }
                        }

                        if (!editOp.IsEmpty)
                        {
                            if (editOp.Execute())
                                Utils.SendMessageToDockPane($"✅ Multipartes explotadas correctamente. Nuevos IDs: {string.Join(", ", nuevosOIDs)}", true);
                            else
                                Utils.SendMessageToDockPane("❌ No se pudo ejecutar la operación de explosión de multipartes.", true);
                        }
                        else
                        {
                            Utils.SendMessageToDockPane("ℹ No se encontraron multipartes para explotar.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error al explotar multipartes: {ex.Message}", true);
                }
            });
        }

    }
}
