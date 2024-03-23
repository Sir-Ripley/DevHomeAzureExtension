﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Serilog;
using Windows.Storage;

internal sealed class Program
{
    private static void Main()
    {
        // Test console is set up with logging to test any component manually.
        Environment.SetEnvironmentVariable("DEVHOME_LOGS_ROOT", ApplicationData.Current.TemporaryFolder.Path);
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        Log.Information("Hello Azure");

        Log.CloseAndFlush();
    }
}
