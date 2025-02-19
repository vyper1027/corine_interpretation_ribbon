using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace ProAppModule2.UI.CheckBoxes
{
    internal class ToggleTopologyCheckBox : CheckBox
    {
        protected override void OnClick()
        {
            if (IsChecked == true)
            {
                FrameworkApplication.State.Activate("Control_Topology_cond");
                Utils.SendMessageToDockPane("✅ Validación de Topología Activada");
            }
            else
            {
                FrameworkApplication.State.Deactivate("Control_Topology_cond");
                Utils.SendMessageToDockPane("❌ Validación de Topología Desactivada");
            }
        }
    }
}
