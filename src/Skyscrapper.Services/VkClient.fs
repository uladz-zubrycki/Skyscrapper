namespace Skyscrapper.Services
open FSharp.Data

type DialogId = User of int | Group of int

[<RequireQualifiedAccess>]
module VkClient = 
    let private apiVersion = "5.62"
    let private baseUrl = "https://api.vk.com/method"
    let private getMethodUrl accessToken methodName = sprintf "%s/%s?&access_token=%s&v=%s" baseUrl methodName accessToken apiVersion

    let sendMessage accessToken (dialogId: DialogId) text = 
        let peerId = 
            match dialogId with
            | User(id) -> id
            | Group(id) -> 2000000000 + id
    
        let parameters = ["peer_id", peerId.ToString(); "message", text ]
        let url = getMethodUrl accessToken "messages.send"
        let response = Http.Request(url, httpMethod = "POST", body = FormValues(parameters))
        match response.Body with 
        | Text(responseBody) -> responseBody
        | _ -> failwithf "Only 'Text' response type is supported for 'Vk.sendMessage' response. Response is '%A'" response.Body
