using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace KatyLauncher.API.Tools
{
    internal static class KatyLauncherAPIHelper
    {
        /// <summary>
        /// Check the update and ask the user to do so.
        /// </summary>
        public static async Task Check(string AppId)
        {
            Application application = new Application(AppId, Assembly.GetExecutingAssembly().GetName().Version.ToString());

            if (application.IsLauncherHooked)
                return;

            try
            {
                if (!await application.IsUpdated()) //  Check if a update is available.
                    if (MessageBox.Show("An update available, do you want to download it?", "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) //   ask the user.
                        if (Launcher.IsInstalled)
                            application.ForceLauncher();    //  Relaunch the app with the launcher
                        else
                            Process.Start(await application.GetAppWebsite());   //  Open a internet link
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                    Console.WriteLine(e.InnerException.Message);
                else
                    Console.WriteLine(e.Message);
            }
        }
    }
}