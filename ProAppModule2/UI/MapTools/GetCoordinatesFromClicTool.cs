using System;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Security.Policy;

namespace ProAppModule2.UI.MapTools
{
    /// <summary>
    /// Class providing the behavior for the custom map tool.
    /// </summary>
    internal class GetCoordinatesFromClicTool : MapTool
    {
        public GetCoordinatesFromClicTool()
        {            
            // Set the tools OverlayControlID to the DAML id of the embeddable control
            OverlayControlID = "MapToolWithOverlayControl_EmbeddableControl";
            // Embeddable control can be resized
            OverlayControlCanResize = true;
            // Specify ratio of 0 to 1 to place the control
            OverlayControlPositionRatio = new Point(0, 0.95); // bottom left
        }

        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            // En este caso, no estamos manejando nada en el MouseDown, solo necesitamos capturar el clic
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                e.Handled = true;
        }

        /// <summary>
        /// Called when the OnToolMouseDown event is handled. Allows the opportunity to perform asynchronous operations corresponding to the event.
        /// </summary>
        protected override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {            
            // Obtener las coordenadas del clic y mostrarlas en un MessageBox
            return QueuedTask.Run(() =>
            {
                var mapPoint = ActiveMapView.ClientToMap(e.ClientPoint);
                var coords = GeometryEngine.Instance.Project(mapPoint, SpatialReferences.WGS84) as MapPoint;
                if (coords == null) return;

                var sb = new StringBuilder();
                sb.AppendLine($"X: {coords.X:0.000}");
                sb.Append($"Y: {coords.Y:0.000}");
                if (coords.HasZ)
                {
                    sb.AppendLine();
                    sb.Append($"Z: {coords.Z:0.000}");
                }

                // Mostrar las coordenadas en un MessageBox
                string url = GenerateUrl(this.Caption, coords);

                if (!string.IsNullOrEmpty(url))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Utils.SendMessageToDockPane("No se pudo generar el enlace. Asegúrate de que el botón tiene un caption válido.");
                }
            });
        }

        private string GenerateUrl(string caption, MapPoint coords)
        {
            string url = string.Empty;

            switch (caption)
            {
                case "Abrir en Google":
                    url = $"https://www.google.com/maps/@{coords.Y},{coords.X},15z";
                    break;

                case "Abrir en Bing":
                    url = $"https://www.bing.com/maps?lvl=17&style=h&cp={coords.Y}~{coords.X}";
                    break;

                case "Abrir en Esri":
                    url = $"https://www.arcgis.com/home/webmap/viewer.html?center={coords.X}%2C{coords.Y}&level=15";
                    break;

                default:
                    Debug.WriteLine("Caption desconocido: " + caption);
                    break;
            }

            return url;
        }
    }
}

