module Skyscrapper.DataServices.Skyscanner

open Core
open FSharp.Data
open Newtonsoft.Json.Linq
open System.Net

let [<Literal>] servicesBaseUrl = "https://www.skyscanner.net/dataservices"

let requestJson (url, payload: string option) = 
    let headers = 
        [ BrowserHeaders.Pragma;
          BrowserHeaders.CacheControl;
          BrowserHeaders.UserAgent;
          BrowserHeaders.Accept;
          BrowserHeaders.AcceptEncoding;
          ("X-Skyscanner-ChannelId", "website"); ]

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

let requestJsonSafe (url, payload: string option) = 
    let requestInner i = 
        waitRandomTime 1000 1200 |> ignore // I'm a human, trust me
        let response = requestJson(url, payload)

        if response.StartsWith "<!DOCTYPE html>" then
            failwith "Skyscanner thinks I'm a robot"
        else 
            response

    repeatOnError 5 "RequestJson" requestInner 

let requestJsonUntrusted (url, 
                          payload: string option, 
                          (requiredPath, requiredCount, maxAttemptsCount),
                          untrustedPaths: string seq) = 
    let mergeSettings = new JsonMergeSettings()
    mergeSettings.MergeArrayHandling <- MergeArrayHandling.Union
    mergeSettings.MergeNullValueHandling <- MergeNullValueHandling.Merge

    let rawResponses =
        Seq.initInfinite (fun i -> requestJsonSafe (url, payload))
        |> Seq.cache
       
    let jResponses = 
        rawResponses
        |> Seq.map JObject.Parse
        |> Seq.indexed
        |> Seq.map (fun (i, jObject) ->
            Option.ofObj(jObject.SelectToken(requiredPath))
            |> Option.map (fun token -> token.Value<JArray>())
            |> function 
                | None -> (i, false, jObject)
                | Some(value) -> (i, value.Count > 0, jObject))
        |> Seq.filter (fun (i, hasRequiredValue, jObject) -> 
            if i < maxAttemptsCount - requiredCount then
                hasRequiredValue
            else
                true)
        |> Seq.take requiredCount
        |> Seq.filter (fun (i, hasRequiredValue, jObject) -> hasRequiredValue)
        |> Seq.map (fun (i, hasRequiredValue, jObject) -> jObject)
        |> Seq.toList

    if jResponses.Length = 0 then
        rawResponses |> Seq.head
    else
        let baseResponse = jResponses |> Seq.head

        untrustedPaths
        |> Seq.map (fun jPath -> 
            let mergedValue = 
                jResponses 
                |> Seq.map (fun jObject -> 
                    Option.ofObj(jObject.SelectToken(jPath))
                    |> Option.map (fun token -> token.Value<JArray>().ToString()))
                |> Seq.filter Option.isSome
                |> Seq.map Option.get
                |> Seq.distinct
                |> Seq.map JArray.Parse
                |> Seq.reduce (fun res cur -> res.Merge(cur); res )
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