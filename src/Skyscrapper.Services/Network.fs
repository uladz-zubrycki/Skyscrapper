namespace Skyscrapper.Services.Skyscanner
open System 
open FSharp.Data
open Newtonsoft.Json.Linq
open System.Threading
open System.Diagnostics

[<AutoOpen>]
type Network() =
    static let random = new Random()
    static let waitRandomTime minMs maxMs =
        let waitTime = random.Next(minMs, maxMs)    
        Thread.Sleep(waitTime)
        waitTime
with 
    static member private repeatOnError maxAttempts (action: int -> 'a) = 
        let responses =
            [1..maxAttempts]
            |> Seq.map (fun i -> 
                try
                    Choice1Of2(action i)
                with ex -> 
                    Choice2Of2(ex))
            |> Seq.cache
     
        let succeedChoice = responses |> Seq.tryFind (function Choice1Of2(_) -> true | _ -> false)

        match succeedChoice with
        | Some(Choice1Of2(response)) -> response
        | _ -> 
            let errors = 
                responses 
                |> Seq.map (function 
                    | Choice2Of2(error) -> error 
                    | Choice1Of2(_) -> failwith "Expected only exceptions here, but got valid response")
        
            raise (new AggregateException(errors))

    static member requestJson (url, ?payload: string, ?additionalHeaders: (string * string) list) = 
        let userAgents = 
            ["Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/64.0.3282.186 Safari/537.36";
             "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Ubuntu Chromium/34.0.1847.116 Chrome/34.0.1847.116 Safari/537.36";
             "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.12; rv:58.0) Gecko/20100101 Firefox/58.0";
             "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/45.0.2454.85 Safari/537.36 OPR/32.0.1948.25";
             "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/54.0.2840.100 YaBrowser/16.11.0.2680 Yowser/2.5 Safari/537.36";]
        
        let headers = 
            let defaults =
                ["Pragma", "no-cache";
                 "Cache-Control", "no-cache";
                 "User-Agent", userAgents.[random.Next(userAgents.Length - 1)];
                 "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                 "Accept-Encoding", "gzip, deflate, sdch, br";
                 "X-Requested-With", "XMLHttpRequest";
                 "Content-Type", "application/json";]
            match additionalHeaders with
            | Some(additionals) -> defaults @ additionals
            | None -> defaults

        let stopWatch = new Stopwatch()
        stopWatch.Start()
        let response = 
            match payload with
            | Some(body) -> 
                Http.RequestString(
                    url = url, 
                    headers = headers, 
                    body = TextRequest (body))
            | None -> 
                Http.RequestString(
                    url = url, 
                    headers = headers)
        stopWatch.Stop()
        networkCounter.TrackRequest stopWatch.ElapsedMilliseconds
        response
        
    static member requestJsonSafe (url, waitTimes, ?payload: string, ?additionalHeaders) = 
        let requestInner i = 
            let minWaitTime, maxWaitTime = waitTimes
            let waitTime = waitRandomTime minWaitTime maxWaitTime  // I'm a human, trust me
            networkCounter.TrackSleepTime (int64 waitTime)
            let response = Network.requestJson(url, ?payload = payload, ?additionalHeaders = additionalHeaders)

            if response.StartsWith "<!DOCTYPE html>" then
                failwith "Skyscanner thinks I'm a robot"
            else 
                response

        Network.repeatOnError 5 requestInner 

    static member requestJsonUntrusted (url, 
                                        (requiredPath, requiredCount, maxAttemptsCount),
                                        untrustedPaths: string seq,
                                        waitTimes,
                                        ?payload: string,
                                        ?additionalHeaders) = 
        let rawResponses =
            Seq.initInfinite (fun _ -> 
                Network.requestJsonSafe (
                    url, 
                    waitTimes, 
                    ?payload = payload, 
                    ?additionalHeaders = additionalHeaders))
            |> Seq.cache
       
        let jResponses = 
            rawResponses
            |> Seq.map JObject.Parse
            |> Seq.indexed
            |> Seq.map (fun (i, jObject) ->
                jObject.SelectToken(requiredPath)
                |> Option.ofObj
                |> Option.map (fun token -> token.Value<JArray>())
                |> function 
                   | None -> (i, false, jObject)
                   | Some(value) -> (i, value.Count > 0, jObject))
            |> Seq.filter (fun (i, hasRequiredValue, _) -> 
                if i < maxAttemptsCount - requiredCount then hasRequiredValue
                else true)
            |> Seq.take requiredCount
            |> Seq.filter (fun (_, hasRequiredValue, _) -> hasRequiredValue)
            |> Seq.map (fun (_, _, jObject) -> jObject)
            |> Seq.toList

        if jResponses.Length = 0 then
            rawResponses |> Seq.head
        else
            let mergeSettings = new JsonMergeSettings()
            mergeSettings.MergeArrayHandling <- MergeArrayHandling.Union
            mergeSettings.MergeNullValueHandling <- MergeNullValueHandling.Merge

            let baseResponse = jResponses |> Seq.head

            untrustedPaths
            |> Seq.map (fun jPath -> 
                let mergedValue = 
                    jResponses 
                    |> Seq.map (fun jObject -> jObject.SelectToken(jPath))
                    |> Seq.filter (fun jToken -> jToken <> null)
                    |> Seq.map (fun jToken -> jToken.Value<JArray>())
                    |> Seq.reduce (fun res cur -> res.Merge(cur, mergeSettings); res )
                (jPath, mergedValue))
            |> Seq.iter (fun (jPath, value) ->
                match Option.ofObj(baseResponse.SelectToken(jPath)) with
                | Some(baseValueToken) -> baseValueToken.Replace(value)
                | None -> 
                    let lastSeparatorIndex = jPath.LastIndexOf('.')
                    let propertyName = jPath.Substring(lastSeparatorIndex + 1)
                    let parentPath = jPath.Substring(0, lastSeparatorIndex)
                    let parentToken = baseResponse.SelectToken(parentPath)

                    if parentToken = null then
                        failwithf "Can't find value at path '%s'" parentPath
                    else
                        parentToken.AddAfterSelf(new JProperty(propertyName, value)))

            baseResponse.ToString()