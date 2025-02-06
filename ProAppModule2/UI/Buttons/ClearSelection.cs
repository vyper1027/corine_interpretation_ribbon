using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace ProAppModule2.UI.Buttons
{
    internal class ClearSelection : Button
    {
        protected override void OnClick()
        {
            QueuedTask.Run(() =>
            {
                // Obtener el mapa activo
                MapView mapView = MapView.Active;
                if (mapView != null)
                {
                    // Limpiar la selección de todas las capas
                    mapView.Map?.SetSelection(null);
                }
            });
        }
    }
}
