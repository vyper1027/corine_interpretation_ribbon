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
            QueuedTask.Run(() =>
            {
                var featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(fl => fl.Name.Contains("Vectores_Cambios_18_20")).FirstOrDefault();
                QueryFilter qf = new QueryFilter()
                {
                    WhereClause = "Estado = 'Por Revisar'"
                };
                using (RowCursor rows = featureLayer.Search(qf)) //execute
                {
                    //Looping through to count
                    int i = 0;
                    while (rows.MoveNext()) i++;
                    System.Diagnostics.Debug.WriteLine(i.ToString());

                    QueryFilter qf2 = new QueryFilter()
                    {
                        WhereClause = "Estado = 'Aprobado'"
                    };
                    using (RowCursor rows1 = featureLayer.Search(qf2)) //execute
                    {
                        //Looping through to count
                        int j = 0;
                        while (rows1.MoveNext()) j++;
                        System.Diagnostics.Debug.WriteLine(j.ToString());
                        System.Diagnostics.Debug.WriteLine(i.ToString());
                        //MessageBox.Show(String.Format("Total de poligonos por Revisar: {0}", i.ToString()));
                        //Module1.Current.Reviewer1 = this;
                        var txt = "Total de poligonos por Revisar: " + i.ToString() + "\nTotal de poligonos Aprobados: " + j.ToString();
                        Text = txt.ToString();
                    }



                    return i;
                }

                //Modvalueset();
                //Module1.Current.ModValueToSetcl20181 = this;
                //Text = "Prueba"; //id.ToString();
                //const string layer = "Vectores_Cambios_18_20";
                //var featLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layer));
                //var featSelectionOIDs = featLayer.GetSelection().GetObjectIDs();
                //var qf = new QueryFilter() { ObjectIDs = featSelectionOIDs };
                //var rowCursor = featLayer.Search(qf);
                //while (rowCursor.MoveNext())
                //{
                //    using (var feat = rowCursor.Current as Feature)
                //    {
                //        var id = feat.GetOriginalValue(10);
                //        Module1.Current.Reviewer1 = this;
                //        Text = id.ToString();
                //    }
                //}
            });

        }

    }


}
