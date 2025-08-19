using System.Configuration;
using System.Data;
using System.Windows;

namespace ControlBox
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Box.Start();
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Box.Stop();
            base.OnExit(e);
        }
    }

}
