using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.KnowledgeGraph;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using QueryFilter = ArcGIS.Core.Data.QueryFilter;

namespace ProAppModule2.UI.Buttons
{
    internal class Reviewer : Button
    {
        protected override async void OnClick()
        {
            try
            {
                var layer = await Utils.GetDynamicLayer("vectoresDeCambio");
                //var layerName = layer?.Name ?? "No se encontro la capa de cambios";
                //var featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Where(fl => fl.Name.Contains(layerName)).FirstOrDefault();
                var count = QueuedTask.Run(() =>
                {
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

                            Module1.Current.Reviewer1 = this;
                            var txt = "Total de poligonos por Revisar: " + i.ToString() + "\nTotal de poligonos Aprobados: " + j.ToString();
                            MessageBox.Show(string.Format(txt));
                            string Text = txt.ToString();
                            //Module1.ToggleState("controls_edbox");
                        }
                        return i;
                    }

                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
            }
        }        
    }
}
