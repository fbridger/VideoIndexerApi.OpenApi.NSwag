using Azure.Core;
using Azure.Identity;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VideoIndexerApi;

namespace VideoIndexer.OpenApi.NSwag
{
    public class Program
    {
        private const string ApiVersion = "2021-11-10-preview";
        private const string AzureResourceManager = "https://management.azure.com";
        // Complete using your Video Indexer Account
        private const string SubscriptionId = "<<Video Indexer SubscriptionId>>";
        private const string ResourceGroup = "<<Video Indexer ResourceGroup>>";
        private const string AccountName = "<<Video Indexer AccountName>>";
        private const string VideoId = "<<Get VideoId from Video Indexer Portal>>";

        public static async Task Main(string[] args)
        {
            // ********************************************************************************
            // IMPORTANT: If this FAILS, go to Tools => Options => Azure Service Authentication
            // and sign in using the correct Azure Account
            // ********************************************************************************
            // Build Azure Video Analyzer for Media resource provider client that has access token throuhg ARM
            VideoIndexerResourceProviderClient videoIndexerResourceProviderClient = await VideoIndexerResourceProviderClient.BuildVideoIndexerResourceProviderClient();

            // Get account details
            Account account = await videoIndexerResourceProviderClient.GetAccount();
            string accountLocation = account.Location;
            string accountId = account.Properties.Id;

            // Get account level access token for Azure Video Analyzer for Media 
            string accountAccessToken = await videoIndexerResourceProviderClient.GetAccessToken(ArmAccessTokenPermission.Contributor, ArmAccessTokenScope.Account);

            System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;

            // Create the http client
            HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };
            HttpClient client = new HttpClient(handler);
            VideoIndexerClient videoIndexerClient = new VideoIndexerClient(client);

            Location9 location = Enum.Parse<Location9>(accountLocation, true);

            // Get Video Index 
            VideoIndexContainer videoIndexContainer = await videoIndexerClient.GetVideoIndexAsync(location, accountId, VideoId, null, null, null, null, null, null, accountAccessToken, null);
            /*
            The operation above is generating the following Exception:
            Inner Exception 1:
JsonSerializationException: Error converting value "en-US" to type 'VideoIndexerApi.LanguageV2'. Path 'videos[0].insights.sourceLanguage', line 1, position 22166.

Inner Exception 2:
ArgumentException: Could not cast or convert from System.String to VideoIndexerApi.LanguageV2.
            */

            Console.WriteLine(JsonSerializer.Serialize(videoIndexContainer));

            Console.WriteLine("\nPress Enter to exit...");
            string line = Console.ReadLine();
            if (line == "enter")
            {
                System.Environment.Exit(0);
            }
        }


        public class VideoIndexerResourceProviderClient
        {
            private readonly string armAccessToken;

            public static async Task<VideoIndexerResourceProviderClient> BuildVideoIndexerResourceProviderClient()
            {
                TokenRequestContext tokenRequestContext = new TokenRequestContext(new[] { $"{AzureResourceManager}/.default" });
                DefaultAzureCredential credentials = new DefaultAzureCredential();
                AccessToken tokenRequestResult = await credentials.GetTokenAsync(tokenRequestContext);
                return new VideoIndexerResourceProviderClient(tokenRequestResult.Token);
            }
            public VideoIndexerResourceProviderClient(string armAaccessToken)
            {
                armAccessToken = armAaccessToken;
            }

            /// <summary>
            /// Generates an access token. Calls the generateAccessToken API  (https://github.com/Azure/azure-rest-api-specs/blob/main/specification/vi/resource-manager/Microsoft.VideoIndexer/preview/2021-11-10-preview/vi.json#:~:text=%22/subscriptions/%7BsubscriptionId%7D/resourceGroups/%7BresourceGroupName%7D/providers/Microsoft.VideoIndexer/accounts/%7BaccountName%7D/generateAccessToken%22%3A%20%7B)
            /// </summary>
            /// <param name="permission"> The permission for the access token</param>
            /// <param name="scope"> The scope of the access token </param>
            /// <param name="videoId"> if the scope is video, this is the video Id </param>
            /// <param name="projectId"> If the scope is project, this is the project Id </param>
            /// <returns> The access token, otherwise throws an exception</returns>
            public async Task<string> GetAccessToken(ArmAccessTokenPermission permission, ArmAccessTokenScope scope, string videoId = null, string projectId = null)
            {
                AccessTokenRequest accessTokenRequest = new AccessTokenRequest
                {
                    PermissionType = permission,
                    Scope = scope,
                    VideoId = videoId,
                    ProjectId = projectId
                };

                Console.WriteLine($"\nGetting access token: {JsonSerializer.Serialize(accessTokenRequest)}");

                // Set the generateAccessToken (from video indexer) http request content
                try
                {
                    string jsonRequestBody = JsonSerializer.Serialize(accessTokenRequest);
                    StringContent httpContent = new StringContent(jsonRequestBody, System.Text.Encoding.UTF8, "application/json");

                    // Set request uri
                    string requestUri = $"{AzureResourceManager}/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroup}/providers/Microsoft.VideoIndexer/accounts/{AccountName}/generateAccessToken?api-version={ApiVersion}";
                    HttpClient client = new HttpClient(new HttpClientHandler());
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", armAccessToken);

                    HttpResponseMessage result = await client.PostAsync(requestUri, httpContent);

                    VerifyStatus(result, System.Net.HttpStatusCode.OK);
                    string jsonResponseBody = await result.Content.ReadAsStringAsync();
                    Console.WriteLine($"Got access token: {scope} {videoId}, {permission}");
                    return JsonSerializer.Deserialize<GenerateAccessTokenResponse>(jsonResponseBody).AccessToken;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }

            /// <summary>
            /// Gets an account. Calls the getAccount API (https://github.com/Azure/azure-rest-api-specs/blob/main/specification/vi/resource-manager/Microsoft.VideoIndexer/preview/2021-11-10-preview/vi.json#:~:text=%22/subscriptions/%7BsubscriptionId%7D/resourceGroups/%7BresourceGroupName%7D/providers/Microsoft.VideoIndexer/accounts/%7BaccountName%7D%22%3A%20%7B)
            /// </summary>
            /// <returns> The Account, otherwise throws an exception</returns>
            public async Task<Account> GetAccount()
            {
                Console.WriteLine($"Getting account {AccountName}.");
                Account account;
                try
                {
                    // Set request uri
                    string requestUri = $"{AzureResourceManager}/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroup}/providers/Microsoft.VideoIndexer/accounts/{AccountName}?api-version={ApiVersion}";
                    HttpClient client = new HttpClient(new HttpClientHandler());
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", armAccessToken);

                    HttpResponseMessage result = await client.GetAsync(requestUri);

                    VerifyStatus(result, System.Net.HttpStatusCode.OK);
                    string jsonResponseBody = await result.Content.ReadAsStringAsync();
                    account = JsonSerializer.Deserialize<Account>(jsonResponseBody);
                    VerifyValidAccount(account);
                    Console.WriteLine($"The account ID is {account.Properties.Id}");
                    Console.WriteLine($"The account location is {account.Location}");
                    return account;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }

            private static void VerifyValidAccount(Account account)
            {
                if (string.IsNullOrWhiteSpace(account.Location) || account.Properties == null || string.IsNullOrWhiteSpace(account.Properties.Id))
                {
                    Console.WriteLine($"{nameof(AccountName)} {AccountName} not found. Check {nameof(SubscriptionId)}, {nameof(ResourceGroup)}, {nameof(AccountName)} ar valid.");
                    throw new Exception($"Account {AccountName} not found.");
                }
            }
        }

        public class AccessTokenRequest
        {
            [JsonPropertyName("permissionType")]
            public ArmAccessTokenPermission PermissionType { get; set; }

            [JsonPropertyName("scope")]
            public ArmAccessTokenScope Scope { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; }

            [JsonPropertyName("videoId")]
            public string VideoId { get; set; }
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum ArmAccessTokenPermission
        {
            Reader,
            Contributor,
            MyAccessAdministrator,
            Owner,
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum ArmAccessTokenScope
        {
            Account,
            Project,
            Video
        }

        public class GenerateAccessTokenResponse
        {
            [JsonPropertyName("accessToken")]
            public string AccessToken { get; set; }
        }

        public class AccountProperties
        {
            [JsonPropertyName("accountId")]
            public string Id { get; set; }
        }

        public class Account
        {
            [JsonPropertyName("properties")]
            public AccountProperties Properties { get; set; }

            [JsonPropertyName("location")]
            public string Location { get; set; }
        }


        public static void VerifyStatus(HttpResponseMessage response, System.Net.HttpStatusCode excpectedStatusCode)
        {
            if (response.StatusCode != excpectedStatusCode)
            {
                string message = response.Content.ReadAsStringAsync().Result;
                throw new Exception(response.ToString() + message);
            }
        }
    }
}
