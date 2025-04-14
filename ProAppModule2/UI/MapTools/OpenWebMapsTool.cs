using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ProAppModule2.UI.MapTools
{
    internal class OpenWebMapsTool : MapTool
    {
        public OpenWebMapsTool()
        {
            OverlayControlID = "MapToolWithOverlayControl_EmbeddableControl";
            OverlayControlCanResize = true;
            OverlayControlPositionRatio = new Point(0, 0.95); // bottom left
        }

        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                e.Handled = true;
        }

        protected override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            return QueuedTask.Run(() =>
            {
                var mapPoint = ActiveMapView.ClientToMap(e.ClientPoint);
                var coords = GeometryEngine.Instance.Project(mapPoint, SpatialReferences.WGS84) as MapPoint;
                if (coords == null)
                    return;

                string url = GenerateUrl(this.Caption, coords);
                if (!string.IsNullOrEmpty(url))
                {
                    OpenInBrowser(url);
                }
                else
                {
                    Utils.SendMessageToDockPane("No se pudo generar el enlace. Asegúrate de que el botón tiene un caption válido.");
                }
            });
        }

        private static void OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al abrir el navegador: {ex.Message}");
            }
        }

        private static string GenerateUrl(string caption, MapPoint coords)
        {
            string y = coords.Y.ToString(CultureInfo.InvariantCulture);
            string x = coords.X.ToString(CultureInfo.InvariantCulture);

            return caption switch
            {
                "Abrir en Google" => $"https://www.google.com/maps/@{y},{x},13z",
                "Abrir en Bing" => $"https://www.bing.com/maps?lvl=13&style=h&cp={y}~{x}",
                "Abrir en Esri" => $"https://www.arcgis.com/apps/mapviewer/index.html?" +
                                  "basemapUrl=http%3A%2F%2Fservices.arcgisonline.com%2FArcGIS%2Frest%2Fservices%2FWorld_Imagery%2FMapServer" +
                                  "&basemapReferenceUrl=http%3A%2F%2Fservices.arcgisonline.com%2FArcGIS%2Frest%2Fservices%2FReference%2FWorld_Boundaries_and_Places%2FMapServer" +
                                  $"&center={x}%2C{y}&level=13",
                _ => null
            };
        }
    }
}
