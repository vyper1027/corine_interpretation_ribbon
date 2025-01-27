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

namespace ProAppModule2.UI.MapTools
{
    internal class SelectDrawAndSplitTool : MapTool
    {
        public SelectDrawAndSplitTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Rectangle;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task OnToolActivateAsync(bool active)
        {
            return base.OnToolActivateAsync(active);
        }

        protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return await QueuedTask.Run(() =>
            {
                var selPoly = geometry as Polygon;
                var elems = MapView.Active.SelectElements(selPoly, SelectionCombinationMethod.New);
                return true;
            });
        }
        protected override void OnToolMouseUp(MapViewMouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case System.Windows.Input.MouseButton.Right:
                    e.Handled = true;
                    var menu = FrameworkApplication.CreateContextMenu(
                           "esri_layouts_mapGraphicContextMenu");
                    menu.DataContext = this;
                    menu.IsOpen = true;
                    break;
            }
        }
    }
}
