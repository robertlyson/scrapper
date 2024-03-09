using System.Net;
using System.Net.Http.Json;
using Frontiers;
using Nest;
using Proto;
using Page = Frontiers.Page;

class FrontiersScrapperActor : IActor
{
    private readonly HttpClient _httpClient;
    private readonly IElasticClient _elasticClient;
    private Dictionary<PID, bool> _children = new();

    public FrontiersScrapperActor(HttpClient httpClient, IElasticClient elasticClient)
    {
        _httpClient = httpClient;
        _elasticClient = elasticClient;
        ScrollPageNumber = 0;
    }

    public int ScrollPageNumber { get; private set; }

    public async Task ReceiveAsync(IContext context)
    {
        var total = 600_000;
        
        if (context.Message is StartScrapping startScrapping)
        {
            HandleStartScrapping(context, total, startScrapping);
        }
        
        if (context.Message is FinishedProcessingPages stoppedProcessing)
        {
            _children[stoppedProcessing.Pid] = true;
            if (_children.All(x => x.Value))
            {
                await context.PoisonAsync(context.Self);
            }
        }
    }

    private void HandleStartScrapping(IContext context, int total, StartScrapping startScrapping)
    {
        var pageSize = 24;
        var pages = total / pageSize;
        var pagesPerActor = pages / startScrapping.Actors;
        var array = Enumerable.Range(0, startScrapping.Actors)
            .Select(x => new Page { PageStart = pagesPerActor * x, PageEnd = (pagesPerActor * x) + pagesPerActor })
            .ToArray();

        foreach (var page in array)
        {
            var props = Props.FromProducer(() => new FrontiersPageScrapperActor(_httpClient, _elasticClient));
            var pid = context.Spawn(props);
            _children[pid] = false;

            context.Send(pid, new ScrapPage(page, pageSize));
        }
    }
}
