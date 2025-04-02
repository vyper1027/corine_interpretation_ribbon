# -*- coding: utf-8 -*-
import arcpy

class Toolbox:
    def __init__(self):
        """Define el toolbox."""
        self.label = "Analisis de Polígonos"
        self.alias = "analisis_poligonos"
        self.tools = [FiltrarPoligonos]

class FiltrarPoligonos:
    def __init__(self):
        """Define la herramienta."""
        self.label = "Filtrar Polígonos por Área"
        self.description = "Encuentra polígonos menores a 5 ha y menores a 25 ha."

    def getParameterInfo(self):
        """Define los parámetros de entrada y salida."""
        params = [
            arcpy.Parameter(
                displayName="Capa de entrada",
                name="input_layer",
                datatype="DEFeatureClass",
                parameterType="Required",
                direction="Input"
            ),
            arcpy.Parameter(
                displayName="Capa de salida (<5 ha)",
                name="output_layer_5ha",
                datatype="DEFeatureClass",
                parameterType="Required",
                direction="Output"
            ),
            arcpy.Parameter(
                displayName="Capa de salida (<25 ha)",
                name="output_layer_25ha",
                datatype="DEFeatureClass",
                parameterType="Required",
                direction="Output"
            )
        ]
        return params

    def execute(self, parameters, messages):
        """Ejecuta el análisis."""
        input_layer = parameters[0].valueAsText
        output_layer_5ha = parameters[1].valueAsText
        output_layer_25ha = parameters[2].valueAsText

        # Definir los filtros para áreas menores a 5 ha y 25 ha
        filtro_5ha = "Shape_Area < 50000"   # 5 ha = 50,000 m²
        filtro_25ha = "Shape_Area < 250000" # 25 ha = 250,000 m²

        arcpy.AddMessage("🔍 Buscando polígonos menores a 5 ha...")
        arcpy.analysis.Select(input_layer, output_layer_5ha, filtro_5ha)
        arcpy.AddMessage("✅ Polígonos menores a 5 ha guardados en: " + output_layer_5ha)

        arcpy.AddMessage("🔍 Buscando polígonos menores a 25 ha...")
        arcpy.analysis.Select(input_layer, output_layer_25ha, filtro_25ha)
        arcpy.AddMessage("✅ Polígonos menores a 25 ha guardados en: " + output_layer_25ha)
