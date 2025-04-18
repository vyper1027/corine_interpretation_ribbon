﻿
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Topology;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProAppModule2;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace GeoprocessingExecuteAsync
{
    internal class CorineAnalysisService
    {
        /// <summary>
        /// Ejecuta la validación de topología de la capa Corine (Capa destino)
        /// </summary>
    public static async Task ValidateAllLayerTopology()
    {
        await QueuedTask.Run(async () =>
        {
            // ✅ Obtener la capa de topología
            var topologyLayer = await Utils.GetTopologyLayer();

            if (topologyLayer == null)
            {
                Utils.SendMessageToDockPane("❌ No se encontró una capa de topología en el mapa.");
                return;
            }

            Utils.SendMessageToDockPane($"✅ Ejecutando validación de topología en toda la capa: {topologyLayer.Name}");

            // ✅ Obtener el objeto de Topology
            Topology topology = topologyLayer.GetTopology();
            if (topology == null)
            {
                Utils.SendMessageToDockPane("❌ No se pudo obtener la topología desde el layer.");
                return;
            }

            // ⚠ Confirmar guardado de ediciones pendientes
            if (Project.Current.HasEdits)
            {
                bool shouldSave = false;

                // Mostrar cuadro de diálogo para confirmar guardado
                await QueuedTask.Run(() =>
                {
                    var result = MessageBox.Show(
                        "Hay ediciones pendientes. ¿Deseas guardar los cambios antes de validar la topología?",
                        "Guardar ediciones",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    shouldSave = (result == MessageBoxResult.Yes);
                });

                if (!shouldSave)
                {
                    Utils.SendMessageToDockPane("❌ Validación cancelada por el usuario para evitar guardar cambios.");
                    return;
                }

                Utils.SendMessageToDockPane("💾 Guardando cambios antes de validar...");
                await Project.Current.SaveEditsAsync();
            }

            // 📏 Obtener el extent completo de la topología
            Envelope fullExtent = topology.GetExtent();

            // 🔍 Validar la topología en toda la capa
            ValidationResult result = topology.Validate(new ValidationDescription(fullExtent));

            // ✅ Verificar si hay errores topológicos
            if (result.AffectedArea != null && !result.AffectedArea.IsEmpty)
            {
                Utils.SendMessageToDockPane($"⚠ Se encontraron errores de topología en la capa: {result.AffectedArea.ToJson()}");
            }
            else
            {
                Utils.SendMessageToDockPane("✅ No se encontraron errores de topología en la capa.");
            }
        });
    }



    public static async Task ValidateCurrentExtentTopology()
        {
            await QueuedTask.Run(async () =>
            {
                // Obtener la capa de topología en el mapa activo
                var topologyLayer = await Utils.GetTopologyLayer();

                if (topologyLayer == null)
                {
                    Utils.SendMessageToDockPane("❌ No se encontró una capa de topología en el mapa.");
                    return;
                }

                Utils.SendMessageToDockPane($"✅ Ejecutando validación de topología en: {topologyLayer.Name}");

                Topology topology = topologyLayer.GetTopology();
                if (topology == null)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener la topología desde el layer.");
                    return;
                }

                var activeView = MapView.Active;
                if (activeView == null)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener la vista activa.");
                    return;
                }

                SpatialReference srTopologyLayer = topologyLayer.GetSpatialReference();
                if (srTopologyLayer == null)
                {
                    Utils.SendMessageToDockPane("no existe layer de topologia, revisa que exista y que tenga el nombre indicado");
                    return;
                }

                if (srTopologyLayer.Unit is AngularUnit angularUnitTopologyLayer)
                {
                    if (angularUnitTopologyLayer.Name != "Degree")
                    {
                        Utils.SendMessageToDockPane($"📏 Unidad de medida incorrecta: {angularUnitTopologyLayer.Name}, la capa debe estar en Degree", true);
                        return;
                    }
                }
                else if (srTopologyLayer.Unit is LinearUnit linearUnitTopologyLayer)
                {
                    if (linearUnitTopologyLayer.Name != "Meter")
                    {
                        Utils.SendMessageToDockPane($"📏 Unidad de medida incorrecta: {linearUnitTopologyLayer.Name}, la capa debe estar en Meter", true);
                        return;
                    }
                }


                double extentArea = activeView.Extent.Area;
                if (extentArea > 1521354409.2)
                {
                    Utils.SendMessageToDockPane("❌ El area del extent es demasiado grande para validar la topologia.");
                    return;
                }

                Envelope extent = activeView.Extent;
                if (extent == null || extent.IsEmpty)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener el extent actual.");
                    return;
                }

                if (srTopologyLayer == null)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener el sistema de coordenadas de la capa de topología.");
                    return;
                }

                if (GeometryEngine.Instance.Project(extent, srTopologyLayer) is not Envelope convertedExtent || convertedExtent.IsEmpty)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo reproyectar el extent al sistema de coordenadas de la capa.");
                    return;
                }

                Utils.SendMessageToDockPane($"🔍 Validando topología en el extent actual...", true);

                /*if (Project.Current.HasEdits)
                {
                    Utils.SendMessageToDockPane("⚠ Hay ediciones pendientes. Guardando cambios...");
                    await Project.Current.SaveEditsAsync();
                }*/

                // Validar la topología dentro del extent actual
                ValidationResult result = topology.Validate(new ValidationDescription(convertedExtent));

                if (result.AffectedArea != null && !result.AffectedArea.IsEmpty)
                {
                    Utils.SendMessageToDockPane("⚠ Se encontraron errores de topología en el área");
                }
                else
                {
                    Utils.SendMessageToDockPane("✅ No se encontraron errores de topología en el extent actual.");
                }
            });
        }

        /// <summary>
        /// Ejecuta un análisis de clúster (ejemplo).
        /// </summary>
        public async Task FindCluster()
        {
            await QueuedTask.Run(async () =>
            {
                Utils.SendMessageToDockPane("🔍 Buscando clústeres...");

                // Simulación de parámetros, reemplázalos con los reales
                var parameters = Geoprocessing.MakeValueArray("NombreDeTuCapa", "Memory\\ClusterResult");

                var gpResult = await Geoprocessing.ExecuteToolAsync("analysis.FindClusters", parameters);

                if (gpResult.IsFailed)
                {
                    Utils.SendMessageToDockPane($"❌ Error en el análisis de clústeres: {gpResult.Messages}");
                }
                else
                {
                    Utils.SendMessageToDockPane("✅ Análisis de clúster completado.");
                }
            });
        }

        /// <summary>
        /// Encuentra polígonos menores a 5 ha (ejemplo).
        /// </summary>
        public async Task<List<long>> FindSmallPolygons()
        {
            List<long> selectedIds = new List<long>();

            await QueuedTask.Run(() =>
            {
                Utils.SendMessageToDockPane("🔍 Buscando polígonos menores a 5 ha...");

                var inputLayer = Utils.GetDynamicLayer("capaCorine");
                if (inputLayer == null)
                {
                    Utils.SendMessageToDockPane("⚠️ No se encontró la capa 'capaCorine'.");
                    return;
                }

                if (inputLayer.Result is not FeatureLayer featureLayer)
                {
                    Utils.SendMessageToDockPane("⚠️ La capa no es un FeatureLayer válido.");
                    return;
                }

                string areaFieldName = "area_ha";

                QueryFilter queryFilter = new QueryFilter
                {
                    WhereClause = $"{areaFieldName} < 5"
                };

                using (Selection selection = featureLayer.Select(queryFilter, SelectionCombinationMethod.New, null, null, null))
                {
                    int selectedCount = (int)selection.GetCount();
                    if (selectedCount == 0)
                    {
                        Utils.SendMessageToDockPane("⚠️ No se encontraron polígonos menores a 5 ha.");
                    }
                    else
                    {
                        Utils.SendMessageToDockPane($"✅ Se seleccionaron {selectedCount} polígonos menores a 5 ha.");

                        // Obtener los ObjectIDs de los features seleccionados
                        using (RowCursor cursor = selection.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                using (Row row = cursor.Current)
                                {
                                    selectedIds.Add(row.GetObjectID());
                                }
                            }
                        }
                    }
                }
            });

            return selectedIds;
        }


        /// <summary>
        /// Ejecuta el cálculo de prioridad
        /// </summary>
        public async Task CalculatePriority()
        {
            await QueuedTask.Run(async () =>
            {
                Utils.SendMessageToDockPane("🔍 Calculando prioridad...");

                var inputLayer = Utils.GetDynamicLayer("capaCorine");
                string outputTable = "PolygonNeighbors_Table";
                string priorityField = "Priority_Field";

                // Calcular los vecinos de los polígonos
                var neighborParams = Geoprocessing.MakeValueArray(inputLayer, outputTable);
                var neighborResult = await Geoprocessing.ExecuteToolAsync("analysis.PolygonNeighbors", neighborParams);

                if (neighborResult.IsFailed)
                {
                    Utils.SendMessageToDockPane($"❌ Error al calcular vecinos: {neighborResult.Messages}");
                    return;
                }

                // Aquí podrías procesar la tabla resultante (outputTable) si es necesario antes de calcular el campo
                // Por simplicidad, se asume que el cálculo de prioridad se hace directamente sobre la capa

                // Calcular el campo de prioridad
                var fieldParams = Geoprocessing.MakeValueArray(inputLayer, priorityField);
                var fieldResult = await Geoprocessing.ExecuteToolAsync("analysis.CalculateField", fieldParams);

                if (fieldResult.IsFailed)
                {
                    Utils.SendMessageToDockPane($"❌ Error en el cálculo de prioridad: {fieldResult.Messages}");
                }
                else
                {
                    Utils.SendMessageToDockPane("✅ Cálculo de prioridad completado.");
                }
            });
        }

    }
}
