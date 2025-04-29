using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Core.CIM;
using System.Windows; // Para Application

namespace ProAppModule2.UI.MapTools
{
    class SplitTool : MapTool
    {
        public SplitTool()
        {
            SketchType = SketchGeometryType.Line;
            IsSketchTool = true;
            UseSnapping = true;
            SketchOutputMode = SketchOutputMode.Map;
        }

        protected override Task OnToolActivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Dibuje la sección de corte...");

            // Suscribir evento de teclado para detectar F6
            Application.Current.MainWindow.PreviewKeyDown += OnKeyDown;

            return base.OnToolActivateAsync(hasMapViewChanged);
        }

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            Utils.SendMessageToDockPane("Seleccione una herramienta para comenzar...");

            // Desuscribir evento de teclado
            Application.Current.MainWindow.PreviewKeyDown -= OnKeyDown;

            return base.OnToolDeactivateAsync(hasMapViewChanged);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.D6)
            {
                Utils.SendMessageToDockPane("F6 detectado: Realizando Snap to Vertex...");
                SnapToVertex(); // Lógica de Snap que debes implementar
                e.Handled = true;
            }
        }

        protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return QueuedTask.Run(() => ExecuteCut(geometry));
        }

        private Task<bool> ExecuteCut(Geometry geometry)
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

            var cutOperation = new EditOperation
            {
                Name = "Cut Elements",
                ProgressMessage = "Trabajando...",
                CancelMessage = "Operación cancelada.",
                ErrorMessage = "Error al cortar polígonos",
                SelectModifiedFeatures = false,
                SelectNewFeatures = false
            };

            foreach (var layer in editableLayers)
            {
                Utils.SendMessageToDockPane($"Procesando capa: {layer.Name}");
                Table table = layer.GetTable();

                var cutOIDs = new List<long>();
                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = geometry,
                    SpatialRelationship = SpatialRelationship.Crosses,
                    SubFields = "*"
                };

                using (var cursor = table.Search(spatialFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        using (var feature = cursor.Current as Feature)
                        {
                            var featureGeometry = feature?.GetShape();
                            if (featureGeometry != null)
                            {
                                var projectedGeometry = GeometryEngine.Instance.Project(geometry, featureGeometry.SpatialReference);
                                if (GeometryEngine.Instance.Relate(projectedGeometry, featureGeometry, "TT*F*****"))
                                {
                                    cutOIDs.Add(feature.GetObjectID());
                                }
                            }
                        }
                    }
                }

                if (cutOIDs.Count > 0)
                {
                    var attributes = new Dictionary<string, object> { { "cambio", 2 } };
                    foreach (var oid in cutOIDs)
                        cutOperation.Modify(layer, oid, attributes);

                    cutOperation.Split(layer, cutOIDs, geometry);
                    Utils.SendMessageToDockPane($"Corte realizado en la capa: {layer.Name}");
                }
                else
                {
                    Utils.SendMessageToDockPane($"No se encontraron elementos para cortar en la capa: {layer.Name}");
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
            var cutGeometry = await base.GetCurrentSketchAsync() as Polyline;

            await QueuedTask.Run(() =>
            {
                if (cutGeometry?.PointCount > 2)
                {
                    var currentSymbol = base.SketchSymbol;
                    if (currentSymbol == null)
                    {
                        var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(
                            ColorFactory.Instance.RedRGB, 3, SimpleLineStyle.DashDotDot);
                        base.SketchSymbol = lineSymbol.MakeSymbolReference();
                    }
                    else
                    {
                        currentSymbol.Symbol.SetColor(ColorFactory.Instance.RedRGB);
                        base.SketchSymbol = currentSymbol;
                    }
                }
            });

            return true;
        }

        private void SnapToVertex()
        {
            // Obtener el mapa actual
            var map = MapView.Active.Map;

            // Obtener la configuración actual de snapping por capa
            var layerSnapModes = Snapping.GetLayerSnapModes(map);

            // Detectar si el snapping a vértices ya está activado en al menos una capa
            bool vertexSnappingActive = layerSnapModes.Values.Any(modes => modes.Vertex);

            if (vertexSnappingActive)
            {
                // Desactivar el snapping
                Snapping.IsEnabled = false;
                Snapping.SnapChipEnabled = false;

                // Limpiar modos de snapping por capa
                foreach (var kvp in layerSnapModes)
                {
                    kvp.Value.Vertex = false;
                    kvp.Value.Point = false;
                }

                Snapping.SetLayerSnapModes(layerSnapModes, true);
                Utils.SendMessageToDockPane("Snapping a vértices desactivado.");
            }
            else
            {
                // Activar el snapping
                Snapping.IsEnabled = true;
                Snapping.SnapChipEnabled = true;

                foreach (var kvp in layerSnapModes)
                {
                    kvp.Value.Vertex = true;
                    kvp.Value.Point = false;
                }

                Snapping.SetLayerSnapModes(layerSnapModes, true);
                Utils.SendMessageToDockPane("Snapping a vértices activado.");
            }
        }
    }
}
