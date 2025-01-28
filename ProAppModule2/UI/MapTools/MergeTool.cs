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

namespace ProAppModule2.UI.MapTools
{
    /// <summary>
    /// A sample sketch tool that uses a polygon selection to merge selected polygons.
    /// </summary>
    class MergeTool : MapTool
    {
        public MergeTool() : base()
        {
            // Define the type of tool: Polygon for selection.
            SketchType = SketchGeometryType.Polygon;
            IsSketchTool = true;
            SketchOutputMode = SketchOutputMode.Map;
        }

        /// <summary>
        /// Called when the sketch finishes. This is where we will perform the merge operation.
        /// </summary>
        /// <param name="geometry">The geometry created by the sketch.</param>
        /// <returns>A Task returning a Boolean indicating if the merge operation was successfully handled.</returns>
        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return QueuedTask.Run(() => ExecuteMerge(geometry));
        }

        /// <summary>
        /// Method to perform the merge operation on selected features.
        /// </summary>
        /// <param name="geometry">Polygon geometry used to perform the selection of features to merge.</param>
        /// <returns>If the merge operation was successful.</returns>
        protected Task<bool> ExecuteMerge(Geometry geometry)
        {
            if (geometry == null)
                return Task.FromResult(false);

            // Create a collection of editable polygon feature layers
            var editableLayers = ActiveMapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(lyr => lyr.CanEditData() && lyr.ShapeType == esriGeometryType.esriGeometryPolygon);

            if (!editableLayers.Any())
                return Task.FromResult(false);

            // Create an edit operation
            EditOperation mergeOperation = new EditOperation()
            {
                Name = "Merge Features",
                ProgressMessage = "Merging polygons...",
                CancelMessage = "Merge operation canceled.",
                ErrorMessage = "Error merging polygons",
                SelectModifiedFeatures = true
            };

            foreach (var editableLayer in editableLayers)
            {
                Table featureClass = editableLayer.GetTable();

                // Find the features intersected by the sketch geometry
                var selectedOIDs = new List<long>();
                using (var rowCursor = featureClass.Search(geometry, SpatialRelationship.Intersects, false))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (var feature = rowCursor.Current as Feature)
                        {
                            selectedOIDs.Add(feature.GetObjectID());
                        }
                    }
                }

                // If there are at least two features to merge
                if (selectedOIDs.Count >= 2)
                {
                    mergeOperation.Merge(editableLayer, selectedOIDs);
                }
            }

            // Execute the operation
            bool operationResult = mergeOperation.Execute();

            return Task.FromResult(operationResult);
        }

        /// <summary>
        /// Method to override the sketch symbol during the sketch operation.
        /// </summary>
        /// <returns>If the sketch symbology was successfully changed.</returns>
        protected override async Task<bool> OnSketchModifiedAsync()
        {
            // Retrieve the current sketch geometry
            Polygon sketchGeometry = await base.GetCurrentSketchAsync() as Polygon;

            await QueuedTask.Run(() =>
            {
                if (sketchGeometry != null && sketchGeometry.PointCount > 3)
                {
                    // Update the sketch symbol for the merge operation
                    var symbolReference = base.SketchSymbol;
                    if (symbolReference == null)
                    {
                        var cimPolygonSymbol = SymbolFactory.Instance.ConstructPolygonSymbol(
                            ColorFactory.Instance.CreateRGBColor(0, 255, 0, 50)); // Semi-transparent green
                        base.SketchSymbol = cimPolygonSymbol.MakeSymbolReference();
                    }
                    else
                    {
                        symbolReference.Symbol.SetColor(ColorFactory.Instance.CreateRGBColor(0, 255, 0, 50));
                        base.SketchSymbol = symbolReference;
                    }
                }
            });

            return true;
        }
    }
}
