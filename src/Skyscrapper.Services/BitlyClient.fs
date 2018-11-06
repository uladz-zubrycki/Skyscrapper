namespace Skyscrapper.Services
open FSharp.Data
open System.Net

[<RequireQualifiedAccess>]
module BitlyClient = 
    let shortenUrl apiKey url =
        let encodedUrl = WebUtility.UrlEncode url
        let shortenerResponse = 
            sprintf 
                "https://api-ssl.bitly.com/v3/shorten?access_token=%s&longUrl=%s&format=txt" 
                apiKey
                encodedUrl
            |> Http.RequestString
        shortenerResponse.Trim()