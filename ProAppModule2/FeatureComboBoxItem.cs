using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProAppModule2
{
    internal class FeatureComboBoxItem : ComboBoxItem
    {
        internal FeatureComboBoxItem(string name, Geometry geometry) : base(name, null, $@"zoom to '{name}'")
        {
            Geometry = geometry;
        }
        internal Geometry Geometry { get; set; }
    }
}