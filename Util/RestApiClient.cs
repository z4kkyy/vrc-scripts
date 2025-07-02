using System.Text;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.StringLoading;
using VRC.SDK3.Data;
using VRC.Udon.Common.Interfaces;

public class RestApiClient : UdonSharpBehaviour
{
    [Header("API Settings")]
    [SerializeField] private VRCUrl apiBaseUrl;
    [SerializeField] private VRCUrl allExportEndpointUrl;
    [SerializeField] private VRCUrl glovesExportEndpointUrl;
    [SerializeField] private VRCUrl patreonMembersEndpointUrl;

    // public
    public string[] PatreonMemberNames { get; private set; }
    public bool IsPatreonDataLoading { get; private set; }
    public bool IsGlovesDataLoading { get; private set; }
    public bool IsAllExportDataLoading { get; private set; }
    public bool IsAnyRequestInProgress => IsPatreonDataLoading || IsGlovesDataLoading || IsAllExportDataLoading;

    // Constants
    private const string SERVICE_KEY = "service";
    private const string RESULT_KEY = "result";
    private const string DATA_KEY = "data";
    private const string GLOVES_SERVICE = "gloves";
    private const string PATREON_SERVICE = "patreon";

    void Start()
    {
        ValidateEndpoints();
    }

    private void ValidateEndpoints()
    {
        if (apiBaseUrl == null || !apiBaseUrl.Get().StartsWith("https://") || !apiBaseUrl.Get().EndsWith("/"))
            Debug.LogError("[RestApiClient] API base URL is not set or invalid.");

        if (allExportEndpointUrl == null)
            Debug.LogWarning("[RestApiClient] All export endpoint URL is not set.");

        if (glovesExportEndpointUrl == null)
            Debug.LogWarning("[RestApiClient] Gloves export endpoint URL is not set.");

        if (patreonMembersEndpointUrl == null)
            Debug.LogWarning("[RestApiClient] Patreon members endpoint URL is not set.");
    }

    public void FetchAllExport()
    {
        Debug.Log("[RestApiClient] Fetching All Export data from: " + allExportEndpointUrl.ToString());
        IsAllExportDataLoading = true;
        VRCStringDownloader.LoadUrl(allExportEndpointUrl, (IUdonEventReceiver)this);
    }

    public void FetchGlovesExport()
    {
        Debug.Log("[RestApiClient] Fetching Gloves Export data from: " + glovesExportEndpointUrl.ToString());
        IsGlovesDataLoading = true;
        VRCStringDownloader.LoadUrl(glovesExportEndpointUrl, (IUdonEventReceiver)this);
    }

    public void FetchPatreonMembers()
    {
        Debug.Log("[RestApiClient] Fetching Patreon Members data from: " + patreonMembersEndpointUrl.ToString());
        IsPatreonDataLoading = true;
        VRCStringDownloader.LoadUrl(patreonMembersEndpointUrl, (IUdonEventReceiver)this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        string resultAsUTF8 = result.Result;
        string requestType = result.Url.Get().Replace(apiBaseUrl.Get(), "");
        Debug.Log("[RestApiClient] Successfully loaded data for: " + requestType);
        ParseApiResponse(resultAsUTF8, requestType);
        ResetLoadingFlags(requestType);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        string requestType = result.Url.Get().Replace(apiBaseUrl.Get(), "");
        Debug.LogError("[RestApiClient] Error loading data for: " + requestType);
        ResetLoadingFlags(requestType);
    }

    private void ResetLoadingFlags(string requestType)
    {
        switch (requestType)
        {
            case "all/export":
                IsAllExportDataLoading = false;
                break;
            case "gloves/export":
                IsGlovesDataLoading = false;
                break;
            case "patreon/members":
                IsPatreonDataLoading = false;
                break;
            default:
                Debug.LogWarning("[RestApiClient] Unrecognized request type: " + requestType);
                break;
        }
    }

    private void ParseApiResponse(string response, string requestType)
    {
        if (!VRCJson.TryDeserializeFromJson(response, out DataToken result))
        {
            Debug.LogError("[RestApiClient] Failed to parse API response: " + response);
            return;
        }

        if (result.TokenType == TokenType.DataDictionary)
        {
            ProcessSingleResponse(result.DataDictionary);
        }
        else if (result.TokenType == TokenType.DataList)
        {
            ProcessListResponse(result.DataList, requestType);
        }
        else
        {
            Debug.LogWarning("[RestApiClient] Unrecognized token type in API response: " + result.TokenType);
        }
    }

    private void ProcessSingleResponse(DataDictionary dataDictionary)
    {
        if (!ValidateResponseStructure(dataDictionary, out DataToken serviceToken, out DataToken dataToken))
            return;

        ProcessServiceData(serviceToken.String, dataToken.DataDictionary);
    }

    private void ProcessListResponse(DataList dataList, string requestType)
    {
        if (requestType != "all/export")
        {
            Debug.LogWarning("[RestApiClient] Expected 'all/export' response to be a DataList");
            return;
        }

        for (int i = 0; i < dataList.Count; i++)
        {
            DataToken item = dataList[i];
            if (item.TokenType != TokenType.DataDictionary)
            {
                Debug.LogWarning($"[RestApiClient] Expected DataDictionary in DataList, but found: {item.TokenType}");
                continue;
            }

            if (!ValidateResponseStructure(item.DataDictionary, out DataToken serviceToken, out DataToken dataToken))
                continue;

            ProcessServiceData(serviceToken.String, dataToken.DataDictionary);
        }
    }

    private bool ValidateResponseStructure(DataDictionary dataDictionary, out DataToken serviceToken, out DataToken dataToken)
    {
        serviceToken = new DataToken();
        dataToken = new DataToken();

        if (!dataDictionary.TryGetValue(SERVICE_KEY, out serviceToken) ||
            !dataDictionary.TryGetValue(RESULT_KEY, out DataToken resultToken) ||
            !dataDictionary.TryGetValue(DATA_KEY, out dataToken))
        {
            Debug.LogWarning("[RestApiClient] Missing expected keys in API response.");
            return false;
        }

        Debug.Log($"[RestApiClient] Service: {serviceToken.String}, Result: {resultToken.Boolean}");

        if (dataToken.TokenType != TokenType.DataDictionary)
        {
            Debug.LogWarning("[RestApiClient] Expected 'data' to be a DataDictionary, but found: " + dataToken.TokenType);
            return false;
        }

        return true;
    }

    private void ProcessServiceData(string service, DataDictionary dataDict)
    {
        switch (service)
        {
            case GLOVES_SERVICE:
                Debug.Log("[RestApiClient] Handling gloves/export response.");
                ParseGlovesExportJson(dataDict);
                break;
            case PATREON_SERVICE:
                Debug.Log("[RestApiClient] Handling patreon/members response.");
                ParsePatreonMembersJson(dataDict);
                break;
            default:
                Debug.LogWarning("[RestApiClient] Unhandled service type: " + service);
                break;
        }
    }

    private void ParseGlovesExportJson(DataDictionary dataDictionary)
    {
        if (!dataDictionary.TryGetValue("gloves", out DataToken glovesList) ||
            glovesList.TokenType != TokenType.DataList)
        {
            Debug.LogWarning("[RestApiClient] Gloves export response does not contain 'gloves' as a DataList.");
            return;
        }

        for (int i = 0; i < glovesList.DataList.Count; i++)
        {
            DataToken gloveName = glovesList.DataList[i];
            if (gloveName.TokenType == TokenType.String)
            {
                Debug.Log($"[RestApiClient] Glove {i}: {gloveName.String}");
                // Do something here
            }
            else
            {
                Debug.LogWarning($"[RestApiClient] Glove {i} is not a string: {gloveName.TokenType}");
            }
        }
    }

    private void ParsePatreonMembersJson(DataDictionary dataDictionary)
    {
        DataList membersList = dataDictionary.GetKeys();
        string[] tmpPatreonMemberNames = new string[membersList.Count];

        for (int i = 0; i < membersList.Count; i++)
        {
            string memberName = membersList[i].String;
            Debug.Log($"[RestApiClient] Patreon Member {i}: {memberName}");
            tmpPatreonMemberNames[i] = memberName;
        }

        PatreonMemberNames = tmpPatreonMemberNames;
    }
}
