using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data.Topology;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProAppModule2; 

namespace GeoprocessingExecuteAsync
{
    internal class CorineAnalysisService
    {
        /// <summary>
        /// Ejecuta la validación de topología de la capa Corine (Capa destino)
        /// </summary>
        public static async Task ValidateTopology()
        {
            await QueuedTask.Run(async () =>
            {
                // Obtener la capa de topología en el mapa activo
                var topologyLayer = await Utils.GetDynamicLayer("capaCorine");

                if (topologyLayer == null)
                {
                    Utils.SendMessageToDockPane("❌ No se encontró una capa de topología en el mapa.");
                    return;
                }

                Utils.SendMessageToDockPane($"✅ Ejecutando validación de topología en: {topologyLayer.Name}");

                // Parámetros para la herramienta de geoprocesamiento
                var parameters = Geoprocessing.MakeValueArray(topologyLayer);

                // Ejecutar la herramienta de geoprocesamiento "ValidateTopology"
                var gpResult = await Geoprocessing.ExecuteToolAsync("management.ValidateTopology", parameters);

                if (gpResult.IsFailed)
                {
                    Utils.SendMessageToDockPane($"❌ Error en la validación de topología: {gpResult.Messages}");
                }
                else
                {
                    Utils.SendMessageToDockPane("✅ Validación de topología completada con éxito.");
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

                Envelope extent = activeView.Extent;

                if (extent == null || extent.IsEmpty)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener el extent actual.");
                    return;
                }

                SpatialReference srCapa = topologyLayer.GetSpatialReference();
                if (srCapa == null)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener el sistema de coordenadas de la capa de topología.");
                    return;
                }

                if (GeometryEngine.Instance.Project(extent, srCapa) is not Envelope convertedExtent || convertedExtent.IsEmpty)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo reproyectar el extent al sistema de coordenadas de la capa.");
                    return;
                }

                Utils.SendMessageToDockPane($"🔍 Validando topología en el extent actual...", true);


                if (Project.Current.HasEdits)
                {
                    Utils.SendMessageToDockPane("⚠ Hay ediciones pendientes. Guardando cambios...");
                    await Project.Current.SaveEditsAsync();
                }
                // Validar la topología dentro del extent actual
                ValidationResult result = topology.Validate(new ValidationDescription(convertedExtent));

                if (result.AffectedArea != null && !result.AffectedArea.IsEmpty)
                {
                    Utils.SendMessageToDockPane($"⚠ Se encontraron errores de topología en el área: {result.AffectedArea.ToJson()}");
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
        public async Task FindSmallPolygons()
        {
            await QueuedTask.Run(async () =>
            {
                Utils.SendMessageToDockPane("🔍 Buscando polígonos menores a 5 ha...");

                // Simulación de parámetros, reemplázalos con los reales
                var parameters = Geoprocessing.MakeValueArray("NombreDeTuCapa", "AREA < 5");

                var gpResult = await Geoprocessing.ExecuteToolAsync("management.SelectLayerByAttribute", parameters);

                if (gpResult.IsFailed)
                {
                    Utils.SendMessageToDockPane($"❌ Error al buscar polígonos pequeños: {gpResult.Messages}");
                }
                else
                {
                    Utils.SendMessageToDockPane("✅ Búsqueda de polígonos menores a 5 ha completada.");
                }
            });
        }

        /// <summary>
        /// Ejecuta el cálculo de prioridad
        /// </summary>
        public async Task CalculatePriority()
        {
            await QueuedTask.Run(async () =>
            {
                Utils.SendMessageToDockPane("🔍 Calculando prioridad...");

                // Parámetros ficticios, reemplazar con los reales
                var parameters = Geoprocessing.MakeValueArray("NombreDeTuCapa", "Priority_Field");

                var gpResult = await Geoprocessing.ExecuteToolAsync("analysis.CalculateField", parameters);

                if (gpResult.IsFailed)
                {
                    Utils.SendMessageToDockPane($"❌ Error en el cálculo de prioridad: {gpResult.Messages}");
                }
                else
                {
                    Utils.SendMessageToDockPane("✅ Cálculo de prioridad completado.");
                }
            });
        }
    }
}

