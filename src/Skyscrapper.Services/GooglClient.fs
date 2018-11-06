namespace Skyscrapper.Services
open FSharp.Data

[<RequireQualifiedAccess>]
module GooglClient = 
    type private Response = JsonProvider<"""{"kind": "text", "id": "text"}""">

    let shortenUrl apiKey url =
        let serviceUrl = sprintf "https://www.googleapis.com/urlshortener/v1/url?key=%s" apiKey
        let response = 
            Http.RequestString (
                url = serviceUrl, 
                httpMethod = "POST", 
                headers = ["Content-Type", "application/json"],
                body = TextRequest (sprintf "{\"longUrl\": \"%s\"}" url))
        Response.Parse(response).Id
