using ArcGIS.Desktop.Framework.Contracts;
using System.Threading.Tasks;

namespace ProAppModule2.UI.Buttons
{
    internal class ValidateLayerButton : Button
    {
        protected override async void OnClick()
        {
            await LayerValidationService.ValidateLayerConformity();
            await LayerValidationService.ValidateNullFields();
        }
    }
}
