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
    /// A unique application name for pipe discovery purposes.
    /// </summary>
    private const String Name = "SingleInstance_A5515652-A588-4B8D-A9DE-49E141A23A78";

    #endregion

    #region Fields

    /// <summary>
    /// The command line arguments.
    /// </summary>
    private static readonly ObservableCollection<IEnumerable<String>> CommandLineArguments = new ObservableCollection<IEnumerable<String>>();

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
      // Nothing to cancel, monitor the late instances from start to finish.
      var cancellationToken = CancellationToken.None;

      // Specifying interval of zero will only work on the local machine, the server will accept the connection immediately.
      // It will not be enough to work over the network. For that case, choose a low enough interval for humans not to wait too long (100 ms at most), but network to have enough time.
      if (await ConnectAsClient(e.Args, 0, cancellationToken))
      {
        this.Shutdown();
        return;
      }

      CommandLineArguments.Add(e.Args);
      await ConnectAsServer(cancellationToken);
      base.OnStartup(e);
    }

    /// <summary>
    /// Returns a value indicating whether the named pipe client stream succeeded in a connection attempt towards the named pipe server streams and delegated the command line arguments.
    /// </summary>
    /// <param name="commandLineArguments">
    /// The command Line Arguments.
    /// </param>
    /// <param name="timeout">
    /// The timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation Token.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing as asynchronous operation.
    /// </returns>
    private static async Task<Boolean> ConnectAsClient(IEnumerable<String> commandLineArguments, Int32 timeout, CancellationToken cancellationToken)
    {
      using (var namedPipeClientStream = new NamedPipeClientStream(".", Name, PipeDirection.Out))
      {
        try
        {
          await namedPipeClientStream.ConnectAsync(timeout, cancellationToken);
          using (var streamWriter = new StreamWriter(namedPipeClientStream))
          {
            foreach (var commandLineArgument in commandLineArguments)
            {
              await streamWriter.WriteLineAsync(commandLineArgument);
            }
          }

          return true;
        }
        catch (TimeoutException timeoutException)
        {
          return false;
        }
      }
    }

    /// <summary>
    /// Starts a named pipe server stream awaiting named pipe client stream connections and reading delegated command line arguments.
    /// </summary>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing as asynchronous operation.
    /// </returns>
    private static async Task ConnectAsServer(CancellationToken cancellationToken)
    {
      using (var namedPipeServerStream = new NamedPipeServerStream(Name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
      using (var streamReader = new StreamReader(namedPipeServerStream))
      {
        while (true)
        {
          await namedPipeServerStream.WaitForConnectionAsync(cancellationToken);
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

    #endregion
  }
}
