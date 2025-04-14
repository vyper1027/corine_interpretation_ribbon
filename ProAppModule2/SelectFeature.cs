using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Dialogs;

namespace ProAppModule2
{
    internal class SelectFeature : ComboBox
    {
        private const string LayerName = "Area_corte_periodos";

        // Referencias estáticas a los combos
        public static SelectFeature ComboMes { get; private set; }
        public static SelectFeature ComboBloque { get; private set; }
        public static SelectFeature ComboPlancha { get; private set; }

        private List<SelectFeature> _otherComboBoxes = new();

        protected override void OnUpdate()
        {
            // Asignación automática de instancia según ID
            switch (ID)
            {
                case "ComboBoxShowingLayers_SelectMes":
                    ComboMes = this;
                    break;
                case "ComboBoxShowingLayers_SelectBloque":
                    ComboBloque = this;
                    break;
                case "ComboBoxShowingLayers_SelectPlancha":
                    ComboPlancha = this;
                    break;
            }
        }

        protected override void OnDropDownOpened()
        {
            EnsureCombosAreLinked();
            _ = UpdateComboAsync();
        }

        protected override void OnSelectionChange(ComboBoxItem item)
        {
            _ = HandleSelectionChangeAsync(item);
        }

        private void EnsureCombosAreLinked()
        {
            if (_otherComboBoxes.Count > 0)
                return;

            var allCombos = new[] { ComboMes, ComboBloque, ComboPlancha }.Where(c => c != null);
            _otherComboBoxes = allCombos.Where(c => c != this).ToList();
        }

        private async Task UpdateComboAsync()
        {
            Clear();

            var featureLayer = await FindFeatureLayerByNameAsync(LayerName);
            if (featureLayer == null) return;

            await QueuedTask.Run(() =>
            {
                string fieldName = GetCaption();
                var uniqueValues = new HashSet<string>();

                using var cursor = featureLayer.Search();
                while (cursor.MoveNext())
                {
                    using var feature = cursor.Current as Feature;
                    var value = feature[fieldName]?.ToString();
                    if (!string.IsNullOrEmpty(value) && uniqueValues.Add(value))
                    {
                        Add(new FeatureComboBoxItem(value, null));
                    }
                }
            });
        }

        private async Task HandleSelectionChangeAsync(ComboBoxItem item)
        {
            ClearOtherSelections();

            if (item is not FeatureComboBoxItem selectedItem)
                return;

            var geometry = await SelectAndZoomFeaturesAsync(selectedItem.Text);
            if (geometry != null)
            {
                await MapView.Active.ZoomToAsync(geometry, TimeSpan.FromSeconds(1));
            }
            else
            {
                MessageBox.Show("No se encontraron elementos con el valor seleccionado.");
            }
        }

        private void ClearOtherSelections()
        {
            foreach (var combo in _otherComboBoxes)
            {
                if (combo != null)
                {
                    combo.SelectedItem = null;
                    combo.Clear(); // Borra los ítems del combo también
                }
            }
        }

        private async Task<Geometry> SelectAndZoomFeaturesAsync(string selectedValue)
        {
            var featureLayer = await FindFeatureLayerByNameAsync(LayerName);
            if (featureLayer == null || string.IsNullOrEmpty(selectedValue))
                return null;

            return await QueuedTask.Run(() =>
            {
                string fieldName = GetCaption();
                var tableDef = featureLayer.GetTable().GetDefinition();
                var fieldDef = tableDef.GetFields().FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (fieldDef == null)
                    return null;

                bool isNumericField = fieldDef.FieldType is FieldType.Integer or FieldType.Single or FieldType.SmallInteger or FieldType.Double;

                var whereClause = isNumericField
                    ? $"{fieldName} = {selectedValue}"
                    : $"{fieldName} = '{selectedValue.Replace("'", "''")}'";

                var queryFilter = new QueryFilter { WhereClause = whereClause };
                featureLayer.Select(queryFilter, SelectionCombinationMethod.New);

                var extents = new List<Geometry>();

                using var cursor = featureLayer.Search(queryFilter);
                while (cursor.MoveNext())
                {
                    using var feature = cursor.Current as Feature;
                    var geom = feature?.GetShape();
                    if (geom != null)
                        extents.Add(geom.Extent);
                }

                return extents.Count > 0 ? GeometryEngine.Instance.Union(extents) : null;
            });
        }

        private async Task<FeatureLayer> FindFeatureLayerByNameAsync(string layerName)
        {
            return await QueuedTask.Run(() =>
                Project.Current
                    .GetItems<MapProjectItem>()
                    .Select(item => item.GetMap())
                    .SelectMany(map => map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                    .FirstOrDefault(fl => fl.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase))
            );
        }

        public string GetCaption()
        {            
            return Tooltip;
        }
    }
}
