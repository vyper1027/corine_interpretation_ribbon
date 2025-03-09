using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ProAppModule2
{
    class EditboxRv : ArcGIS.Desktop.Framework.Contracts.EditBox
    {

        public EditboxRv()
        {
            QueuedTask.Run(async () =>
            {
                var layer = await Utils.GetDynamicLayer("vectoresDeCambio");
                //var featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(fl => fl.Name.Contains("Vectores_Cambios_18_20")).FirstOrDefault();
                QueryFilter qf = new QueryFilter()
                {
                    WhereClause = "Estado = 'Por Revisar'"
                };
                using (RowCursor rows = layer.Search(qf)) //execute
                {
                    //Looping through to count
                    int i = 0;
                    while (rows.MoveNext()) i++;
                    System.Diagnostics.Debug.WriteLine(i.ToString());

                    QueryFilter qf2 = new QueryFilter()
                    {
                        WhereClause = "Estado = 'Aprobado'"
                    };
                    using (RowCursor rows1 = layer.Search(qf2)) //execute
                    {
                        //Looping through to count
                        int j = 0;
                        while (rows1.MoveNext()) j++;
                        System.Diagnostics.Debug.WriteLine(j.ToString());
                        System.Diagnostics.Debug.WriteLine(i.ToString());                        
                        var txt = "Total de poligonos por Revisar: " + i.ToString() + "\nTotal de poligonos Aprobados: " + j.ToString();
                        Text = txt.ToString();
                    }
                    return i;
                }                
            });

        }

    }


}
