using System;
using Google.Ads.Gax.Config;
using Google.Ads.GoogleAds.Config;
using Google.Ads.GoogleAds.Lib;
using Google.Ads.GoogleAds.V18.Errors;
using Google.Ads.GoogleAds.V18.Services;
using Google.Api;
using Google.Apis.Auth.OAuth2;
using GoogleAdsEngagement.Context;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GoogleAdsEngagement.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleAdsEngagement
{
    public class GoogleAdsEngagement
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;

        public GoogleAdsEngagement(ILoggerFactory loggerFactory, HttpClient httpClient, IConfiguration configuration, ApplicationDbContext context)
        {
            _logger = loggerFactory.CreateLogger<GoogleAdsEngagement>();
            _httpClient = httpClient;
            _configuration = configuration;
            _context = context;
            _configuration = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
        }

        [Function("GoogleAdsEngagement")]
        public async Task Run([TimerTrigger("0 0 * * 0")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            //await GetGoogleServiceAccountAsync();
            await GetMccAccountsAsync();
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }

        public async Task<string> GetGoogleServiceAccountAsync()
        {
            var socialSettings = await _context.SocialSettings
                            .Where(c => c.TokenFrom == "GoogleAds")
                            .ToListAsync();
            string relativePath = "";
            foreach (var setting in socialSettings)
            {
                if(setting.TokenKey == "ServiceAccountJsonPath")
                {
                    relativePath = setting.TokenValue;
                }
            }
            _logger.LogInformation($"relativepath: {relativePath}");
            var token = "";
            if (relativePath != null)
            {
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);

                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Service account file not found at {fullPath}");
                }

                string scope = "https://www.googleapis.com/auth/adwords";

                GoogleCredential credential;
                using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream)
                        .CreateScoped(new[] { scope });
                }

                token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync();
            }

            return token.ToString();
        }

        public async Task<string> GetMccAccountsAsync()
        {
            var socialSettings = await _context.SocialSettings
                            .Where(c => c.TokenFrom == "GoogleAds")
                            .ToListAsync();
            var setting = socialSettings.FirstOrDefault(c => c.TokenKey == "DeveloperToken");
            _logger.LogInformation($"setting: {setting.TokenValue}");

            var developerToken = socialSettings.FirstOrDefault(c => c.TokenKey == "DeveloperToken")?.TokenValue;
            var managerCustomerId = socialSettings.FirstOrDefault(c => c.TokenKey == "ManagerCustomerId")?.TokenValue;
            var serviceAccountJsonPath = socialSettings.FirstOrDefault(c => c.TokenKey == "ServiceAccountJsonPath")?.TokenValue;
            var impersonatedEmail = socialSettings.FirstOrDefault(c => c.TokenKey == "ImpersonatedEmail")?.TokenValue;

            GoogleAdsConfig config = new GoogleAdsConfig()
            {
                DeveloperToken = developerToken,
                OAuth2Mode = OAuth2Flow.SERVICE_ACCOUNT,
                OAuth2SecretsJsonPath = serviceAccountJsonPath,
                LoginCustomerId = managerCustomerId,
                OAuth2PrnEmail = impersonatedEmail
            };
            GoogleAdsClient client = new GoogleAdsClient(config);

            // Get the GoogleAdsService.
            GoogleAdsServiceClient googleAdsService = client.GetService(
                Google.Ads.GoogleAds.Services.V18.GoogleAdsService);

            // Create a query that will retrieve all campaigns.
            string query = @"SELECT
                    campaign.id,
                    campaign.name,
                    campaign.network_settings.target_content_network
                FROM campaign
                ORDER BY campaign.id";

            query = $@"
                SELECT
                    customer_client.client_customer,
                    customer_client.level,
                    customer_client.status,
                    customer_client.descriptive_name
                FROM customer_client
                WHERE customer_client.status = ENABLED";

            try
            {
                SearchGoogleAdsRequest request = new SearchGoogleAdsRequest
                {
                    CustomerId = managerCustomerId,
                    Query = query
                };

                var response = googleAdsService.Search(request);
                foreach (var customer in response)
                {
                    var clientCustomer = customer.CustomerClient.ClientCustomer;
                    string customerid = clientCustomer.Split('/')[1];

                    if (customerid != managerCustomerId)
                    {
                        // Log the clientCustomer value
                        _logger.LogInformation($"Customer Id: {customerid}");
                        // Query to get campaigns for each customer
                        string campaignQuery = @"
                            SELECT 
                                campaign.id, campaign.name, campaign.status, campaign.advertising_channel_type,
                                campaign.advertising_channel_sub_type, campaign.start_date, campaign.end_date,
                                campaign.bidding_strategy_type, campaign.network_settings.target_google_search,
                                campaign.network_settings.target_search_network, campaign.network_settings.target_content_network,
                                campaign.targeting_setting.target_restrictions, metrics.impressions, metrics.clicks,
                                metrics.video_views, metrics.ctr, metrics.average_cpc,
                                metrics.conversions, metrics.cost_micros, campaign.tracking_url_template, campaign.final_url_suffix,
                                campaign.experiment_type, campaign.labels, campaign.serving_status,
                                campaign.manual_cpc.enhanced_cpc_enabled
                            FROM campaign";

                        SearchGoogleAdsRequest request1 = new SearchGoogleAdsRequest
                        {
                            CustomerId = customerid,
                            Query = campaignQuery
                        };
                        // Fetch campaigns for the client customer
                        var campaignResponse = googleAdsService.Search(request1);
                        var clientData = await _context.ClientDetails
                            .Where(c => c.GoogleAdsId == long.Parse(customerid))
                            .FirstOrDefaultAsync();
                        if (clientData != null)
                        {
                            // Loop through the campaigns and display the data
                            foreach (var campaignRow in campaignResponse)
                            {
                                //_logger.LogInformation($"Campaign: {campaignRow}");
                                GoogleAdsEngagementModel engagement = new GoogleAdsEngagementModel
                                {
                                    ClientId = clientData.Id,
                                    CustomerId = long.Parse(customerid),
                                    CampaignId = campaignRow.Campaign.Id,
                                    Name = campaignRow.Campaign.Name,
                                    StartDate = DateTime.Parse(campaignRow.Campaign.StartDate),
                                    EndDate = DateTime.Parse(campaignRow.Campaign.EndDate),
                                    Clicks = (int)campaignRow.Metrics.Clicks,
                                    Impressions = (int)campaignRow.Metrics.Impressions,
                                    AverageCpc = Math.Round(campaignRow.Metrics.AverageCpc, 10),
                                    Ctr = Math.Round(campaignRow.Metrics.Ctr, 10)
                                };
                                _logger.LogInformation($"Campaign: {campaignRow}");
                                // Save to the database
                                _context.GoogleAdsEngagementModels.Add(engagement);
                                _context.SaveChanges();
                            }

                            var today = DateOnly.FromDateTime(DateTime.Today);
                            clientData.GoogleAdsRunDate = today;
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                return "";
            }
            catch (GoogleAdsException e)
            {
                Console.WriteLine("Failure:");
                Console.WriteLine($"Message: {e.Message}");
                Console.WriteLine($"Failure: {e.Failure}");
                Console.WriteLine($"Request ID: {e.RequestId}");
                return e.Message;
            }
        }

    }
}

