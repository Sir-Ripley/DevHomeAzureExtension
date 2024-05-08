﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using AzureExtension.Contracts;
using AzureExtension.DevBox.Exceptions;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AzureExtension.DevBox.Helpers;

public class WingetConfigWrapper : IApplyConfigurationOperation, IDisposable
{
    // Example of the JSON payload for the customization task
    //  {
    //    "tasks": [
    //        {
    //            "name": "winget",
    //            "runAs": "User",
    //            "parameters": {
    //                "inlineConfigurationBase64": "..."
    //            },
    //        },
    //    ]
    //  }
    public const string WingetTaskJsonBaseStart = "{\"tasks\": [";

    public const string WingetTaskJsonTaskStart = @"{
            ""name"": ""winget"",
			""runAs"": ""User"",
            ""parameters"": {
                ""inlineConfigurationBase64"": """;

    public const string WingetTaskJsonTaskEnd = "\"}},";

    public const string WingetTaskJsonBaseEnd = "]}";

    public const string ConfigApplyFailedKey = "DevBox_ConfigApplyFailedKey";

    public const string ValidationFailedKey = "DevBox_ValidationFailedKey";

    public const string NotRunningFailedKey = "DevBox_NotRunningFailedKey";

    public event TypedEventHandler<IApplyConfigurationOperation, ApplyConfigurationActionRequiredEventArgs> ActionRequired = (s, e) => { };

    public event TypedEventHandler<IApplyConfigurationOperation, ConfigurationSetStateChangedEventArgs> ConfigurationSetStateChanged = (s, e) => { };

    private JsonSerializerOptions _taskJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private string _fullTaskJSON = string.Empty;

    private List<ConfigurationUnit> _units = new();

    private OpenConfigurationSetResult _openConfigurationSetResult = new(null, null, null, 0, 0);

    private ApplyConfigurationSetResult _applyConfigurationSetResult = new(null, null);

    private string _restAPI;

    private IDevBoxManagementService _managementService;

    private IDeveloperId _devId;

    private Serilog.ILogger _log;

    private string[] _oldUnitState = Array.Empty<string>();

    private bool _pendingNotificationShown;

    private ManualResetEvent _resumeEvent = new(false);

    private ComputeSystemState _computeSystemState;

    // Using a common failure result for all the tasks
    // since we don't get any other information from the REST API
    private ConfigurationUnitResultInformation _commonfailureResult = new ConfigurationUnitResultInformation(
            new WingetConfigurationException("Runtime Failure"), string.Empty, string.Empty, ConfigurationUnitResultSource.UnitProcessing);

    public WingetConfigWrapper(
        string configuration,
        string taskAPI,
        IDevBoxManagementService devBoxManagementService,
        IDeveloperId associatedDeveloperId,
        Serilog.ILogger log,
        ComputeSystemState computeSystemState)
    {
        _restAPI = taskAPI;
        _managementService = devBoxManagementService;
        _devId = associatedDeveloperId;
        _log = log;
        _computeSystemState = computeSystemState;

        // If the dev box isn't running, skip initialization
        // Later this logic will be changed to start the dev box
        if (_computeSystemState == ComputeSystemState.Running)
        {
            Initialize(configuration);
        }
    }

    public void Initialize(string configuration)
    {
        List<ConfigurationUnit> units = new();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var baseDSC = deserializer.Deserialize<TaskYAMLToCSClasses.BasePackage>(configuration);

        // Move the resources to a separate list
        var resources = baseDSC?.Properties?.Resources;

        // Remove the resources from the baseDSC
        // They will be added back, each as an individual task since we
        // cannot get individual task statuses for a single comprehensive task
        baseDSC?.Properties?.SetResources(null);

        if (resources != null)
        {
            // Start collecting the individual tasks, starting with the base
            StringBuilder fullTask = new(WingetTaskJsonBaseStart);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitEmptyCollections | DefaultValuesHandling.OmitNull)
                .Build();

            foreach (var resource in resources)
            {
                if (resource.Resource is not null && resource.Directives is not null)
                {
                    if (resource.Resource.Equals("Microsoft.WinGet.DSC/WinGetPackage", System.StringComparison.OrdinalIgnoreCase))
                    {
                        units.Add(new("WinGetPackage", resource.Id, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Apply));
                    }
                    else if (resource.Resource.EndsWith("GitDsc/GitClone", System.StringComparison.OrdinalIgnoreCase))
                    {
                        units.Add(new("GitClone", resource.Id, ConfigurationUnitState.Unknown, false, null, null, ConfigurationUnitIntent.Apply));
                    }
                }

                // Add the resource back as an individual task
                var tempDsc = baseDSC;

                // Remove "dependsOn:" from the configuration
                // This is a workaround as the current implementation breaks down the task into individual tasks
                // and we cannot have dependencies between them
                resource.DependsOn = null;
                tempDsc?.Properties?.SetResources(new List<TaskYAMLToCSClasses.ResourceItem> { resource });
                var yaml = serializer.Serialize(tempDsc);
                var encodedConfiguration = DevBoxOperationHelper.Base64Encode(yaml);
                fullTask.Append(WingetTaskJsonTaskStart + encodedConfiguration + WingetTaskJsonTaskEnd);
            }

            // Remove the last comma after the last task
            fullTask.Length--;
            fullTask.Append(WingetTaskJsonBaseEnd);
            _fullTaskJSON = fullTask.ToString();

            _units = units;
            _oldUnitState = Enumerable.Repeat(string.Empty, units.Count).ToArray();
        }
    }

    private void HandleEndState(TaskJSONToCSClasses.BaseClass response, bool isSetSuccessful = false)
    {
        List<ApplyConfigurationUnitResult> unitResults = new();
        for (var i = 0; i < _units.Count; i++)
        {
            var task = _units[i];
            if (isSetSuccessful || response.Tasks[i].Status == "Succeeded")
            {
                unitResults.Add(new(task, ConfigurationUnitState.Completed, false, false, null));
            }
            else
            {
                unitResults.Add(new(task, ConfigurationUnitState.Completed, false, false, _commonfailureResult));
            }
        }

        _applyConfigurationSetResult = new(null, unitResults);
    }

    private void SetStateForCustomizationTask(TaskJSONToCSClasses.BaseClass response)
    {
        var setState = DevBoxOperationHelper.JSONStatusToSetStatus(response.Status);
        _log.Debug($">>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>>Set Status: {response.Status}");

        // No need to show the pending status more than once
        if (_pendingNotificationShown && setState == ConfigurationSetState.Pending)
        {
            return;
        }

        switch (response.Status)
        {
            case "NotStarted":
                ConfigurationSetStateChanged?.Invoke(this, new(new(ConfigurationSetChangeEventType.SetStateChanged, setState, ConfigurationUnitState.Unknown, null, null)));
                _pendingNotificationShown = true;
                break;

            case "Running":
                bool isAnyTaskRunning = false;
                bool isWaitingForUserSession = false;
                for (var i = 0; i < _units.Count; i++)
                {
                    var responseStatus = response.Tasks[i].Status;
                    _log.Debug($"Unit Response Status: {responseStatus}");

                    // If the status is waiting for a user session, there is no need to check for other
                    // individual task statuses.
                    if (responseStatus == "WaitingForUserSession")
                    {
                        isWaitingForUserSession = true;
                        continue;
                    }

                    if (responseStatus == "Running")
                    {
                        isAnyTaskRunning = true;
                    }

                    if (_oldUnitState[i] != responseStatus)
                    {
                        var task = _units[i];
                        var unitState = DevBoxOperationHelper.JSONStatusToUnitStatus(responseStatus);
                        var resultInfo = responseStatus == "Failed" ? _commonfailureResult : null;
                        ConfigurationSetStateChangedEventArgs args = new(new(ConfigurationSetChangeEventType.UnitStateChanged, setState, unitState, resultInfo, task));
                        ConfigurationSetStateChanged?.Invoke(this, args);
                        _oldUnitState[i] = responseStatus;
                    }
                }

                // If waiting for user session and no task is running, show the adaptive card
                // We add a wait since Dev Boxes take at least 2 minutes to start applying
                // the configuration and we don't want to show the same message immediately after.
                if (isWaitingForUserSession && !isAnyTaskRunning)
                {
                    ApplyConfigurationActionRequiredEventArgs eventArgs = new(new WaitingForUserAdaptiveCardSession(_resumeEvent));
                    ActionRequired?.Invoke(this, eventArgs);
                    WaitHandle.WaitAny(new[] { _resumeEvent });
                }

                break;

            case "ValidationFailed":
                _openConfigurationSetResult = new(new FormatException(Resources.GetResource(ValidationFailedKey)), null, null, 0, 0);
                break;

            case "Succeeded":
                HandleEndState(response);
                break;

            case "Failed":
                HandleEndState(response);
                break;
        }
    }

    IAsyncOperation<ApplyConfigurationResult> IApplyConfigurationOperation.StartAsync()
    {
        return Task.Run(async () =>
        {
            try
            {
                if (_computeSystemState != ComputeSystemState.Running)
                {
                    throw new InvalidOperationException(Resources.GetResource(NotRunningFailedKey));
                }

                _log.Information($"Applying config {_fullTaskJSON}");

                HttpContent httpContent = new StringContent(_fullTaskJSON, Encoding.UTF8, "application/json");
                var result = await _managementService.HttpsRequestToDataPlane(new Uri(_restAPI), _devId, HttpMethod.Put, httpContent);

                var setStatus = string.Empty;
                while (setStatus != "Succeeded" && setStatus != "Failed" && setStatus != "ValidationFailed")
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    var poll = await _managementService.HttpsRequestToDataPlane(new Uri(_restAPI), _devId, HttpMethod.Get, null);
                    var rawResponse = poll.JsonResponseRoot.ToString();
                    var response = JsonSerializer.Deserialize<TaskJSONToCSClasses.BaseClass>(rawResponse, _taskJsonSerializerOptions);
                    setStatus = response?.Status;

                    if (response is not null)
                    {
                        SetStateForCustomizationTask(response);
                    }
                }

                return new ApplyConfigurationResult(_openConfigurationSetResult, _applyConfigurationSetResult);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unable to apply configuration {_fullTaskJSON}");
                return new ApplyConfigurationResult(ex, Resources.GetResource(Constants.DevBoxUnableToPerformOperationKey, ex.Message), ex.Message);
            }
        }).AsAsyncOperation();
    }

    void IDisposable.Dispose()
    {
        _resumeEvent?.Dispose();
        GC.SuppressFinalize(this);
    }
}
