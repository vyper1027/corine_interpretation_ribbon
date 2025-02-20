using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProAppModule2
{
    /// <summary>
    /// Represents the ComboBox
    /// </summary>
    internal class SelectFeature : ComboBox
    {
        /// <summary>
        /// Combo Box constructor
        /// </summary>
        public SelectFeature()
        {
            //UpdateCombo();
        }

        /// <summary>
        /// Updates the combo box with all the items.
        /// </summary>
        protected override void OnDropDownOpened()
        {
            // collect all features in a ComboFeature collection '_comboFeatures'
            UpdateCombo();
        }
        private async void UpdateCombo()
        {
            Clear();
            // get the state layer
            var featureLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(fl => fl.Name.Equals("zonas_prueba_opt")); //planchas_100k
            if (featureLayer == null) return;

            // Add feature layer names to the combobox
            await QueuedTask.Run(() =>
            {
                using var featCursor = featureLayer.Search();                
                while (featCursor.MoveNext())
                {
                    using var feature = featCursor.Current as Feature;
                    Add(new FeatureComboBoxItem(feature["Zona"].ToString(), feature.GetShape().Clone())); ///PLANCHA
                };
            });

        }

        /// <summary>
        /// The on comboBox selection change event. 
        /// </summary>
        /// <param name="item">The newly selected combo box item</param>
        protected override void OnSelectionChange(ComboBoxItem item)
        {
            if (item is FeatureComboBoxItem featComboBoxItem)
            {
                MapView.Active?.ZoomToAsync(featComboBoxItem.Geometry, TimeSpan.FromSeconds(1.5));               
            }
            return;
        }

    }
}
