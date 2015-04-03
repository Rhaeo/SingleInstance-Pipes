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
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
    /// The command line arguments.
    /// </summary>
    private static readonly ObservableCollection<IEnumerable<String>> CommandLineArguments = new ObservableCollection<IEnumerable<String>>();

    /// <summary>
    /// The mutex.
    /// </summary>
    private static Mutex mutex;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the command line arguments.
    /// </summary>
    public IReadOnlyCollection<IEnumerable<String>> CommandLineArgs
    {
      get
      {
        return CommandLineArguments;
      }
    }

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
      var createdNew = default(Boolean);
      mutex = new Mutex(true, Name, out createdNew);

      if (createdNew)
      {
        CommandLineArguments.Add(e.Args);
        await MonitorInstances();
      }
      else
      {
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
      // Dispose the mutex, never wait-one'd, so no need to release it either.
      mutex.Dispose();
      base.OnExit(e);
    }

    /// <summary>
    /// Monitors the application instances popping up, failing to acquire the named mutex and reporting the command line arguments to the sole instance.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> represents an asynchronous operation.
    /// </returns>
    private static async Task MonitorInstances()
    {
      // Create a named pipe server stream that listens to the named pipe clients handing over the command line arguments.
      using (var namedPipeServerStream = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
      using (var streamReader = new StreamReader(namedPipeServerStream))
      {
        // Continue monitors the late instances until the sole instance is shutting down.
        while (true)
        {
          // Await a client connection.
          await new TaskFactory().FromAsync(namedPipeServerStream.BeginWaitForConnection, namedPipeServerStream.EndWaitForConnection, null);

          // Collect the command line arguments to something enumerable so the handler can be reused for both original and received command line arguments.
          var commandLineArguments = new List<String>();
          while (!streamReader.EndOfStream)
          {
            commandLineArguments.Add(await streamReader.ReadLineAsync());
          }

          CommandLineArguments.Add(commandLineArguments);
          namedPipeServerStream.Disconnect();
        }
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