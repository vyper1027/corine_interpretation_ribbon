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
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.KnowledgeGraph;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProAppModule2.UI.DockPanes
{
    internal class CustomDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "ProAppModule2_CustomDockpane";
        private EmbeddableControl _inspectorViewModel = null;
        private System.Windows.Controls.UserControl _inspectorView = null;
        private Inspector _attributeInspector;
        private Geometry _geometry;
        public ICommand SaveCommand { get; private set; }


        protected CustomDockpaneViewModel()
        {
            // create a new instance for the inspector
            _attributeInspector = new Inspector();

            // Tell the singleton module class
            Module1.AttributeInspector = AttributeInspector;
            Module1.AttributeViewModel = this;

            // create an embeddable control from the inspector class to display on the pane
            var icontrol = _attributeInspector.CreateEmbeddableControl();

            // get viewmodel and view for the inspector control
            InspectorViewModel = icontrol.Item1;
            InspectorView = icontrol.Item2;

            SaveCommand = new RelayCommand(async () => await GuardarCambios(), () => _attributeInspector.HasAttributes);


        }
        #region Properties
        public Inspector AttributeInspector
        {
            get
            {
                return _attributeInspector;
            }
        }

        public System.Windows.Controls.UserControl InspectorView
        {
            get { return _inspectorView; }
            set { SetProperty(ref _inspectorView, value, () => InspectorView); }
        }

        public EmbeddableControl InspectorViewModel
        {
            get { return _inspectorViewModel; }
            set
            {
                if (value != null)
                {
                    _inspectorViewModel = value;
                    _inspectorViewModel.OpenAsync();
                }
                else if (_inspectorViewModel != null)
                {
                    _inspectorViewModel.CloseAsync();
                    _inspectorViewModel = value;
                }
                NotifyPropertyChanged(() => InspectorViewModel);
            }
        }

        private async Task GuardarCambios()
        {
            var ValidEdits = _attributeInspector.HasValidEdits;
            if (ValidEdits == false || ValidEdits == null)
            {
                Utils.SendMessageToDockPane("No hay cambios que guardar o lo atributos editados no son validos.");
                return;
            }

            await QueuedTask.Run(() =>
            {
                var op = new EditOperation
                {
                    Name = "Guardar Edición de Atributos",
                    ShowProgressor = true
                };

                op.Modify(_attributeInspector);
                if (op.Execute())
                {
                    Utils.SendMessageToDockPane($"Los cambios han sido guardados correctamente.\nOID { _attributeInspector.ObjectID }");
                }
                else
                {
                    Utils.SendMessageToDockPane("Error al guardar los cambios.");
                }
            });
        }


        public Geometry Geometry
        {
            get => _geometry;
            set => SetProperty(ref _geometry, value);
        }
        #endregion
        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "Seleccione un poligono para ver sus atributos";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }

    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class CustomDockpane_ShowButton : Button
    {
        protected override void OnClick()
        {
            CustomDockpaneViewModel.Show();
        }
    }
}
