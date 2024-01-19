﻿// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

/// <summary>
/// As the actual implementation of IComputeSystem, DevBoxInstance is an instance of a DevBox.
/// It contains the DevBox details such as name, id, state, CPU, memory, and OS. And all the
/// operations that can be performed on the DevBox.
/// </summary>
public class DevBoxInstance : IComputeSystem
{
    private readonly IDevBoxAuthService _authService;

    public event TypedEventHandler<IComputeSystem, ComputeSystemState>? StateChanged;

    public IDeveloperId? DevId
    {
        get; private set;
    }

    public string? Name
    {
        get; private set;
    }

    public string? Id
    {
        get; private set;
    }

    public string? ProjectName
    {
        get; private set;
    }

    public string? State
    {
        get; private set;
    }

    public string? CPU
    {
        get; private set;
    }

    public string? Memory
    {
        get; private set;
    }

    public Uri? WebURI
    {
        get; private set;
    }

    public Uri? BoxURI
    {
        get; private set;
    }

    public Uri? RdpURI
    {
        get; private set;
    }

    public string? OS
    {
        get; private set;
    }

    public DevBoxInstance(IDevBoxAuthService devBoxAuthService)
    {
        _authService = devBoxAuthService;
    }

    /// <summary>
    /// Fills the DevBoxInstance from the JSON returned from the DevBox API.
    /// This includes the box name, id, state, CPU, memory, and OS.
    /// </summary>
    /// <param name="item">JSON that contains the dev box details</param>
    /// <param name="project">Project under which the dev box exists</param>
    /// <param name="devId">Developer ID for the dev box</param>
    public void FillFromJson(JsonElement item, string project, IDeveloperId? devId)
    {
        try
        {
            BoxURI = new Uri(item.GetProperty("uri").ToString());
            Name = item.GetProperty("name").ToString();
            Id = item.GetProperty("uniqueId").ToString();
            State = item.GetProperty("powerState").ToString();
            CPU = item.GetProperty("hardwareProfile").GetProperty("vCPUs").ToString();
            Memory = item.GetProperty("hardwareProfile").GetProperty("memoryGB").ToString();
            OS = item.GetProperty("imageReference").GetProperty("operatingSystem").ToString();
            ProjectName = project;
            DevId = devId;

            (WebURI, RdpURI) = GetRemoteLaunchURIsAsync(BoxURI).GetAwaiter().GetResult();
            Log.Logger()?.ReportInfo($"Created box {Name} with id {Id} with {State}, {CPU}, {Memory}, {BoxURI}, {OS}");
            IsValid = true;
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError($"Error making DevBox from JSON: {ex.ToString}");
        }
    }

    private async Task<(Uri? WebURI, Uri? RdpURI)> GetRemoteLaunchURIsAsync(Uri boxURI)
    {
        var connectionUri = $"{boxURI}/remoteConnection?{Constants.APIVersion}";
        var boxRequest = new HttpRequestMessage(HttpMethod.Get, connectionUri);
        var httpClient = _authService.GetDataPlaneClient(DevId);
        if (httpClient == null)
        {
            return (null, null);
        }

        var boxResponse = await httpClient.SendAsync(boxRequest);
        var content = await boxResponse.Content.ReadAsStringAsync();
        JsonElement json = JsonDocument.Parse(content).RootElement;
        var remoteUri = new Uri(json.GetProperty("rdpConnectionUrl").ToString());
        var webUrl = new Uri(json.GetProperty("webUrl").ToString());
        return (webUrl, remoteUri);
    }

    public ComputeSystemOperations SupportedOperations =>
        ComputeSystemOperations.Start | ComputeSystemOperations.ShutDown | ComputeSystemOperations.Delete;

    public bool IsValid
    {
        get;
        private set;
    }

    public string AlternativeDisplayName => throw new NotImplementedException();

    public IDeveloperId AssociatedDeveloperId => throw new NotImplementedException();

    public string AssociatedProviderId => throw new NotImplementedException();

    /// <summary>
    /// Common method to perform REST operations on the DevBox.
    /// This method is used by Start, ShutDown, Restart, and Delete.
    /// </summary>
    /// <param name="operation">Operation to be performed</param>
    /// <param name="method">HttpMethod to be used</param>
    /// <returns>ComputeSystemOperationResult created from the result of the operation</returns>
    private IAsyncOperation<ComputeSystemOperationResult> PerformRESTOperation(string operation, HttpMethod method)
    {
        return Task.Run(async () =>
        {
            try
            {
                var api = $"{BoxURI}:{operation}?{Constants.APIVersion}";
                Log.Logger()?.ReportInfo($"Starting {Name} with {api}");

                var httpClient = _authService.GetDataPlaneClient(DevId);
                if (httpClient == null)
                {
                    var ex = new ArgumentException("PerformRESTOperation: HTTPClient null");
                    return new ComputeSystemOperationResult(ex, string.Empty);
                }

                var response = await httpClient.SendAsync(new HttpRequestMessage(method, api));
                if (response.IsSuccessStatusCode)
                {
                    var res = new ComputeSystemOperationResult();
                    return res;
                }
                else
                {
                    var ex = new HttpRequestException($"PerformRESTOperation: {operation} failed on {Name}: {response.StatusCode} {response.ReasonPhrase}");
                    return new ComputeSystemOperationResult(ex, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"PerformRESTOperation: Exception: {operation} failed on {Name}: {ex.ToString}");
                return new ComputeSystemOperationResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemOperationResult> StartAsync(string options)
    {
        // ToDo: Change this event
        StateChanged?.Invoke(this, ComputeSystemState.Starting);
        return PerformRESTOperation("start", HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> ShutDownAsync(string options)
    {
        return PerformRESTOperation("stop", HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> RestartAsync(string options)
    {
        return PerformRESTOperation("restart", HttpMethod.Post);
    }

    public IAsyncOperation<ComputeSystemOperationResult> DeleteAsync(string options)
    {
        return PerformRESTOperation("delete", HttpMethod.Delete);
    }

    /// <summary>
    /// Launches the DevBox in a browser.
    /// </summary>
    /// <param name="options">Unused parameter</param>
    public IAsyncOperation<ComputeSystemOperationResult> ConnectAsync(string options)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = WebURI?.ToString();
                Process.Start(psi);
                return new ComputeSystemOperationResult();
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error connecting to {Name}: {ex.ToString}");
                return new ComputeSystemOperationResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    public IAsyncOperation<ComputeSystemStateResult> GetStateAsync(string options)
    {
        return Task.Run(() =>
        {
            try
            {
                if (State == "Running")
                {
                    return new ComputeSystemStateResult(ComputeSystemState.Running);
                }
                else if (State == "Deallocated")
                {
                    return new ComputeSystemStateResult(ComputeSystemState.Stopped);
                }
                else
                {
                    Log.Logger()?.ReportError($"Unknown state {State}");
                    return new ComputeSystemStateResult(ComputeSystemState.Unknown);
                }
            }
            catch (Exception ex)
            {
                Log.Logger()?.ReportError($"Error getting state of {Name}: {ex.ToString}");
                return new ComputeSystemStateResult(ex, string.Empty);
            }
        }).AsAsyncOperation();
    }

    // Unsupported operations
    public IAsyncOperation<ComputeSystemOperationResult> ApplyConfigurationAsync(string configuration) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> RevertSnapshotAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> CreateSnapshotAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> DeleteSnapshotAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> PauseAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> ResumeAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> SaveAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> TerminateAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemOperationResult> ModifyPropertiesAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<ComputeSystemThumbnailResult> GetComputeSystemThumbnailAsync(string options) => throw new NotImplementedException();

    public IAsyncOperation<IEnumerable<ComputeSystemProperty>> GetComputeSystemPropertiesAsync(string options) => throw new NotImplementedException();

    IAsyncOperationWithProgress<ComputeSystemOperationResult, ComputeSystemOperationData> IComputeSystem.ApplyConfigurationAsync(string configuration) => throw new NotImplementedException();
}
