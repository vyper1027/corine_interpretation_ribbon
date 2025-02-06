using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ProAppModule2.UI.Buttons;
using ProAppModule2.UI.DockPanes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProAppModule2
{
    internal class Module1 : Module
    {
        private static Module1 _this = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        //public static Module1 Current => _this ??= (Module1)FrameworkApplication.FindModule("ProAppModule2_Module");

        public static Module1 Current
        {
            get
            {
                return _this ?? (_this = (Module1)FrameworkApplication.FindModule("ProAppModule2_Module"));
            }
        }

        #region Static Properties

        private static Inspector _attributeInspector = null;

        internal static Inspector AttributeInspector
        {
            get { return _attributeInspector; }
            set { _attributeInspector = value; }
        }

        private static CustomDockpaneViewModel _attributeViewModel = null;

        internal static CustomDockpaneViewModel AttributeViewModel
        {
            get { return _attributeViewModel; }
            set { _attributeViewModel = value; }
        }

        #endregion Static Properties

        #region Overrides
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //TODO - add your business logic
            //return false to ~cancel~ Application close
            return true;
        }

        #endregion Overrides

        #region Toggle State
        /// <summary>
        /// Activate or Deactivate the specified state. State is identified via
        /// its name. Listen for state changes via the DAML <b>condition</b> attribute
        /// </summary>
        /// <param name="stateID"></param>
        public static void ToggleState(string stateID)
        {
            if (FrameworkApplication.State.Contains(stateID))
            {
                FrameworkApplication.State.Deactivate(stateID);
            }
            else
            {
                FrameworkApplication.State.Activate(stateID);
            }
        }

        #endregion Toggle State

        #region Business Logic
        private ModValueToSetcl2018 _attributEditBox = null;
        public ModValueToSetcl2018 ModValueToSetcl20181
        {
            get; set;
            //get { return _attributEditBox; }
            //set { _attributEditBox = value; }

        }
        private Reviewer _attributEditBoxr = null;
        public Reviewer Reviewer1
        {
            //get; set;
            get { return _attributEditBoxr; }
            set { _attributEditBoxr = value; }

        }
        #endregion Business Logic

    }
}
