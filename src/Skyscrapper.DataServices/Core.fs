module Skyscrapper.DataServices.Core

open System
open System.Threading

let [<Literal>] jsonSamplesPath = "D:/dev/skyscrapper/data/json-samples/"

type BrowserHeaders = 
    static member Pragma = ("Pragma", "no-cache");
    static member CacheControl = ("Cache-Control", "no-cache");
    static member UserAgent = ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/55.0.2883.87 Safari/537.36")
    static member Accept = ("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
    static member AcceptEncoding = ("Accept-Encoding", "gzip, deflate, sdch, br");

let private random = new Random()
let getRandomInt min max = random.Next(min, max) 

let dateToString (date: DateTime) = date.ToString("yyyy-MM-dd")

let waitRandomTime minMs maxMs =
    let waitTime = getRandomInt minMs maxMs 
//    printfn "Sleeping for %dms" waitTime
    Thread.Sleep(waitTime)

let repeatOnError maxAttempts actionName (action: int -> 'a) = 
    let responses =
        [1..maxAttempts]
        |> Seq.map (fun i -> 
            try
//                printfn "Performing action '%s' for %dst time" actionName i
                Choice1Of2(action i)
            with ex -> 
//                printfn "Error during action '%s' repeated for %dst time" actionName i
                Choice2Of2(ex))
        |> Seq.cache
     
    let succeedChoice = 
        responses 
        |> Seq.tryFind (function Choice1Of2(_) -> true | _ -> false)

    match succeedChoice with
    | Some(Choice1Of2(response)) -> response
    | _ -> 
        let errors = 
            responses 
            |> Seq.map (function 
                | Choice2Of2(error) -> error 
                | Choice1Of2(_) -> failwith "Expected only exceptions here, but got valid response")
        
        raise (new AggregateException(errors))
