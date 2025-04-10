// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full 
// license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace ArcGISTest
{
    internal static class TestResolver
    {
        private static Dictionary<string, string> assemblyMap = new Dictionary<string, string>();

        private static string _productId = "ArcGISPro";
        private static string _installDir = String.Empty;

        static TestResolver()
        {
        }

        static public void Install(string productId = "ArcGISPro")
        {
            _productId = productId;
            ConfigureResolver();
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CustomResolverHandler);
        }

        private static Assembly CustomResolverHandler(object sender, ResolveEventArgs args)
        {
            System.Reflection.Assembly retAsm = null;

            try
            {
                string filename = args.Name.Split(',')[0];

                if (filename.Contains(".resources.dll"))
                    return null;

                string folder = string.Empty;
                var match = assemblyMap.TryGetValue(filename, out folder);

                filename = String.Concat(filename, ".dll");

                if (match)
                {
                    retAsm ??= Assembly.LoadFrom(Path.Combine(folder, filename));
                }
                else
                {
                    retAsm ??= Assembly.LoadFrom(Path.Combine(GetProInstallLocation(),
                                                                               filename));
                }
            }
            catch
            {
                try
                {
                    retAsm ??= Assembly.LoadFrom(Path.Combine(
                                 GetProInstallLocation(), "Configurations", "Intelligence",
                                     "ArcGIS.Desktop.Intelligence.Configuration.dll"));
                }
                catch
                {
                }
            }

            return retAsm;
        }

        private static string GetProInstallLocation()
        {
            if (!String.IsNullOrEmpty(_installDir))
                return _installDir;

            try
            {
                var sk = Registry.LocalMachine.OpenSubKey(@$"Software\ESRI\{_productId}");
                _installDir = sk.GetValue("InstallDir") as string;
            }
            catch
            {
                try
                {
                    var sku = Registry.CurrentUser.OpenSubKey(@$"Software\ESRI\{_productId}");
                    _installDir = sku.GetValue("InstallDir") as string;
                }
                catch
                {
                }
            }

            _installDir = Path.Combine(_installDir, "bin");
            return _installDir;
        }

        private static void ConfigureResolver()
        {
            // Already configured
            if (assemblyMap.Count != 0)
            {
                return;
            }

            string installPath = GetProInstallLocation();
            string jsonPath = Path.Combine(installPath, "InstallDependencies.json");
            if (!File.Exists(jsonPath))
            {
                return;
            }

            try
            {
                string fileContent = File.ReadAllText(jsonPath);
                var jd = JsonDocument.Parse(fileContent);
                var root = jd.RootElement;
                var installationNode = root.GetProperty("Installation");

                var folders = installationNode.GetProperty("Folders");
                int count = folders.GetArrayLength();

                for (int i = 0; i < count; ++i)
                {
                    var folder = folders[i];
                    var folderPath = folder.GetProperty("Path");
                    var fullPath = Path.Combine(installPath, folderPath.ToString());

                    var assemblies = folder.GetProperty("Assemblies");
                    int asmCount = assemblies.GetArrayLength();

                    for (int j = 0; j < asmCount; ++j)
                    {
                        var assm = assemblies[j];
                        var asmName = assm.GetProperty("Name");

                        assemblyMap.Add(asmName.ToString(), fullPath);
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(
                          $"Error processing dependency file: {e.Message}");
            }
        }
    }
}