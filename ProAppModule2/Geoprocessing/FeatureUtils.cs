using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;


namespace ProAppModule2.Geoprocessing
{
    public static class FeatureUtils
    {
        public static async Task<bool> InsertGeometryToCorineLayer(FeatureLayer sourceLayer, long featureID)
        {
            var targetLayer = await Utils.GetDynamicLayer("capaCorine");
            if (targetLayer == null)
            {
                Utils.SendMessageToDockPane("No se encontró la capa de destino: Cobertura_Corine_2020.");
                return false;
            }

            return await QueuedTask.Run(() =>
            {
                using (var rowCursor = sourceLayer.GetSelection().Search())
                {
                    if (!rowCursor.MoveNext()) return false;

                    using (var feature = rowCursor.Current as Feature)
                    {
                        Geometry geometry = feature.GetShape();
                        if (geometry == null) return false;

                        var editOp = new EditOperation
                        {
                            Name = "Insertar geometría en Cobertura_Corine_2020"
                        };

                        var attributes = new Dictionary<string, object>
                        {
                            { "Nombre", "Nueva entidad" }
                        };

                        editOp.Create(targetLayer, geometry, attributes);

                        return editOp.Execute();
                    }
                }
            });
        }
    }
}