﻿// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using AzureExtension.DevBox;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;
using Log = AzureExtension.DevBox.Log;

namespace AzureExtension.Services.DevBox;

public class ARMTokenService : IArmTokenService
{
    private AuthenticationHelper AuthHelper => new();

    public async Task<string> GetTokenAsync(IDeveloperId? devId)
    {
        if (devId == null)
        {
            Log.Logger()?.ReportError($"ARMTokenService::GetTokenAsync: No dev id provided");
            return string.Empty;
        }

        try
        {
            var result = await AuthHelper.ObtainTokenForLoggedInDeveloperAccount(new string[] { Constants.ManagementPlaneScope }, devId.LoginId);
            return result?.AccessToken ?? string.Empty;
        }
        catch (Exception e)
        {
            Log.Logger()?.ReportError($"ARMTokenService::GetTokenAsync: {e.ToString}");
            return string.Empty;
        }
    }
}