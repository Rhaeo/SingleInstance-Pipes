// --------------------------------------------------------------------------------------------------------------------
// <copyright file="App.xaml.cs" company="Tomáš Hübelbauer">
//   Copyright © Tomáš Hübelbauer 2015
// </copyright>
// <summary>
//   Interaction logic for App.xaml
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System.Threading;
using System.Windows;

namespace SingleInstance
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App
  {
    #region Methods

    /// <summary>
    /// Handles the application startup logic.
    /// </summary>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    protected override void OnStartup(StartupEventArgs e)
    {
      var createdNew = false;
      var mutex = new Mutex(true, "SingleInstance_A5515652-A588-4B8D-A9DE-49E141A23A78", out createdNew);
      if (createdNew)
      {
        // TODO: Start a named pipe server and listen for client connections with their command line arguments.
        MessageBox.Show("Acquired the mutex.");
        mutex.ReleaseMutex();
      }
      else
      {
        // TODO: Start a named pipe client and hand over the command line arguments to the server process.
        MessageBox.Show("Failed to acquire the mutex.");
        this.Shutdown();
      }

      base.OnStartup(e);
    }

    #endregion
  }
}