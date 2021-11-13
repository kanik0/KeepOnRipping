using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KeepOnRipping.Pages
{
    public class IndexModel : PageModel
    {
        public string AssemblyVersion { get; set; }

        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
