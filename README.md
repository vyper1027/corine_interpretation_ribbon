# ArcGIS Pro Add-In: Herramientas de Generalización  

## 📌 Descripción  
Este Add-In para **ArcGIS Pro**, desarrollado en **.NET (C#)**, optimiza el proceso de generalización cartográfica mediante la automatización de reglas de negocio y la reducción de la carga operativa. Su objetivo principal es mejorar la eficiencia en la interpretación de datos espaciales, minimizando la necesidad de intervención manual en tareas repetitivas.  

## 🚀 Características  
- **Selección espacial avanzada** sobre capas vectoriales  
- **Automatización de reglas de negocio**, como la identificación de áreas menores a 5 ha  
- **Análisis de polígonos vecinos** y sus propiedades  
- **Actualización automatizada de atributos**  
- **Integración con herramientas de ArcGIS Pro** para reducir tiempos de edición  

## 🛠️ Tecnologías Utilizadas  
- **Lenguaje:** C#  
- **Framework:** .NET (ArcGIS Pro SDK)  
- **Plataforma:** ArcGIS Pro  
- **Control de versiones:** Git + GitHub  

## 📦 Instalación  
### Requisitos Previos  
- **ArcGIS Pro** 3.3  
- **ArcGIS Pro SDK para .NET**  
- **Visual Studio** 2022  

### Pasos para la Instalación  
1. **Clonar el repositorio:**  
   ```sh  
   git clone https://github.com/vyper1027/corine_interpretation_ribbon.git  
   cd corine_interpretation_ribbon 
   ```  
2. **Abrir el proyecto en Visual Studio**  
   - Cargar el archivo `.sln` en Visual Studio  
   - Verificar dependencias del SDK de ArcGIS Pro  
3. **Compilar el Add-In**  
   - Seleccionar la configuración `Debug` o `Release`  
   - Compilar y generar el archivo `.esriAddInX`  
4. **Instalar el Add-In en ArcGIS Pro**  
   - Copiar el archivo `.esriAddInX` en la carpeta de complementos de ArcGIS Pro  
   - Activar el Add-In desde la interfaz del software  

## 📌 Uso del Add-In  
1. **Cargar las capas de datos en ArcGIS Pro**  
2. **Ejecutar las herramientas del Add-In** desde la barra de herramientas  
3. **Seleccionar un polígono o permitir la selección automatizada**  
4. **Aplicación de reglas de generalización:**  
   - Evaluación de condiciones espaciales y atributos  
   - Identificación de polígonos vecinos  
   - Modificación automática de atributos según las reglas definidas  
5. **Resultados:** Se muestra un mensaje de confirmación y se actualizan los datos  

## 🐛 Reporte de Problemas y Sugerencias  
Si encuentras algún error o deseas proponer mejoras, puedes abrir un **issue** en este repositorio de GitHub.  

## 📜 Licencia  
Este proyecto está licenciado bajo la **Apache License 2.0**. Para más detalles, consulta el archivo [LICENSE](LICENSE).  

## 📣 Créditos  
**Desarrollado por:** Vyper1027  


