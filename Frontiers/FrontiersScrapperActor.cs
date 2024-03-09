using Nest;
using Proto;

namespace Frontiers;

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
    public bool Processing { get; private set; }

    public async Task ReceiveAsync(IContext context)
    {
        var total = 600_000;

        if (context.Message is StartScrapping startScrapping && Processing == false)
        {
            Processing = true;
            HandleStartScrapping(context, total, startScrapping);
        }
        
        if (context.Message is FinishedProcessingPages stoppedProcessing)
        {
            _children[stoppedProcessing.Pid] = true;
            if (_children.All(x => x.Value))
            {
                Processing = false;
                _children.Clear();
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