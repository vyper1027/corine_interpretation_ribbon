using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProAppModule2
{
    class ModValueToSetcl2018 : ArcGIS.Desktop.Framework.Contracts.EditBox
    {
        public ModValueToSetcl2018()
        {
            QueuedTask.Run(() =>
            {                
                const string layer = "Vectores_Cambios_18_20";
                var featLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layer));
                var featSelectionOIDs = featLayer.GetSelection().GetObjectIDs();
                var qf = new QueryFilter() { ObjectIDs = featSelectionOIDs };
                var rowCursor = featLayer.Search(qf);
                while (rowCursor.MoveNext())
                {
                    using (var feat = rowCursor.Current as Feature)
                    {
                        var id = feat.GetOriginalValue(10);
                        Module1.Current.ModValueToSetcl20181 = this;
                        Text = id.ToString();
                    }
                }
            });
            
        }

    }
}
