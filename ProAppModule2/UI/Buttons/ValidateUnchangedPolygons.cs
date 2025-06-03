using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using GeoprocessingExecuteAsync;
using System;
namespace ProAppModule2.UI.Buttons
{
    internal class ValidateUnchangedPolygons : Button
    {
        private readonly CorineAnalysisService _analysisService;

        /// <summary>
        /// Constructor de la clase, inicializa el servicio de análisis
        /// </summary>
        public ValidateUnchangedPolygons()
        {
            _analysisService = new CorineAnalysisService();
        }

        protected override void OnClick()
        {
            Validate();
        }


        private void Validate()
        {
            QueuedTask.Run(async () =>
            {
                try
                {
                    await LayerValidationService.ValidateChangedFeaturesByAttributeAsync();
                    Utils.SendMessageToDockPane("✅ Validación de poligonos completada.", true);
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error poligonos sin cambio (cambio 0 a cambio 1): {ex.Message}", true);
                }
            });
        }
    }

}