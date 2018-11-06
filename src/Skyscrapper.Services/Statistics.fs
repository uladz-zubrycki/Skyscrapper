namespace Skyscrapper.Services.Skyscanner

open System

type NetworkStats = 
    { RequestsCount: int; 
      TotalResponseTime: TimeSpan; 
      TotalSleepTime: TimeSpan; }

type NetworkCounter () = 
    let mutable requestsCount = 0;
    let mutable responseTimeMs = 0L;
    let mutable sleepTimeMs = 0L;

    member __.TrackSleepTime(timeMs) = sleepTimeMs <- sleepTimeMs + timeMs
    member __.TrackRequest(timeMs) = 
        requestsCount <- requestsCount + 1
        responseTimeMs <- responseTimeMs + timeMs

    member __.GetResults() = 
        { RequestsCount = requestsCount; 
          TotalResponseTime = TimeSpan.FromMilliseconds(float responseTimeMs); 
          TotalSleepTime = TimeSpan.FromMilliseconds(float sleepTimeMs); }

[<AutoOpen>]
module Statistics = 
    let mutable internal networkCounter = new NetworkCounter()
    let startNetworkTracking() =
        networkCounter <- new NetworkCounter()

    let stopNetworkTracking() = networkCounter.GetResults()