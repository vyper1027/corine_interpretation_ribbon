using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.UtilityNetwork.Trace;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Core;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using ProAppModule2.UI.DockPanes;

namespace ProAppModule2.UI.MapTools
{
    internal class MapTool1 : MapTool
    {
        public MapTool1()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;
            SketchOutputMode = SketchOutputMode.Screen;
        }

        protected override Task OnToolActivateAsync(bool hasMapViewChanged)
        {
            CustomDockpaneViewModel.Show();
            return Task.FromResult(true);//base.OnToolActivateAsync(active);
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            CustomDockpaneViewModel.Show();
            QueuedTask.Run(() =>
            {
                // Using the active map view to select
                // the features that intersect the sketch geometry
                ActiveMapView.SelectFeatures(geometry);
                var results = ActiveMapView.GetFeatures(geometry);
                //ActiveMapView.FlashFeature(results);

                var debug = string.Join("\n", results.ToDictionary().Select(kvp => string.Format("{0}: {1}", kvp.Key.Name, kvp.Value.Count())));

                if (results.Count == 0)
                {
                    Utils.SendMessageToDockPane("Seleccione uno o varios poligonos para comenzar...");
                }

                foreach (var kvp in results.ToDictionary())
                {
                    var featLyr = kvp.Key as BasicFeatureLayer;
                    const string layer = "Vectores_Cambios_18_20";
                    if (kvp.Key.Name is layer)
                    {
                        var nc = kvp.Value.Count();
                        if (nc >= 1 || nc <= 90)
                        {
                            //Project.Current.SetIsEditingEnabledAsync(true);
                            if (FrameworkApplication.State.Contains("controls_state"))
                            {
                                FrameworkApplication.State.Deactivate("controls_state");
                            }
                            if (FrameworkApplication.State.Contains("controls_atb"))
                            {
                                FrameworkApplication.State.Deactivate("controls_atb");
                            }
                            if (FrameworkApplication.State.Contains("controls_crtng"))
                            {
                                FrameworkApplication.State.Deactivate("controls_crtng");
                            }
                            if (FrameworkApplication.State.Contains("controls_edbox"))
                            {
                                FrameworkApplication.State.Deactivate("controls_edbox");
                            }


                            var featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals(layer));
                            var qf = new QueryFilter() { ObjectIDs = kvp.Value };
                            var rowCursor = featLyr.Search(qf);
                            while (rowCursor.MoveNext())
                            {
                                using (var feat = rowCursor.Current as Feature)
                                {
                                    var listOID = new List<long> { feat.GetObjectID() };
                                    //Access all field values
                                    var id = feat.GetOriginalValue(2);
                                    var gc = feat.GetOriginalValue(3);
                                    var area = feat.GetOriginalValue(6);
                                    var clase = feat.GetOriginalValue(8);
                                    var count = feat.GetFields().Count();
                                    var pointSelection = featureLayer.Select(qf);
                                    List<long> oids = pointSelection.GetObjectIDs().ToList();
                                    var msj2 = "Ha seleccionado " + nc + " poligonos, Continue con el proceso...";//+ " con area de "+area+ " representa la clase "+clase;
                                    Utils.SendMessageToDockPane(msj2);
                                    //ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"{msj2}");

                                    var inspector = Module1.AttributeInspector;
                                    inspector.Load(kvp.Key, kvp.Value); //oids[0]

                                    // update the heading 
                                    Module1.AttributeViewModel.Heading = $@"OID Seleccionado: {oids[0]}";

                                    foreach (var attrib in inspector)
                                    {
                                        var value = attrib.CurrentValue.ToString();
                                        Debug.WriteLine(value);

                                    }
                                }
                            }
                        }
                        else
                        {
                            var msj = "En la capa " + kvp.Key.Name + " fueron seleccionados " + nc + " registros, por favor seleccione solo uno si desea aprobar o editar.";
                            //System.Diagnostics.Debug.WriteLine(msj);
                            Utils.SendMessageToDockPane(msj);
                            if (FrameworkApplication.State.Contains("controls_state")) 
                            {
                                FrameworkApplication.State.Deactivate("controls_state");
                            }
                            if (FrameworkApplication.State.Contains("controls_atb")) 
                            {
                                FrameworkApplication.State.Deactivate("controls_atb");
                            }
                            if (FrameworkApplication.State.Contains("controls_crtng"))
                            {
                                FrameworkApplication.State.Deactivate("controls_crtng");
                            }
                            if (Project.Current.IsEditingEnabled)
                            {
                                //Before disabling we must check for any edits
                                if (Project.Current.HasEdits)
                                {
                                    var res = MessageBox.Show("Save edits?", "Save Edits?",
                                    System.Windows.MessageBoxButton.YesNoCancel);
                                    if (res == System.Windows.MessageBoxResult.Cancel)
                                        return;//user has canceled
                                    else if (res == System.Windows.MessageBoxResult.No)
                                        Project.Current.DiscardEditsAsync(); //user does not want to save
                                    else
                                        Project.Current.SaveEditsAsync();//save
                                }
                                Project.Current.SetIsEditingEnabledAsync(false);
                            }
                        }


                    }

                }

            });
            return Task.FromResult(true);
        }
    }
}
