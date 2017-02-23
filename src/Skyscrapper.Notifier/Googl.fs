namespace Skyscrapper.Notifier
open FSharp.Data

module private GooglPrivates = 
    let apiKey = ""
    type Response = JsonProvider<"""{"kind": "text", "id": "text"}""">

open GooglPrivates

[<RequireQualifiedAccess>]
module Googl = 
    let shortenUrl url =
        let serviceUrl = sprintf "https://www.googleapis.com/urlshortener/v1/url?key=%s"  apiKey
        let response = 
            Http.RequestString (
                url = serviceUrl, 
                httpMethod = "POST", 
                headers = ["Content-Type", "application/json"],
                body = TextRequest (sprintf "{\"longUrl\": \"%s\"}" url))
        Response.Parse(response).Id
