// --------------------------------------------------------------------------------------------------------------------
// <copyright file="App.xaml.cs" company="Tomáš Hübelbauer">
//   Copyright © Tomáš Hübelbauer 2015
// </copyright>
// <summary>
//   Interaction logic for App.xaml
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using log4net.Appender;
using log4net.Config;

namespace SingleInstance
{
  /// <summary>
  /// Interaction logic for App.xaml
  /// </summary>
  public partial class App
  {
    #region Constants

    /// <summary>
    /// A unique application name for mutex and pipe discovery purposes.
    /// </summary>
    private const String Name = "SingleInstance_A5515652-A588-4B8D-A9DE-49E141A23A78";

    #endregion

    #region Fields

    /// <summary>
    /// The mutex.
    /// </summary>
    private static Mutex mutex;

    /// <summary>
    /// The cancellation token source.
    /// </summary>
    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

    /// <summary>
    /// The log.
    /// </summary>
    private readonly ILog log = LogManager.GetLogger(typeof(App));

    #endregion

    #region Methods

    /// <summary>
    /// Handles the application startup logic.
    /// </summary>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    protected override async void OnStartup(StartupEventArgs e)
    {
      AppDomain.CurrentDomain.UnhandledException += (sender, e2) =>
      {
        log.Error("Unhandled eception.", (Exception)e2.ExceptionObject);
        var halt = true;
      };

      if (TryAcquireMutex())
      {
        this.log.InfoFormat("Process #{0} is the server.", Process.GetCurrentProcess().Id);

        // Use the constructor instead of the Run static method or variable assignment to avoid ReSharper await suggestion. In this case the task is not to be awaited as 
        new Task(async () =>
        {
          // Start the named pipe server stream thread in parallel with the main thread, until the application is shutting down at which point the mutex is released.
          await MonitorInstances(this.cancellationTokenSource.Token).ContinueWith(t => ReleaseMutex());
        }).Start();
      }
      else
      {
        this.log.InfoFormat("Process #{0} is the client.", Process.GetCurrentProcess().Id);

        // Hand the command line arguments over to the sole instance for them to be processed and exit self.
        await DelegateCommandLineArguments(e.Args);
        this.Shutdown();
      }

      base.OnStartup(e);
    }

    /// <summary>
    /// Handles the application exit logic.
    /// </summary>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    protected override void OnExit(ExitEventArgs e)
    {
      Debug.WriteLine("Exiting!");
      this.cancellationTokenSource.Cancel();
      this.cancellationTokenSource.Dispose();
      base.OnExit(e);
    }

    /// <summary>
    /// Attempts to acquire the mutex.
    /// </summary>
    /// <returns>
    /// <code>true</code>, if the mutex acquirement was successful; otherwise <code>false</code>.
    /// </returns>
    private static Boolean TryAcquireMutex()
    {
      var createdNew = default(Boolean);
      mutex = new Mutex(true, Name, out createdNew);
      return createdNew;
    }

    /// <summary>
    /// Releases and disposes the mutex.
    /// </summary>
    private static void ReleaseMutex()
    {
      Debug.WriteLine("Releasing and disposing the mutex.");
      mutex.ReleaseMutex();
      mutex.Dispose();
    }

    /// <summary>
    /// Monitors the application instances popping up, failing to acquire the named mutex and reporting the command line arguments to the sole instance.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation Token.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> represents an asynchronous operation.
    /// </returns>
    private static async Task MonitorInstances(CancellationToken cancellationToken)
    {
      // Create a named pipe server stream that listens to the named pipe clients handing over the command line arguments.
      using (var namedPipeServerStream = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
      {
        // Continue monitors the late instances until the sole instance is shutting down.
        while (!cancellationToken.IsCancellationRequested)
        {
          // Await a client connection.
          await new TaskFactory().FromAsync(namedPipeServerStream.BeginWaitForConnection, namedPipeServerStream.EndWaitForConnection, null);
        }

        Debug.WriteLine("Cancelled, ceasing the monitoring loop.");
      }
    }

    /// <summary>
    /// Hands over the command line arguments passed to the current instance to the sole instance running using named pipes.
    /// </summary>
    /// <param name="commandLineArguments">
    /// The command line arguments.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing an asynchronous operation.
    /// </returns>
    private static async Task DelegateCommandLineArguments(IEnumerable<String> commandLineArguments)
    {
      // Create a named pipe client stream that connects to the named pipe server in the sole instance.
      using (var namedPipeClientStream = new NamedPipeClientStream(".", Name, PipeDirection.Out))
      {
        // Connect to the named pipe server instance.
        namedPipeClientStream.Connect();

        // Write the command line arguments to the client outward stream a line each.
        using (var streamWriter = new StreamWriter(namedPipeClientStream))
        {
          streamWriter.AutoFlush = true;
          foreach (var commandLineArgument in commandLineArguments)
          {
            await streamWriter.WriteLineAsync(commandLineArgument);
          }
        }
      }
    }

    #endregion
  }
}