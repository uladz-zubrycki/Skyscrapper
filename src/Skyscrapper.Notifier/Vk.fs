namespace Skyscrapper.Notifier
open FSharp.Data

module private VkPrivates = 
    let apiVersion = "5.62"
    let baseUrl = "https://api.vk.com/method"
    let accessToken = ""
    let getMethodUrl methodName = sprintf "%s/%s?&access_token=%s&v=%s" baseUrl methodName accessToken apiVersion

[<AutoOpen>]
module Parameters = 
    type DialogId = 
        | User of int
        | Group of int

open VkPrivates

[<RequireQualifiedAccess>]
module Vk = 
    let sendMessage (dialogId: DialogId) text = 
        let peerId = 
            match dialogId with
            | User (id) -> id
            | Group(id) -> 2000000000 + id
    
        let parameters = ["peer_id", peerId.ToString(); "message", text ]
        let response = Http.RequestString(url = getMethodUrl "messages.send", httpMethod = "POST", body = FormValues(parameters))
        response |> ignore