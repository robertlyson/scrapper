using Nest;using Proto;
using Proto.Schedulers.SimpleScheduler;

var system = new ActorSystem();
var context = new RootContext(system);
using var httpClient = new HttpClient();
var elasticClient = new ElasticClient(new ConnectionSettings(new Uri("http://localhost:9200")).DefaultIndex("scrapper-frontiers"));
var props = Props.FromProducer(() => new FrontiersScrapperActor(httpClient, elasticClient));

var pid = context.Spawn(props);

ISimpleScheduler scheduler = new SimpleScheduler(context);
scheduler
    .ScheduleTellRepeatedly(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), pid, new StartScrapping(), out _);

// This prevents the app from exiting
// before the async work is done

Console.ReadLine();