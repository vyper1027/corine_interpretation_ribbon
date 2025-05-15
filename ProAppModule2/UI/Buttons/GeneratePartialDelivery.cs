using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ProAppModule2.UI.ComboBoxes;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Core.Data.DDL;
using FieldDescription = ArcGIS.Core.Data.DDL.FieldDescription;


namespace ProAppModule2.UI.Buttons
{
    internal class GeneratePartialDelivery : Button
    {
        public class ClippedFeature
        {
            public Geometry Geometry { get; set; }
            public Dictionary<string, object> Attributes { get; set; } = new();
        }

        protected override void OnClick()
        {
            var (field, value, mensaje) = GetSeleccionCombo();

            if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(value))
            {
                Utils.SendMessageToDockPane("⚠️ No hay valores seleccionados en el combo mes de entrega");
                return;
            }

            Utils.SendMessageToDockPane($"✅ Valor seleccionado:\n{mensaje}");

            _ = QueuedTask.Run(async () =>
            {
                // 1. Obtener geometría de recorte
                var geometry = await GetGeometryFromAreaCorteAsync(field, value);
                if (geometry == null)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo obtener la geometría de recorte.");
                    return;
                }
                Utils.SendMessageToDockPane("✅ Geometría de recorte obtenida correctamente.");

                // 2. Generar nombre versionado
                string fullPath = await GenerateVersionedOutputNameAsync(field, value);
                string featureClassName = System.IO.Path.GetFileName(fullPath);

                // 3. Obtener capa original para copiar definición
                var capaCorine = await Utils.GetDynamicLayer("capaCorine") as FeatureLayer;
                if (capaCorine == null)
                {
                    Utils.SendMessageToDockPane("❌ No se encontró la capa 'capaCorine'.");
                    return;
                }

                // 4. Crear clase de entidad vacía en el dataset
                bool creada = await CrearFeatureClassAsync(featureClassName, capaCorine);
                if (!creada)
                {
                    Utils.SendMessageToDockPane("❌ No se pudo crear la clase de entidad en la GDB.", true);
                    return;
                }

                // 5. Realizar recorte
                var clippedFeatures = await ClipLayerAsync(geometry);
                if (clippedFeatures == null || clippedFeatures.Count == 0)
                {
                    Utils.SendMessageToDockPane("❌ No se generaron recortes.");
                    return;
                }

                bool insertado = await InsertarGeometriasAsync(featureClassName, clippedFeatures);

                if (insertado)
                    Utils.SendMessageToDockPane($"📦 Recorte guardado correctamente como '{featureClassName}' en 'Entrega'.", true);
                else
                    Utils.SendMessageToDockPane("❌ Error al insertar los recortes en la geodatabase.", true);
            });
        }

        private async Task<bool> InsertarGeometriasAsync(string featureClassName, List<ClippedFeature> features)
        {
            return await QueuedTask.Run(() =>
            {
                try
                {
                    string gdbPath = Project.Current.DefaultGeodatabasePath;

                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                    {
                        var allDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();
                        var internalPath = allDefs
                            .Select(d => d.GetName())
                            .FirstOrDefault(n =>
                                string.Equals(n, featureClassName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(System.IO.Path.GetFileName(n), featureClassName, StringComparison.OrdinalIgnoreCase));

                        if (string.IsNullOrEmpty(internalPath))
                        {
                            var available = string.Join("\n", allDefs.Select(d => d.GetName()));
                            Utils.SendMessageToDockPane($"❌ No se encontró '{featureClassName}' en la GDB.\n📂 Contenido:\n{available}");
                            return false;
                        }

                        using (var featureClass = geodatabase.OpenDataset<FeatureClass>(internalPath))
                        {
                            geodatabase.ApplyEdits(() =>
                            {
                                var def = featureClass.GetDefinition();
                                string shapeField = def.GetShapeField();

                                var insertFields = def.GetFields()
                                                      .Where(f =>
                                                          f.FieldType != FieldType.OID &&
                                                          f.FieldType != FieldType.GlobalID &&
                                                          f.FieldType != FieldType.Geometry)
                                                      .ToList();

                                using (var insertCursor = featureClass.CreateInsertCursor())
                                {
                                    foreach (var clipped in features)
                                    {
                                        using (var rowBuffer = featureClass.CreateRowBuffer())
                                        {
                                            rowBuffer[shapeField] = clipped.Geometry;

                                            foreach (var field in insertFields)
                                            {
                                                if (!field.IsEditable)
                                                    continue;

                                                if (clipped.Attributes.TryGetValue(field.Name, out var val))
                                                {
                                                    if (val == null && !field.IsNullable)
                                                        continue;

                                                    rowBuffer[field.Name] = val ?? DBNull.Value;
                                                }
                                            }

                                            insertCursor.Insert(rowBuffer);
                                        }
                                    }

                                    insertCursor.Flush();
                                }
                            });
                        }

                        Utils.SendMessageToDockPane($"✅ {features.Count} geometrías con atributos insertadas en '{featureClassName}'.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error al insertar geometrías: {ex.Message}");
                    return false;
                }
            });
        }

        private (string field, string value, string mensaje) GetSeleccionCombo()
        {
            var mes = SelectFeature.ComboMes?.SelectedValue;
            var bloque = SelectFeature.ComboBloque?.SelectedValue;
            var plancha = SelectFeature.ComboPlancha?.SelectedValue;

            if (!string.IsNullOrWhiteSpace(mes))
                return ("Mes_Interpretacion", mes, $"   🗓️ Mes: {mes}\n");

            if (!string.IsNullOrWhiteSpace(bloque) || !string.IsNullOrWhiteSpace(plancha))
            {
                Utils.SendMessageToDockPane("⚠️ La entrega solo está permitida por *Mes de entrega*. Selecciona un mes válido.");
            }

            return (null, null, null);
        }


        private async Task<string> GenerateVersionedOutputNameAsync(string fieldName, string value)
        {
            return await QueuedTask.Run(() =>
            {
                string gdbPath = Project.Current.DefaultGeodatabasePath;
                string datasetName = "Entrega";
                string baseName = $"{fieldName}_{value}_Recorte".Replace(" ", "_");

                int maxVersion = 0;

                using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                {
                    try
                    {
                        var fcDefs = geodatabase.GetDefinitions<FeatureClassDefinition>();

                        foreach (var def in fcDefs)
                        {
                            string fullName = def.GetName(); // Ej: "Entrega\\Mes_Interpretacion_4_Recorte_V1" o solo "Mes_Interpretacion_4_Recorte_V1"

                            string nameToCheck = fullName;
                            if (fullName.StartsWith($"{datasetName}\\"))
                                nameToCheck = fullName.Substring($"{datasetName}\\".Length);

                            if (nameToCheck.StartsWith(baseName))
                            {
                                var match = System.Text.RegularExpressions.Regex.Match(nameToCheck, @"_V(\d+)$");

                                if (match.Success && int.TryParse(match.Groups[1].Value, out int version))
                                {
                                    maxVersion = Math.Max(maxVersion, version);
                                }
                                else if (nameToCheck.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                                {
                                    maxVersion = Math.Max(maxVersion, 1);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Utils.SendMessageToDockPane($"⚠️ Error al revisar versiones en dataset '{datasetName}': {ex.Message}");
                        return System.IO.Path.Combine(gdbPath, datasetName, baseName + "_V1");
                    }
                }

                int nextVersion = maxVersion + 1;
                string finalName = $"{baseName}_V{nextVersion}";

                return System.IO.Path.Combine(gdbPath, datasetName, finalName);
            });
        }

        public static async Task<Geometry> GetGeometryFromAreaCorteAsync(string fieldName, string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            return await QueuedTask.Run(() =>
            {
                var map = Project.Current.GetItems<MapProjectItem>()
                    .FirstOrDefault(m => m.Name == "Ventana1")?.GetMap();

                if (map == null)
                {
                    MessageBox.Show("No se encontró el mapa 'Ventana1'.");
                    return null;
                }

                var layer = map.GetLayersAsFlattenedList().OfType<FeatureLayer>()
                    .FirstOrDefault(l => l.Name == "Area_corte_periodos");

                if (layer == null)
                {
                    MessageBox.Show("No se encontró la capa 'Area_corte_periodos' en el mapa 'Ventana1'.");
                    return null;
                }

                bool isNumeric = int.TryParse(value, out _);
                var whereClause = isNumeric ? $"{fieldName} = {value}" : $"{fieldName} = '{value.Replace("'", "''")}'";
                var queryFilter = new QueryFilter { WhereClause = whereClause };

                var geometries = new List<Geometry>();
                using (var cursor = layer.Search(queryFilter))
                {
                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        if (feature?.GetShape() is Geometry geom)
                            geometries.Add(geom);
                    }
                }

                return geometries.Count > 0 ? GeometryEngine.Instance.Union(geometries) : null;
            });
        }

        public async Task<List<ClippedFeature>> ClipLayerAsync(Geometry geometry)
        {
            var clippedFeatures = new List<ClippedFeature>();

            var capaCorine = await Utils.GetDynamicLayer("capaCorine");
            if (capaCorine == null)
            {
                Utils.SendMessageToDockPane("❌ No se encontró la capa 'capaCorine'.");
                return clippedFeatures;
            }

            await QueuedTask.Run(() =>
            {
                var def = capaCorine.GetFeatureClass().GetDefinition();
                var fieldNames = def.GetFields()
                                    .Where(f => f.FieldType != FieldType.Geometry && f.FieldType != FieldType.OID && f.FieldType != FieldType.GlobalID)
                                    .Select(f => f.Name)
                                    .ToList();

                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = geometry,
                    SpatialRelationship = SpatialRelationship.Intersects,
                    SubFields = "*"
                };

                using (var cursor = capaCorine.Search(spatialFilter))
                {
                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        var shape = feature?.GetShape();

                        if (shape != null)
                        {
                            //var clipped = GeometryEngine.Instance.Clip(shape, geometry.Extent);
                            var clipped = GeometryEngine.Instance.Intersection(shape, geometry);
                            if (clipped != null && !clipped.IsEmpty)
                            {
                                var attributes = new Dictionary<string, object>();
                                foreach (var name in fieldNames)
                                {
                                    attributes[name] = feature[name];
                                }

                                clippedFeatures.Add(new ClippedFeature
                                {
                                    Geometry = clipped,
                                    Attributes = attributes
                                });
                            }
                        }
                    }
                }
            });

            Utils.SendMessageToDockPane($"✂️ Se generaron {clippedFeatures.Count} recortes con atributos.");
            return clippedFeatures;
        }

        private async Task<bool> CrearFeatureClassAsync(string featureClassName, FeatureLayer capaReferencia)
        {
            return await QueuedTask.Run(() =>
            {
                try
                {
                    string gdbPath = Project.Current.DefaultGeodatabasePath;
                    string datasetName = "Entrega";

                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                    {
                        var def = capaReferencia.GetFeatureClass().GetDefinition();
                        var spatialRef = def.GetSpatialReference();

                        var datasetDesc = new FeatureDatasetDescription(datasetName, spatialRef);
                        var schemaBuilder = new SchemaBuilder(geodatabase);

                        // Obtener todos los dominios disponibles en la GDB
                        var dominios = geodatabase.GetDomains()
                            .OfType<CodedValueDomain>()
                            .ToDictionary(d => d.GetName(), d => d);

                        var fieldDescriptions = new List<FieldDescription>();

                        foreach (var field in def.GetFields())
                        {
                            if (field.FieldType is FieldType.OID or FieldType.GlobalID or FieldType.Geometry)
                                continue;

                            var newField = new FieldDescription(field.Name, field.FieldType)
                            {
                                Length = field.Length
                            };

                            // Verificar si el campo tiene dominio asignado y si está presente en la GDB
                            var domain = field.GetDomain() as CodedValueDomain;
                            if (domain != null)
                            {
                                string domainName = domain.GetName();
                                if (dominios.TryGetValue(domainName, out var existingDomain))
                                {
                                    var domainDesc = new CodedValueDomainDescription(existingDomain);
                                    newField.SetDomainDescription(domainDesc);
                                    Utils.SendMessageToDockPane($"🔗 Asignado dominio '{domainName}' al campo '{field.Name}'", true);
                                }
                                else
                                {
                                    Utils.SendMessageToDockPane($"⚠️ Dominio '{domainName}' no encontrado para el campo '{field.Name}'");
                                }
                            }

                            fieldDescriptions.Add(newField);
                        }

                        var shapeDesc = new ShapeDescription(def);
                        var fcDescription = new FeatureClassDescription(featureClassName, fieldDescriptions, shapeDesc);
                        Utils.SendMessageToDockPane("Generando capa de entrega...", true);
                        schemaBuilder.Create(datasetDesc, fcDescription);

                        if (!schemaBuilder.Build())
                        {
                            Utils.SendMessageToDockPane($"❌ Error al crear clase:\n{string.Join("\n", schemaBuilder.ErrorMessages)}");
                            return false;
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Utils.SendMessageToDockPane($"❌ Error creando feature class: {ex.Message}");
                    return false;
                }
            });
        }

    }
}
