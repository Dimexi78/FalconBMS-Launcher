using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;

namespace FalconBMS.Launcher
{
    /// <summary>
    /// App.xaml
    /// </summary>
    public partial class App
    {
        public App()
        {
            // Wine's WPF hardware path can render popup/dropdown content black or corrupt.
            // Keep native Windows on the normal hardware-rendered path.
            if (WineCompatibility.IsRunningUnderWine())
            {
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
                Diagnostics.Log("Wine detected: WPF software rendering enabled.");
            }

            Current.DispatcherUnhandledException += App_DispatcherUnhandledException;
            Diagnostics.Log("Application Initialization starting.");
        }

        private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is NotImplementedException)
            {
                MessageBox.Show(Program.mainWin, "This feature has not yet been implemented.", "Feature not Implemented",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                e.Handled = true;
                return;
            }

            if (Debugger.IsAttached)
            {
                Debug.WriteLine(e.Exception.ToString());
                Debugger.Break();
            }

            Diagnostics.Log(e.Exception);
            Diagnostics.ShowErrorMsgbox(e.Exception);

            e.Handled = true;
            return;
        }
    }
}
