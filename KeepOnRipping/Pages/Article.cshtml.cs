#pragma warning disable CS8602 // Dereference of a possibly null reference.
using Microsoft.AspNetCore.Mvc.RazorPages;
using AngleSharp;
using System.Text;

namespace KeepOnRipping.Pages
{
    public class ArticleModel : PageModel
    {
        private readonly ILogger<ArticleModel> _logger;

        public class ArticleResponseObj
        {
            public bool IsSharedUrl { get; set; }
            public bool IsOk { get; set; }
            public string HtmlContent { get; set; }
            public ArticleResponseObj(bool issharedurl, bool isok, string htmlcontent)
            {
                IsSharedUrl = issharedurl;
                IsOk = isok;
                HtmlContent = htmlcontent;
            }
        }

        public string DecodedUrl { get; set; } = String.Empty;

        public ArticleResponseObj ArticleResponse { get; set; } = new(false, false, "");

        public string ShareUrl { get; set; } = String.Empty;

        private static readonly HttpClient client = new();

        public ArticleModel(ILogger<ArticleModel> logger)
        {
            _logger = logger;
        }

        public void OnGet(string? EncodedUrl)
        {
            ArticleResponse.IsSharedUrl = true;

            if (EncodedUrl is not null)
            {
                try
                {
                    DecodedUrl = Encoding.UTF8.GetString(Convert.FromBase64String(EncodedUrl));
                    ArticleResponse.IsOk = true;
                }
                catch
                {
                    ArticleResponse.IsOk = false;
                    ArticleResponse.HtmlContent = "Invalid Encoded URL.";
                    return;
                }
            }
            else
            {
                ArticleResponse.IsOk = false;
                ArticleResponse.HtmlContent = "No URL provided.";
            }

        }

        public async Task OnPostAsync(string articleURL)
        {
            // Set ShareUrl for footer
            ShareUrl = Convert.ToBase64String(Encoding.UTF8.GetBytes(articleURL));

            // Handle Inserted URL and create two cleaned up URLs
            Uri ArticleURL;
            try
            {
                ArticleURL = new(articleURL);

                if (!ArticleURL.Host.EndsWith("repubblica.it"))
                {
                    ArticleResponse.IsOk = false;
                    ArticleResponse.HtmlContent = "Target site must be repubblica.it.";
                    return;
                }
                
            }
            catch 
            {
                ArticleResponse.IsOk = false;
                ArticleResponse.HtmlContent = "Invalid URL.";
                return;
            }

            string cleanedURL = "https://" + ArticleURL.Host + ArticleURL.AbsolutePath;
            string ampURL = "https://" + ArticleURL.Host + ArticleURL.AbsolutePath + "amp/";


            // Requests to Repubblica
            // Original Article
            string originalArticleHTML;
            try {
                originalArticleHTML = await client.GetStringAsync(cleanedURL);
            }
            catch (Exception ex)
            {
                ArticleResponse.IsOk = false;
                ArticleResponse.HtmlContent = $"Something went wrong (wrong URL?): {ex.Message}";
                return;
            }
            // AMP Article
            string ampArticleHTML;
            try
            {
                ampArticleHTML = await client.GetStringAsync(ampURL);
            }
            catch (Exception ex)
            {
                ArticleResponse.IsOk = false;
                ArticleResponse.HtmlContent = $"Something went wrong: {ex.Message}";
                return;
            }

            // Parse and build the final HTML
            ArticleResponse = await ParseAndBuild(originalArticleHTML, ampArticleHTML);

        }

        private static async Task<ArticleResponseObj> ParseAndBuild(string article, string ampArticle)
        {
            ArticleResponseObj Output = new(false, false, "");

            // Use the default configuration for AngleSharp
            AngleSharp.IConfiguration config = Configuration.Default;
            // Create a new context for evaluating webpages with the given config
            IBrowsingContext context = BrowsingContext.New(config);

            // Parsing the two articles
            AngleSharp.Dom.IDocument document = await context.OpenAsync(req => req.Content(article));
            AngleSharp.Dom.IDocument ampDocument = await context.OpenAsync(req => req.Content(ampArticle));

            // Cleaning AMP article
            AngleSharp.Dom.IElement story;
            try
            {
                if (ampDocument.GetElementsByClassName("article-body").Length > 0)
                {
                    story = ampDocument.GetElementsByClassName("article-body")[0];
                }
                else
                {
                    story = ampDocument.GetElementsByClassName("story__text")[0];
                }                
            }
            catch (Exception ex)
            {
                Output.IsOk = false;
                Output.HtmlContent = $"Couldn't find article-body or story__text: {ex}";
                return Output;
            }

            try
            {
                story.QuerySelectorAll("[subscriptions-section='content-not-granted']")[0].Remove();
                story.QuerySelectorAll("[subscriptions-section='content']")[0].SetAttribute("subscriptions-section", "");
                story.ClassName = "story__content";
                story.QuerySelectorAll("[subscriptions-section='']")[0].ClassName = "story__text";
            }
            catch (Exception ex)
            {
                Output.IsOk = false;
                Output.HtmlContent = $"Couldn't find content in story: {ex}";
                return Output;
            }

            // Replace original article
            document.GetElementById("article-body").OuterHtml = story.OuterHtml;
            document.GetElementById("paywall").Remove();

            // Clean up for mobile devices
            if (document.GetElementById("social-share-top") is not null)
            {
                document.GetElementById("social-share-top").Remove();
            }

            Output.IsOk = true;
            Output.HtmlContent = document.DocumentElement.OuterHtml;
            return Output;
        }
    }
}
