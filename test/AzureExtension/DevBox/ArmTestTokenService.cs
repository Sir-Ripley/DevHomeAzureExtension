﻿// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;

namespace AzureExtension.Test.DevBox;

public class ArmTestTokenService : IArmTokenService
{
    public async Task<string> GetTokenAsync()
    {
        await Task.Delay(0);
        return "eyJ0eXAiOiJKV1QiLCJyaCI6IjAuQWdFQXY0ajVjdkdHcjBHUnF5MTgwQkhiUjBaSWYza0F1dGRQdWtQYXdmajJNQk1hQVBBLiIsImFsZyI6IlJTMjU2IiwieDV0IjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIiwia2lkIjoiVDFTdC1kTFR2eVdSZ3hCXzY3NnU4a3JYUy1JIn0.eyJhdWQiOiJodHRwczovL21hbmFnZW1lbnQuYXp1cmUuY29tLyIsImlzcyI6Imh0dHBzOi8vc3RzLndpbmRvd3MubmV0LzcyZjk4OGJmLTg2ZjEtNDFhZi05MWFiLTJkN2NkMDExZGI0Ny8iLCJpYXQiOjE3MDA2MTA4MDEsIm5iZiI6MTcwMDYxMDgwMSwiZXhwIjoxNzAwNjE1NTU5LCJfY2xhaW1fbmFtZXMiOnsiZ3JvdXBzIjoic3JjMSJ9LCJfY2xhaW1fc291cmNlcyI6eyJzcmMxIjp7ImVuZHBvaW50IjoiaHR0cHM6Ly9ncmFwaC53aW5kb3dzLm5ldC83MmY5ODhiZi04NmYxLTQxYWYtOTFhYi0yZDdjZDAxMWRiNDcvdXNlcnMvMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwL2dldE1lbWJlck9iamVjdHMifX0sImFjciI6IjEiLCJhaW8iOiJBVlFBcS84VkFBQUFLQUc0TTJpbm9FNDdWS1JlN3Z2OWlaT09VWm16T2tlUEpua3JiT3NpNWdLY1N3NVJoMmNjbVJVK0VtTFdVaXFwWEV0UFdYa2FOT01pakhXMWcyNWRQUzZvQ0R3UTN1bXlqL1BuUGM5bGxuMD0iLCJhbXIiOlsicnNhIiwibWZhIl0sImFwcGlkIjoiODcyY2Q5ZmEtZDMxZi00NWUwLTllYWItNmU0NjBhMDJkMWYxIiwiYXBwaWRhY3IiOiIwIiwiZGV2aWNlaWQiOiIyMDZkNzZiYi01YjVjLTQ2MTEtOTcwNS04Yjk0NDE5MDIyZGQiLCJmYW1pbHlfbmFtZSI6IkRhbmlzaCIsImdpdmVuX25hbWUiOiJIdXphaWZhIiwiaWR0eXAiOiJ1c2VyIiwiaXBhZGRyIjoiMjAwMTo0ODk4OjgwZTg6YjpiMWE4OjFmODI6ZGVkNjpkNzdkIiwibmFtZSI6Ikh1emFpZmEgRGFuaXNoIiwib2lkIjoiMjhhZWJiYTgtOTk2Yi00MzRiLWJiNTctNDk2OWMwOTdjOGYwIiwib25wcmVtX3NpZCI6IlMtMS01LTIxLTIxMjc1MjExODQtMTYwNDAxMjkyMC0xODg3OTI3NTI3LTQxMTg5NDA0IiwicHVpZCI6IjEwMDMyMDAwOUQ1QzUwREUiLCJyaCI6IkkiLCJzY3AiOiJ1c2VyX2ltcGVyc29uYXRpb24iLCJzdWIiOiJIbGRsTDYxWFRIaFp5TWNhbGt3TG01cmFJMmtuMENNX3dtNFB1S1A5LWZjIiwidGlkIjoiNzJmOTg4YmYtODZmMS00MWFmLTkxYWItMmQ3Y2QwMTFkYjQ3IiwidW5pcXVlX25hbWUiOiJtb2RhbmlzaEBtaWNyb3NvZnQuY29tIiwidXBuIjoibW9kYW5pc2hAbWljcm9zb2Z0LmNvbSIsInV0aSI6ImU0NVJpU1IzMUVTZVdqbTc1STRFQUEiLCJ2ZXIiOiIxLjAiLCJ3aWRzIjpbImI3OWZiZjRkLTNlZjktNDY4OS04MTQzLTc2YjE5NGU4NTUwOSJdLCJ4bXNfY2FlIjoiMSIsInhtc190Y2R0IjoxMjg5MjQxNTQ3fQ.qvUtRY0Md9Y3XmC49tJVAzxzaGDver7B6G8Fvc1r66nk8sy9IWL4hWv8FBIKTALy-CdLzst3T7DmJFdk2X4VzbQx8BYG8XdGxaXI6BAz943fr40Aypz-Uo5YAleeL9GucRjlSYOSMMggBpPPVn1aeytZlmAlG2yEnuAze5OHuV0QkHl2ijcB-o5L20Kf3_wKZLDX6nsCmLZrs-w3Kf3U4m4Fo0aD9hye0uiWFtEdjERA1w-i0qSx1iUYXq5LHZD-uYTFh6f4qvz1ojzXGLj9_zvPEZpsw8mXMndud0nQWfPK-pGAkKkek9SeJVPpVSGSyG1-EYz7Z-QDcrzbRCs8uQ";
    }
}
