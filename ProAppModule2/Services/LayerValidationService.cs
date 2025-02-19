using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProAppModule2;

public static class LayerValidationService
{
    /// <summary>
    /// Valida si la capa cumple con los requisitos de atributos y sistema de coordenadas.
    /// </summary>
    public static async Task ValidateLayerConformity()
    {
        await QueuedTask.Run(async () =>
        {
            // ✅ 1. Obtener la capa a validar
            var featureLayer = await Utils.GetDynamicLayer("capaCorine") as FeatureLayer;
            if (featureLayer == null)
            {
                Utils.SendMessageToDockPane("❌ No se encontró la capa Corine.", true);
                return;
            }

            using (Table table = featureLayer.GetTable())
            {
                using (FeatureClassDefinition definition = (FeatureClassDefinition)table.GetDefinition())
                {
                    // ✅ 2. Validar conformidad de atributos (estructura de la capa)
                    string[] requiredFields = { "Codigo", "Insumo", "Apoyo", "Confiabilidad" };
                    Dictionary<string, FieldType> expectedTypes = new Dictionary<string, FieldType>
                    {
                        { "Codigo", FieldType.SmallInteger },
                        { "Insumo", FieldType.String },
                        { "Apoyo", FieldType.String },
                        { "Confiabilidad", FieldType.String }
                    };

                    foreach (var field in requiredFields)
                    {
                        var fieldDef = definition.GetFields().FirstOrDefault(f => f.Name == field);
                        if (fieldDef == null)
                        {
                            Utils.SendMessageToDockPane($"❌ Falta el campo obligatorio: {field}", true);
                            continue;
                        }
                        if (fieldDef.FieldType != expectedTypes[field])
                        {
                            Utils.SendMessageToDockPane($"❌ El campo {field} tiene un tipo incorrecto ({fieldDef.FieldType}). Se esperaba {expectedTypes[field]}.", true);
                        }
                    }
                }
            }

            // ✅ 3. Validar conformidad del Datum
            SpatialReference sr = featureLayer.GetSpatialReference();
            if (sr == null)
            {
                Utils.SendMessageToDockPane("❌ No se pudo obtener el sistema de coordenadas.", true);
                return;
            }

            if (sr.Name.Contains("MAGNA-SIRGAS"))
            {
                Utils.SendMessageToDockPane("✅ Sistema de coordenadas correcto: MAGNA-SIRGAS.", true);
            }
            else if (sr.Name.Contains("Transverse Mercator") && sr.VcsWkid == 4686)
            {
                Utils.SendMessageToDockPane("✅ Sistema de coordenadas correcto: Transverse Mercator con MAGNA-SIRGAS.", true);
            }
            else
            {
                Utils.SendMessageToDockPane($"❌ El sistema de coordenadas es incorrecto: {sr.Name}.", true);
            }

            Utils.SendMessageToDockPane("✅ Validación de conformidad completada.", true);
        });
    }
}
