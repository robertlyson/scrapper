## tech ideas

- use existing scraper framework 
- use scraper APIs

## scrapping challenges

- there are many publishers out there each with different page to scrap, some expose json API some just render content on the backend
- scrapping may be impossible to handle when done from single IP
- issues with rate limiting 

## pros of actor model

- actors can be spawned on different machines
- actors can be spawned on different IP
- messages to scraper actor can be scheduled to be sent at different times
- multiple scraper actors can be spawned to work on different pages 
- actors can persist state and can be restarted if they fail or if the machine they are running on fails

## issues found during the build

- top parameter doesn't work within request to Frontiers, but that's not a problem with actors because I can spawn as many actors I want and give them pages to work on

## benchmarks

- Frontiers indexing time 15 minutes

## questions about actor system

- how to configure persistence?
- scheduling - is there a way to schedule message only if prev one was finished processing?
- 