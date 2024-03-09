using System.Net;
using System.Net.Http.Json;
using Frontiers;
using Nest;
using Proto;
using Page = Frontiers.Page;

class FrontiersPageScrapperActor : IActor
{
    private readonly HttpClient _httpClient;
    private readonly IElasticClient _elasticClient;

    public FrontiersPageScrapperActor(HttpClient httpClient, IElasticClient elasticClient)
    {
        _httpClient = httpClient;
        _elasticClient = elasticClient;
    }

    public async Task ReceiveAsync(IContext context)
    {
        if (context.Message is ScrapPage scrapPage)
        {
            for (int i = scrapPage.Page.PageStart; i <= scrapPage.Page.PageEnd; i++)
            {
                Console.WriteLine($"Handling page {i}");                
                var articles = await GetArticles(i);
                if (articles.Articles.Length == 0)
                {
                    break;
                }

                var response = await _elasticClient.IndexManyAsync(articles.Articles.Select(x =>
                    new EsArticle(x.Doi, x.Doi, x.Title, x.PublishedDate == string.Empty ? null : DateTime.Parse(x.PublishedDate))));
                if (response.Errors)
                {
                    throw new Exception("Failed to index articles to elastic search");
                }
            }

            if (context.Parent is not null)
            {
                context.Send(context.Parent, new StoppedProcessing(context.Self));   
            }
        }
    }

    private async Task<SearchResponse> GetArticles(int scrollPage)
    {
        try
        {
            var payload = new SearchPayload(Array.Empty<int>(), new SearchFilter(0, 0, 0, 0, 0, 0), false, 0,
                string.Empty,
                100, scrollPage);
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
    record EsArticle(string Id, string Doi, string Title, DateTime? PublishedDate);
}