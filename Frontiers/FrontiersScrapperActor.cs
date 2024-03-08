using System.Net;
using System.Net.Http.Json;
using Nest;
using Proto;

class FrontiersScrapperActor : IActor
{
    private readonly HttpClient _httpClient;
    private readonly IElasticClient _elasticClient;

    public FrontiersScrapperActor(HttpClient httpClient, IElasticClient elasticClient)
    {
        _httpClient = httpClient;
        _elasticClient = elasticClient;
        ScrollPageNumber = 0;
    }

    public int ScrollPageNumber { get; private set; }

    public async Task ReceiveAsync(IContext context)
    {
        if (context.Message is StartScrapping startScrapping)
        {
            while (true)
            {
                var articles = await GetArticles(ScrollPageNumber);
                if (articles.Articles.Length == 0)
                {
                    break;
                }

                var response = await _elasticClient.IndexManyAsync(articles.Articles);
                if (response.Errors)
                {
                    throw new Exception("Failed to index articles to elastic search");
                }

                ScrollPageNumber++;
            }
        }
    }

    private async Task<SearchResponse> GetArticles(int scrollPage)
    {
        try
        {
            var payload = new SearchPayload(Array.Empty<int>(), new SearchFilter(0, 0, 0, 0, 0, 0), false, 0,
                string.Empty,
                10, scrollPage);
            var response =
                await _httpClient.PostAsJsonAsync("https://www.frontiersin.org/api/v2/articles/search", payload);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await HandleDelay();
                return EmptySearchResponse();
            }

            return await response.Content.ReadFromJsonAsync<SearchResponse>() ??
                   throw new Exception("No articles found");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            await HandleDelay();
            return EmptySearchResponse();
        }

        async Task HandleDelay()
        {
            await Task.Delay(TimeSpan.FromMinutes(15));
        }

        SearchResponse EmptySearchResponse()
        {
            return new SearchResponse(Array.Empty<Article>());
        }
    }

    record SearchPayload(int[] ArticleIds, SearchFilter Filter, bool IsEditorsPickFilterEnabled, int PageNumber, string Search, int Top, int ScrollPageNumber);

    record SearchFilter(int ArticleType, int Date, int DomainId, int JournalId, int SectionId, int Sort);

    record SearchResponse(Article[] Articles);

    record Article(string Doi, string Title, string PublishedDate);
}