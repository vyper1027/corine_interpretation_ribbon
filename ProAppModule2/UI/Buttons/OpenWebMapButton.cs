using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProAppModule2.UI.Buttons
{
    internal class OpenWebMapButton: Button
    {
        protected override void OnClick()
        {
            var map = MapView.Active?.Map;
            if (map == null)
            {
                Utils.SendMessageToDockPane("No hay un mapa activo.");
                return;
            }


        }
    }
}
