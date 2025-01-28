//Copyright 2015-2016 Esri

//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at

//       https://www.apache.org/licenses/LICENSE-2.0

//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;

namespace ProAppModule2.UI.MapTools
{
    /// <summary>
    /// A sample sketch tool that uses the sketch line geometry to cut 
    /// underlying polygons.
    /// </summary>
    class SplitTool : MapTool
    {
        public SplitTool() : base()
        {
            // select the type of construction tool you wish to implement.  
            // Make sure that the tool is correctly registered with the correct component category type in the daml
            SketchType = SketchGeometryType.Line;
            // a sketch feedback is need
            IsSketchTool = true;
            // the geometry is needed in map coordinates
            SketchOutputMode = SketchOutputMode.Map;

        }

        /// <summary>
        /// Called when the sketch finishes. This is where we will create the sketch 
        /// operation and then execute it.
        /// </summary>
        /// <param name="geometry">The geometry created by the sketch.</param>
        /// <returns>A Task returning a Boolean indicating if the sketch complete event 
        /// was successfully handled.</returns>
        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return QueuedTask.Run(() => ExecuteCut(geometry));
        }

        /// <summary>
        /// Method to perform the cut operation on the geometry and change attributes
        /// </summary>
        /// <param name="geometry">Line geometry used to perform the cut against in the polygon features
        /// in the active map view.</param>
        /// <returns>If the cut operation was successful.</returns>
        protected Task<bool> ExecuteCut(Geometry geometry)
        {
            if (geometry == null)
                return Task.FromResult(false);

            // create a collection of feature layers that can be edited
            var editableLayers = ActiveMapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(lyr => lyr.CanEditData() == true).Where(lyr =>
                lyr.ShapeType == esriGeometryType.esriGeometryPolygon);

            // ensure that there are target layers
            if (editableLayers.Count() == 0)
            {
                Utils.SendMessageToDockPane("No se encontraron capas editables para procesar.");
                return Task.FromResult(false); 
            }
            Utils.SendMessageToDockPane("Capas editables identificadas, comenzando el procesamiento...");

            // create an edit operation
            EditOperation cutOperation = new EditOperation()
            {
                Name = "Cut Elements",
                ProgressMessage = "Working...",
                CancelMessage = "Operation canceled.",
                ErrorMessage = "Error cutting polygons",
                SelectModifiedFeatures = false,
                SelectNewFeatures = false
            };

            Utils.SendMessageToDockPane("Realizando corte, espere por favor...");
            // initialize a list of ObjectIDs that need to be cut
            var cutOIDs = new List<long>();

            // for each of the layers 
            foreach (FeatureLayer editableFeatureLayer in editableLayers)
            {
                Utils.SendMessageToDockPane($"Procesando capa: {editableFeatureLayer.Name}");
                // get the feature class associated with the layer
                Table fc = editableFeatureLayer.GetTable();

                // find the field index for the 'Description' attribute
                int descriptionIndex = -1;
                descriptionIndex = fc.GetDefinition().FindField("Description");

                // find the features crossed by the sketch geometry
                //   use the featureClass to search. We need to be able to search with a recycling cursor
                //     seeing we want to Modify the row results
                using (var rowCursor = fc.Search(geometry, SpatialRelationship.Crosses, false))
                {

                    // add the feature IDs into our prepared list
                    while (rowCursor.MoveNext())
                    {
                        using (var feature = rowCursor.Current as Feature)
                        {
                            var geomTest = feature.GetShape();
                            if (geomTest != null)
                            {
                                // make sure we have the same projection for geomProjected and geomTest
                                var geomProjected = GeometryEngine.Instance.Project(geometry, geomTest.SpatialReference);

                                // we are looking for polygons are completely intersected by the cut line
                                if (GeometryEngine.Instance.Relate(geomProjected, geomTest, "TT*F*****"))
                                {
                                    var oid = feature.GetObjectID();

                                    // add the current feature to the overall list of features to cut
                                    cutOIDs.Add(oid);
                                }
                            }
                        }
                    }
                }

                // adjust the attribute before the cut
                if (descriptionIndex != -1)
                {
                    var atts = new Dictionary<string, object>();
                    atts.Add("Description", "Pro Sample");
                    foreach (var oid in cutOIDs)
                        cutOperation.Modify(editableFeatureLayer, oid, atts);
                }

                // add the elements to cut into the edit operation
                cutOperation.Split(editableFeatureLayer, cutOIDs, geometry);
                Utils.SendMessageToDockPane($"Corte realizado en la capa: {editableFeatureLayer.Name}");

            }

            //execute the operation
            
            var operationResult = cutOperation.Execute();

            if (operationResult)
            {
                Utils.SendMessageToDockPane("El proceso de corte se completó con éxito.\nPuede dibujar otra línea para realizar otro corte.");
            }
            else
            {
                Utils.SendMessageToDockPane("El proceso de corte falló. Verifique los datos y vuelva a intentarlo.");
            }


            return Task.FromResult(operationResult);
        }

        /// <summary>
        /// Method to override the sketch symbol after collecting the second vertex
        /// </summary>
        /// <returns>If the sketch symbology was successfully changed.</returns>
        protected override async Task<bool> OnSketchModifiedAsync()
        {
            // retrieve the current sketch geometry
            Polyline cutGeometry = await base.GetCurrentSketchAsync() as Polyline;

            await QueuedTask.Run(() =>
            {
                // if there are more than 2 vertices in the geometry
                if (cutGeometry.PointCount > 2)
                {
                    // adjust the sketch symbol
                    var symbolReference = base.SketchSymbol;
                    if (symbolReference == null)
                    {
                        var cimLineSymbol = SymbolFactory.Instance.ConstructLineSymbol(ColorFactory.Instance.RedRGB, 3,
                            SimpleLineStyle.DashDotDot);
                        base.SketchSymbol = cimLineSymbol.MakeSymbolReference();
                    }
                    else
                    {
                        symbolReference.Symbol.SetColor(ColorFactory.Instance.RedRGB);
                        base.SketchSymbol = symbolReference;
                    }
                }
            });

            return true;
        }

        /// <summary>
        /// Called when the tool is deactivated, such as when the user presses the Escape key or switches tools.
        /// </summary>
        /// <param name="hasMapViewChanged">Indicates if the map view has changed during deactivation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Seleccione una herramienta para comenzar...");
            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }

        /// <summary>
        /// Called when the tool is activated, such as when the user presses the Escape key or switches tools.
        /// </summary>
        /// <param name="hasMapViewChanged">Indicates if the map view has changed during deactivation.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        protected override Task OnToolActivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Dibuje la seccion de corte...");
            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }
    }



    /// <summary>
    /// Extension method to search and retrieve rows
    /// </summary>
    public static class SketchExtensions
    {
        /// <summary>
        /// Performs a spatial query against a feature layer.
        /// </summary>
        /// <remarks>It is assumed that the feature layer and the search geometry are using the same spatial reference.</remarks>
        /// <param name="searchLayer">The feature layer to be searched.</param>
        /// <param name="searchGeometry">The geometry used to perform the spatial query.</param>
        /// <param name="spatialRelationship">The spatial relationship used by the spatial filter.</param>
        /// <returns>Cursor containing the features that satisfy the spatial search criteria.</returns>
        public static RowCursor Search(this BasicFeatureLayer searchLayer, Geometry searchGeometry, SpatialRelationship spatialRelationship)
        {
            RowCursor rowCursor = null;

            // define a spatial query filter
            var spatialQueryFilter = new SpatialQueryFilter
            {
                // passing the search geometry to the spatial filter
                FilterGeometry = searchGeometry,
                // define the spatial relationship between search geometry and feature class
                SpatialRelationship = spatialRelationship
            };

            // apply the spatial filter to the feature layer in question
            rowCursor = searchLayer.Search(spatialQueryFilter);

            return rowCursor;
        }

        /// <summary>
        /// Performs a spatial query against a feature class
        /// </summary>
        /// <remarks>It is assumed that the feature layer and the search geometry are using the same spatial reference.</remarks>
        /// <param name="searchFC">The feature class to be searched.</param>
        /// <param name="searchGeometry">The geometry used to perform the spatial query.</param>
        /// <param name="spatialRelationship">The spatial relationship used by the spatial filter.</param>
        /// <param name="useRecyclingCursor"></param>
        /// <returns>Cursor containing the features that satisfy the spatial search criteria.</returns>
        public static RowCursor Search(this Table searchFC, Geometry searchGeometry, SpatialRelationship spatialRelationship, bool useRecyclingCursor)
        {
            RowCursor rowCursor = null;

            // define a spatial query filter
            var spatialQueryFilter = new SpatialQueryFilter
            {
                // passing the search geometry to the spatial filter
                FilterGeometry = searchGeometry,
                // define the spatial relationship between search geometry and feature class
                SpatialRelationship = spatialRelationship
            };

            // apply the spatial filter to the feature layer in question
            rowCursor = searchFC.Search(spatialQueryFilter, useRecyclingCursor);

            return rowCursor;
        }
    }

}