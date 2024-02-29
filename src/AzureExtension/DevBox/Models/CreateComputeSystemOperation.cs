﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.Contracts;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox.Models;

public delegate CreateComputeSystemOperation CreateComputeSystemOperationFactory(IDeveloperId developerId, DevBoxCreationParameters userOptions);

/// <summary>
/// Responsible for creating a new DevBox. It is the object return back to Dev Home so Dev Home can track the progress of the operation.
/// </summary>
public class CreateComputeSystemOperation : ICreateComputeSystemOperation
{
    private readonly IDevBoxCreationManager _devBoxCreationManager;

    private readonly IDeveloperId _developerId;

    private readonly object _lock = new();

    public CreateComputeSystemResult CompletionResult { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public ComputeSystemOperationData ProgressData { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public event TypedEventHandler<ICreateComputeSystemOperation, CreateComputeSystemResult>? Completed;

    public event TypedEventHandler<ICreateComputeSystemOperation, ComputeSystemOperationData>? Progress;

    public bool IsCompleted { get; private set; }

    public bool IsOperationInProgress { get; private set; }

    public DevBoxCreationParameters DevBoxCreationParameters { get; private set; }

    public CreateComputeSystemOperation(IDevBoxCreationManager devBoxCreationManager, IDeveloperId developerId, DevBoxCreationParameters parameters)
    {
        _devBoxCreationManager = devBoxCreationManager;
        DevBoxCreationParameters = parameters;
        _developerId = developerId;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (IsCompleted || IsOperationInProgress)
            {
                return;
            }

            IsOperationInProgress = true;
        }

        Task.Run(async () => await _devBoxCreationManager.StartCreateDevBoxOperation(this, _developerId, DevBoxCreationParameters));
    }

    public void UpdateProgress(string operationStatus, uint operationProgress)
    {
        if (IsCompleted)
        {
            return;
        }

        Progress?.Invoke(this, new ComputeSystemOperationData(operationStatus, operationProgress));
    }

    public void CompleteWithFailure(Exception exception, string displayText)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Completed?.Invoke(this, new CreateComputeSystemResult(exception, displayText, exception.Message));
        ResetAllSubscriptions();
    }

    public void CompleteWithSuccess(DevBoxInstance devBox)
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        Completed?.Invoke(this, new CreateComputeSystemResult(devBox));
        ResetAllSubscriptions();
    }

    private void ResetAllSubscriptions()
    {
        Completed = null;
        Progress = null;
    }
}