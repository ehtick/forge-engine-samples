﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bimangle.ForgeEngine.Georeferncing;
using Bimangle.ForgeEngine.Revit.Config;
using Bimangle.ForgeEngine.Revit.Core;
using Bimangle.ForgeEngine.Revit.Utility;
using RevitUI = Autodesk.Revit.UI;

namespace Bimangle.ForgeEngine.Revit.Toolset.CreateProj
{
    [Transaction(TransactionMode.Manual)]
    public class CommandToolsetCreateProj : IExternalCommand, IExternalCommandAvailability
    {
        #region Implementation of IExternalCommand

        Result IExternalCommand.Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var document = commandData.View.Document;
                var homePath = VersionInfo.GetHomePath();

                var appConfig = AppConfigManager.Load();
                var localConfig = appConfig.Cesium3DTiles;

                var adapter = new GeoreferncingAdapter(document, localConfig);
                var owner = commandData.GetMainWindowHandle();
                using (var host = GeoreferncingHost.Create(adapter, homePath))
                {
                    GeoreferncingHelper.ShowProjCreateUI(owner, host);
                }
            }
            catch (Exception ex)
            {
                RevitUI.TaskDialog.Show(@"Error", ex.ToString());

                Trace.WriteLine(ex.ToString());
            }

            return Result.Succeeded;
        }

        #endregion

        #region Implementation of IExternalCommandAvailability

        bool IExternalCommandAvailability.IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return true;
        }

        #endregion

    }
}
