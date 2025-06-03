using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProAppModule2;
using ArcGIS.Desktop.Editing;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

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
                    string[] requiredFields = { "codigo", "insumo", "cambio", "confiabili" };
                    Dictionary<string, FieldType> expectedTypes = new Dictionary<string, FieldType>
                    {
                        { "codigo", FieldType.Integer },
                        { "insumo", FieldType.String },
                        { "cambio", FieldType.Integer },
                        { "confiabili", FieldType.String }
                    };

                    Utils.SendMessageToDockPane("");

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

            if (sr.Wkid == 9377)
            {
                Utils.SendMessageToDockPane("✅ Sistema de coordenadas correcto: EPSG 9377", true);
            }
            else if (sr.Wkid == 4686)
            {
                Utils.SendMessageToDockPane("✅ Sistema de coordenadas correcto: EPSG 4686 (MAGNA-SIRGAS)", true);
            }
            else if (sr.Name.Contains("Transverse Mercator") && sr.VcsWkid == 4686)
            {
                Utils.SendMessageToDockPane("✅ Sistema de coordenadas correcto: Transverse Mercator con MAGNA-SIRGAS.", true);
            }
            else
            {
                Utils.SendMessageToDockPane($"❌ El sistema de coordenadas es incorrecto: {sr.Wkid}. Debe ser EPSG 9377 o 4686", true);
            }

            Utils.SendMessageToDockPane("✅ Validación de conformidad del archivo completada.", true);
        });
    }

    public static async Task ValidateNullFields()
    {
        await QueuedTask.Run(async () =>
        {
            // Obtener la capa 'capaCorine'
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
                    // Campos requeridos para verificar si hay valores nulos
                    string[] requiredFields = { "codigo", "insumo", "confiabili", "cambio" };

                    // Crear un filtro de consulta para buscar valores nulos en cualquiera de los campos requeridos
                    QueryFilter queryFilter = new QueryFilter
                    {
                        WhereClause = string.Join(" OR ", requiredFields.Select(f => $"{f} IS NULL"))
                    };

                    List<long> nullFeatureIds = new();

                    // Ejecutar la búsqueda de filas con el filtro
                    using (RowCursor cursor = table.Search(queryFilter, false))
                    {
                        int oidIndex = definition.FindField(definition.GetObjectIDField());
                        while (cursor.MoveNext())
                        {
                            using (Row row = cursor.Current)
                            {
                                nullFeatureIds.Add(Convert.ToInt64(row[oidIndex]));
                            }
                        }
                    }

                    // Mostrar el resultado
                    if (nullFeatureIds.Any())
                    {
                        string joined = string.Join(", ", nullFeatureIds.Take(50));
                        //"codigo", "insumo", "confiabili", "cambio"
                        string msg = $"❌ Hay {nullFeatureIds.Count} entidades con atributos nulos.\ncambio, confiabili, codigo o insumo\nEj: {joined}";
                        if (nullFeatureIds.Count > 50)
                            msg += "...";
                        Utils.SendMessageToDockPane(msg, true);
                    }
                    else
                    {
                        Utils.SendMessageToDockPane("✅ Todos los atributos requeridos están diligenciados.", true);
                    }
                }
            }
        });
    }

    public static async Task ValidateChangedFeaturesByAttributeAsync()
    {
        await QueuedTask.Run(async () =>
        {
            var capaCorine = await Utils.GetDynamicLayer("capaCorine") as FeatureLayer;
            if (capaCorine == null)
            {
                Utils.SendMessageToDockPane("❌ No se encontró la capa Corine.", true);
                return;
            }

            var featureClass = capaCorine.GetFeatureClass();
            var noCambiados = new List<long>();
            var modificados = new List<long>();

            using (var cursor = featureClass.Search())
            {
                while (cursor.MoveNext())
                {
                    using (var row = cursor.Current)
                    {
                        int cambio = Convert.ToInt32(row["cambio"]);
                        long oid = row.GetObjectID();

                        if (cambio == 0)
                            noCambiados.Add(oid);
                        else
                            modificados.Add(oid);
                    }
                }
            }

            if (noCambiados.Any())
            {
                string listado = string.Join(", ", noCambiados.Take(10));
                if (noCambiados.Count > 10)
                    listado += "...";

                Utils.SendMessageToDockPane($"❌ {noCambiados.Count} polígonos con atributo cambio = 0 (no modificados).\nEj: {listado}", true);

                // Mostrar advertencia usando ArcGIS MessageBox
                var result = MessageBox.Show(
                    $"Se encontraron {noCambiados.Count} polígonos con cambio = 0.\n¿Deseas actualizar su valor a cambio = 1?",
                    "Advertencia",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    Utils.SendMessageToDockPane("cambiando atributos...", true);
                    var table = capaCorine.GetTable();
                    var editOperation = new EditOperation
                    {
                        Name = "Actualizar atributos CAMBIO",
                        SelectNewFeatures = false
                    };

                    foreach (var oid in noCambiados)
                    {
                        editOperation.Modify(table, oid, new Dictionary<string, object> { { "cambio", 1 } });
                    }

                    bool success = editOperation.Execute();
                    if (success)
                        Utils.SendMessageToDockPane("✅ Se actualizaron los valores de cambio = 0 a cambio = 1.", true);
                    else
                        Utils.SendMessageToDockPane("❌ Hubo un error al aplicar los cambios.", true);
                }
                else
                {
                    Utils.SendMessageToDockPane("⚠️ No se realizaron cambios en los atributos.", true);
                }
            }
            else
            {
                Utils.SendMessageToDockPane("✅ Todos los polígonos fueron modificados.", true);
            }

            Utils.SendMessageToDockPane(
                $"ℹ️ Total revisados: {noCambiados.Count + modificados.Count}\n✔️ Modificados: {noCambiados.Count}\n",true);

            // Mostrar botón en el dockpane (opcional) si se quisiera realizar la validacion desde el dockpane
            // var vm = FrameworkApplication.DockPaneManager.Find("ProAppModule2_Corine_Analysis_DockPane") as CorineAnalysisDockpaneViewModel;
            // if (vm != null)
            // {
            //     vm.CambioBotonVisibility = Visibility.Visible;
            //     vm.SetPoligonosNoCambiados(noCambiados);
            // }
        });
    }







}
