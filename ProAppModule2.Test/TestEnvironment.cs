using ArcGIS.Core;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Internal.Core;
using ArcGISTest;
using System.Diagnostics;
using System.Windows.Markup;

namespace ProAppModule2.Test
{
    [ArcGISTestClass]
    public static class TestEnvironment
    {
        [AssemblyInitialize()]
        public static void AssemblyInitialize(TestContext testContext)
        {
            StartApplication();
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            StopApplication();
        }

        /// <summary>
        /// Starts an instance of ArcGIS Pro Application
        /// </summary>
        public static async void StartApplication()
        {
            var evt = new System.Threading.ManualResetEvent(false);
            System.Threading.Tasks.Task ready = null;

            var uiThread = new System.Threading.Thread(() =>
            {
                try
                {
                    Application = new ProApp();
                    ready = Application.TestModeInitializeAsync();
                    evt.Set();
                }
                catch (XamlParseException)
                {
                    throw new FatalArcGISException("Pro is not licensed");
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    evt.Set();
                }

                System.Windows.Threading.Dispatcher.Run();
            });

            uiThread.TrySetApartmentState(System.Threading.ApartmentState.STA);
            uiThread.Name = "Test UI Thread";
            uiThread.IsBackground = true;
            uiThread.Start();

            evt.WaitOne(); // Task ready to wait on.

            if (ready != null)
            {
                try
                {
                    await ready;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Shuts down the ArcGIS Pro Application instance
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
             "Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static void StopApplication()
        {
            try
            {
                if (Application != null)
                    Application.Shutdown();
            }
            catch (Exception e)
            {
                //do not re-throw the exception here.
                Debug.Print("Application.Shutdown threw an exception that was ignored. message: {0}", e.Message);
            }
        }

        /// <summary>
        /// Get an instance of ArcGIS Pro Application
        /// </summary>
        public static ProApp Application { get; private set; }
    }
}