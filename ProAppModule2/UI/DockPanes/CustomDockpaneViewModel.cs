using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProAppModule2.UI.DockPanes
{
    internal class CustomDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "ProAppModule2_CustomDockpane";
        private EmbeddableControl _inspectorViewModel;
        private System.Windows.Controls.UserControl _inspectorView;
        private Inspector _attributeInspector;
        private Geometry _geometry;

        public ICommand SaveCommand { get; }

        protected CustomDockpaneViewModel()
        {
            _attributeInspector = new Inspector();

            // Asignar al módulo singleton
            Module1.AttributeInspector = _attributeInspector;
            Module1.AttributeViewModel = this;

            // Crear control embebido
            var inspectorControl = _attributeInspector.CreateEmbeddableControl();
            InspectorViewModel = inspectorControl.Item1;
            InspectorView = inspectorControl.Item2;

            // Comando de guardado
            SaveCommand = new RelayCommand(async () => await GuardarCambios(), () => _attributeInspector.HasAttributes);

            // Atajo Ctrl+Enter para guardar
            Keyboard.AddKeyDownHandler(System.Windows.Application.Current.MainWindow, OnKeyDown);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) &&
                FrameworkApplication.DockPaneManager.Find(_dockPaneID)?.IsVisible == true)
            {
                if (SaveCommand.CanExecute(null))
                {
                    SaveCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        #region Properties

        public Inspector AttributeInspector => _attributeInspector;

        public System.Windows.Controls.UserControl InspectorView
        {
            get => _inspectorView;
            set => SetProperty(ref _inspectorView, value, () => InspectorView);
        }

        public EmbeddableControl InspectorViewModel
        {
            get => _inspectorViewModel;
            set
            {
                if (_inspectorViewModel != null)
                    _inspectorViewModel.CloseAsync();

                _inspectorViewModel = value;

                if (_inspectorViewModel != null)
                    _inspectorViewModel.OpenAsync();

                NotifyPropertyChanged(() => InspectorViewModel);
            }
        }

        public Geometry Geometry
        {
            get => _geometry;
            set => SetProperty(ref _geometry, value);
        }

        private string _heading = "Seleccione un polígono para ver sus atributos";
        public string Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value, () => Heading);
        }

        #endregion

        /// <summary>
        /// Ejecuta la operación de edición si hay cambios válidos.
        /// </summary>
        private async Task GuardarCambios()
        {
            if (_attributeInspector.HasValidEdits != true)
            {
                Utils.SendMessageToDockPane("No hay cambios que guardar o los atributos editados no son válidos.");
                return;
            }

            await QueuedTask.Run(() =>
            {
                AplicarReglasDeNegocio();

                var editOp = new EditOperation
                {
                    Name = "Guardar Edición de Atributos",
                    ShowProgressor = true
                };

                editOp.Modify(_attributeInspector);

                if (editOp.Execute())
                {
                    Utils.SendMessageToDockPane($"Los cambios han sido guardados correctamente.\nOID {_attributeInspector.ObjectID}");
                }
                else
                {
                    Utils.SendMessageToDockPane("Error al guardar los cambios.");
                }
            });
        }

        /// <summary>
        /// Aplica reglas lógicas antes de guardar los atributos.
        /// </summary>
        private void AplicarReglasDeNegocio()
        {
            var nuevoCodigo = _attributeInspector["codigo"]?.ToString();

            if (!string.IsNullOrEmpty(nuevoCodigo))
            {
                _attributeInspector["cambio"] = 2;
                Utils.SendMessageToDockPane("cambio=2", true);
            }
        }


        /// <summary>
        /// Muestra el panel acoplado.
        /// </summary>
        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }

    internal class CustomDockpane_ShowButton : Button
    {
        protected override void OnClick() => CustomDockpaneViewModel.Show();
    }
}
