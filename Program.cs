using System;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using PetNestMonitor.Storage;

namespace PetNestMonitor;

record DogListing(string Id, string Name, string Breed, string Gender, string Age, string City, string State, string PostedOn, string DetailUrl);

class Program
{
    private static readonly HttpClient _httpClient = new HttpClient();

    private static readonly IConfiguration _config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

    private static string TelegramBotToken =>
        _config["TelegramBotToken"]
        ?? Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
        ?? "YOUR_BOT_TOKEN";

    private static string TelegramChatId =>
        _config["TelegramChatId"]
        ?? Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
        ?? "YOUR_CHAT_ID";

    private static string TargetUrl =>
        _config["TargetUrl"]
        ?? "https://thepetnest.com/adopt-a-dog";

    private static int PagesToScan =>
        int.TryParse(_config["PagesToScan"], out var p) ? p : 1;

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[{DateTime.UtcNow}] Starting execution...");
        try
        {
            await RunMonitorAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical Execution Error: {ex.Message}");
        }
    }

    private static IStateStore CreateStateStore()
    {
        string storageType = _config["StorageType"] ?? "File";
        return storageType switch
        {
            "TableStorage" => new TableStorageStateStore(
                _config["AzureStorageConnectionString"]
                ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING")
                ?? ""),
            _ => new FileStateStore("seen_dogs.json")
        };
    }

    private static async Task RunMonitorAsync()
    {
        var store = CreateStateStore();
        var seenDogs = await store.LoadSeenIdsAsync();
        int newCount = 0;

        for (int page = 1; page <= PagesToScan; page++)
        {
            string url = BuildPageUrl(TargetUrl, page);
            Console.WriteLine($"Scanning page {page}: {url}");

            var listings = await ScrapeListingsAsync(url);
            if (listings.Count == 0)
            {
                Console.WriteLine($"No listings found on page {page}. Stopping pagination.");
                break;
            }

            foreach (var dog in listings)
            {
                if (seenDogs.Contains(dog.Id))
                    continue;

                Console.WriteLine($"New listing: {dog.Name} ({dog.Breed}) in {dog.City} [ID: {dog.Id}]");
                await SendTelegramNotificationAsync(dog);
                seenDogs.Add(dog.Id);
                newCount++;
            }
        }

        await store.SaveSeenIdsAsync(seenDogs);
        Console.WriteLine($"[{DateTime.UtcNow}] Done. {newCount} new listing(s) notified.");
    }

    private static string BuildPageUrl(string baseUrl, int page)
    {
        if (baseUrl.Contains("page="))
        {
            return Regex.Replace(baseUrl, @"page=\d+", $"page={page}");
        }

        char separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}page={page}";
    }

    private static async Task<List<DogListing>> ScrapeListingsAsync(string url)
    {
        var results = new List<DogListing>();

        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var doc = await web.LoadFromWebAsync(url);

        var detailLinks = doc.DocumentNode.SelectNodes("//a[contains(., 'See more details')]");
        if (detailLinks == null)
        {
            Console.WriteLine("No listings found in response body.");
            return results;
        }

        foreach (var linkNode in detailLinks)
        {
            string detailUrl = linkNode.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrEmpty(detailUrl)) continue;

            if (!detailUrl.StartsWith("http"))
                detailUrl = "https://thepetnest.com" + detailUrl;

            string dogId = detailUrl.Split('/').LastOrDefault()?.Split('?').FirstOrDefault() ?? detailUrl;

            // Walk up to the listing card container
            var container = linkNode.ParentNode;
            while (container != null && !container.InnerHtml.Contains("Posted on:"))
            {
                container = container.ParentNode;
            }
            if (container == null) continue;

            string cardText = HtmlEntity.DeEntitize(container.InnerText ?? "");

            // Extract dog name from "<Name> for adoption" link text
            var nameLink = container.SelectSingleNode(".//a[contains(., 'for adoption')]");
            string name = "Unknown";
            if (nameLink != null)
            {
                string linkText = HtmlEntity.DeEntitize(nameLink.InnerText).Trim();
                name = Regex.Replace(linkText, @"\s*for adoption\s*$", "", RegexOptions.IgnoreCase).Trim();
            }
            if (string.IsNullOrWhiteSpace(name)) name = "Unknown";

            // Extract breed from the URL path segment: /adopt-a-pet/{breed}-in-{city}/{id}
            string breed = "Unknown";
            var urlMatch = Regex.Match(detailUrl, @"/adopt-a-pet/(.+)-in-(.+)/\d+");
            if (urlMatch.Success)
            {
                breed = urlMatch.Groups[1].Value.Replace("-", " ");
                breed = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(breed);
            }

            // Extract gender and age from card text (e.g., "Male , Adulthood" or "Female , Puppyhood")
            string gender = "Unknown";
            string age = "Unknown";
            var genderAgeMatch = Regex.Match(cardText, @"(Male|Female)\s*,\s*(Puppyhood|Adulthood|Senior)");
            if (genderAgeMatch.Success)
            {
                gender = genderAgeMatch.Groups[1].Value;
                age = genderAgeMatch.Groups[2].Value;
            }

            // Extract city and state
            string city = "Unknown";
            string state = "Unknown";
            if (urlMatch.Success)
            {
                city = urlMatch.Groups[2].Value.Replace("-", " ");
                city = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(city);
            }
            var stateMatch = Regex.Match(cardText, @"(?:Bengaluru|Delhi|Mumbai|Pune|Chennai|Hyderabad|Gurgaon|Noida|Greater Noida|Alwar|[A-Z][a-z]+(?:\-[A-Z][a-z]+)*)\s*,\s+([A-Za-z\-]+)");
            if (stateMatch.Success)
            {
                state = stateMatch.Groups[1].Value.Replace("-", " ");
            }

            // Extract posted date
            string postedOn = "";
            var dateMatch = Regex.Match(cardText, @"Posted on:\s*(\d{1,2}\s+\w+,\s*\d{4})");
            if (dateMatch.Success)
            {
                postedOn = dateMatch.Groups[1].Value;
            }

            results.Add(new DogListing(dogId, name, breed, gender, age, city, state, postedOn, detailUrl));
        }

        return results;
    }

    private static async Task SendTelegramNotificationAsync(DogListing dog)
    {
        if (TelegramBotToken == "YOUR_BOT_TOKEN" || TelegramChatId == "YOUR_CHAT_ID")
        {
            Console.WriteLine("[Warning] Telegram credentials missing. Alert outputted to terminal only.");
            Console.WriteLine($"  >>> {dog.Name} | {dog.Breed} | {dog.City}, {dog.State} | {dog.DetailUrl}");
            return;
        }

        string message =
            $"🐾 *New Dog Listed for Adoption!*\n\n" +
            $"*Name:* {EscapeMarkdown(dog.Name)}\n" +
            $"*Breed:* {EscapeMarkdown(dog.Breed)}\n" +
            $"*Gender:* {dog.Gender} | *Age:* {dog.Age}\n" +
            $"*Location:* {EscapeMarkdown(dog.City)}, {EscapeMarkdown(dog.State)}\n" +
            (string.IsNullOrEmpty(dog.PostedOn) ? "" : $"*Posted:* {dog.PostedOn}\n") +
            $"\n[View Details]({dog.DetailUrl})";

        string apiUri = $"https://api.telegram.org/bot{TelegramBotToken}/sendMessage";

        var payload = new { chat_id = TelegramChatId, text = message, parse_mode = "Markdown" };
        var jsonPayload = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        try
        {
            var response = await _httpClient.PostAsync(apiUri, jsonPayload);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Telegram API error {response.StatusCode}: {body}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed connection to Telegram API endpoint: {ex.Message}");
        }
    }

    private static string EscapeMarkdown(string text) =>
        Regex.Replace(text, @"([_*\[\]()~`>#+\-=|{}.!])", @"\$1");
}
