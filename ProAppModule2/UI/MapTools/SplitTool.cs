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
    class SplitTool : MapTool
    {
        public SplitTool()
        {
            SketchType = SketchGeometryType.Line;
            IsSketchTool = true;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return QueuedTask.Run(() => ExecuteCut(geometry));
        }

        protected Task<bool> ExecuteCut(Geometry geometry)
        {
            if (geometry == null)
                return Task.FromResult(false);

            var editableLayers = ActiveMapView.Map.GetLayersAsFlattenedList()
                .OfType<FeatureLayer>()
                .Where(lyr => lyr.CanEditData() && lyr.ShapeType == esriGeometryType.esriGeometryPolygon);

            if (!editableLayers.Any())
            {
                Utils.SendMessageToDockPane("No se encontraron capas editables para procesar.");
                return Task.FromResult(false);
            }

            Utils.SendMessageToDockPane("Capas editables identificadas, comenzando el procesamiento...");

            EditOperation cutOperation = new EditOperation
            {
                Name = "Cut Elements",
                ProgressMessage = "Trabajando...",
                CancelMessage = "Operación cancelada.",
                ErrorMessage = "Error al cortar polígonos",
                SelectModifiedFeatures = false,
                SelectNewFeatures = false
            };

            foreach (FeatureLayer editableFeatureLayer in editableLayers)
            {
                Utils.SendMessageToDockPane($"Procesando capa: {editableFeatureLayer.Name}");
                Table fc = editableFeatureLayer.GetTable();

                var cutOIDs = new List<long>();

                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = geometry,
                    SpatialRelationship = SpatialRelationship.Crosses,
                    SubFields = "*"
                };

                using (var rowCursor = fc.Search(spatialFilter, false))
                {
                    while (rowCursor.MoveNext())
                    {
                        using (var feature = rowCursor.Current as Feature)
                        {
                            var geomTest = feature.GetShape();
                            if (geomTest != null)
                            {
                                var geomProjected = GeometryEngine.Instance.Project(geometry, geomTest.SpatialReference);
                                if (GeometryEngine.Instance.Relate(geomProjected, geomTest, "TT*F*****"))
                                {
                                    cutOIDs.Add(feature.GetObjectID());
                                }
                            }
                        }
                    }
                }

                if (cutOIDs.Count > 0)
                {
                    var atts = new Dictionary<string, object>
                    {
                        { "cambio", 2 }
                    };

                    foreach (var oid in cutOIDs)
                        cutOperation.Modify(editableFeatureLayer, oid, atts);

                    cutOperation.Split(editableFeatureLayer, cutOIDs, geometry);
                    Utils.SendMessageToDockPane($"Corte realizado en la capa: {editableFeatureLayer.Name}");
                }
                else
                {
                    Utils.SendMessageToDockPane($"No se encontraron elementos para cortar en la capa: {editableFeatureLayer.Name}");
                }
            }

            var result = cutOperation.Execute();

            if (result)
                Utils.SendMessageToDockPane("El proceso de corte se completó con éxito.\nPuede dibujar otra línea para realizar otro corte.");
            else
                Utils.SendMessageToDockPane("El proceso de corte falló. Verifique los datos y vuelva a intentarlo.");

            return Task.FromResult(result);
        }

        protected override async Task<bool> OnSketchModifiedAsync()
        {
            Polyline cutGeometry = await base.GetCurrentSketchAsync() as Polyline;

            await QueuedTask.Run(() =>
            {
                if (cutGeometry?.PointCount > 2)
                {
                    var symbolReference = base.SketchSymbol;
                    if (symbolReference == null)
                    {
                        var cimLineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                            ColorFactory.Instance.RedRGB, 3, SimpleLineStyle.DashDotDot);
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

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Seleccione una herramienta para comenzar...");
            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }

        protected override Task OnToolActivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Dibuje la sección de corte...");
            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }
    }
}
