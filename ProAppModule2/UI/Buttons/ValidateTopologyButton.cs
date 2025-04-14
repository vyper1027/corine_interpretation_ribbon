using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using GeoprocessingExecuteAsync;
using System;
using System.Threading.Tasks;

namespace ProAppModule2.UI.Buttons
{
    internal class ValidateTopologyButton : Button
    {
        private readonly CorineAnalysisService _analysisService;

        /// <summary>
        /// Constructor de la clase, inicializa el servicio de análisis
        /// </summary>
        public ValidateTopologyButton()
        {
            _analysisService = new CorineAnalysisService();
        }

        protected override void OnClick()
        {
            Validate();
        }

        /// <summary>
        /// Ejecuta la validación de topología de todas las capas usando el servicio
        /// </summary>
        private void Validate()
        {
            QueuedTask.Run(async () =>
            {
                try
                {
                    await CorineAnalysisService.ValidateAllLayerTopology();
                    Utils.SendMessageToDockPane("✅ Validación de topología completada.");
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error al validar la topología: {ex.Message}", true);
                }
            });
        }
    }
}
