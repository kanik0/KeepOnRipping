#pragma warning disable CS8602 // Dereference of a possibly null reference.
using Microsoft.AspNetCore.Mvc.RazorPages;
using AngleSharp;

namespace KeepOnRipping.Pages
{
    public class ArticleModel : PageModel
    {
        private readonly ILogger<ArticleModel> _logger;

        public string HtmlContent { get; set; } = string.Empty;

        private static readonly HttpClient client = new();

        public ArticleModel(ILogger<ArticleModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task OnPostAsync(string articleURL)
        {
            HtmlContent = "Loading..";

            // Handle Inserted URL and create two cleaned up URLs
            Uri ArticleURL;
            try
            {
                ArticleURL = new(articleURL);

                if (ArticleURL.Host != "repubblica.it" && ArticleURL.Host != "www.repubblica.it")
                {
                    HtmlContent = "<h1>Target site must be repubblica.it</h1>";
                    return;
                }
                
            }
            catch 
            {
                HtmlContent = "<h1>Invalid URL.</h1>";
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
                HtmlContent = $"Something went wrong (wrong URL?): {ex.Message}";
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
                HtmlContent = $"Something went wrong: {ex.Message}";
                return;
            }

            // Parse and build the final HTML
            HtmlContent = await ParseAndBuild(originalArticleHTML, ampArticleHTML);

        }

        private static async Task<string> ParseAndBuild(string article, string ampArticle)
        {
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
                return $"Couldn't find article-body or story__text: {ex}";
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
                return $"Couldn't find content in story: {ex}";
            }

            // Replace original article
            document.GetElementById("article-body").OuterHtml = story.OuterHtml;
            document.GetElementById("paywall").Remove();


            return document.DocumentElement.OuterHtml;
        }
    }
}
