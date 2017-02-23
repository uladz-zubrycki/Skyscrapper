[<AutoOpen>]
module Skyscrapper.DataServices.DanceEvents

open System
open FSharp.Data
open System.Globalization

type DanceEvent = 
    { Name: string;
      Dates: DateRange;
      Styles: DanceStyle list;
      Location: Location;
      Website: string }

and DateRange = { Start: DateTime; End: DateTime; }

and DanceStyle = 
    | Balboa
    | Blues
    | Boogie
    | Charleston
    | Lindy
    | Jazz
    | Shag
with 
    static member Parse = function
        | "Balboa" -> Balboa
        | "Blues" -> Blues
        | "Boogie Woogie" -> Boogie
        | "Charleston" -> Charleston
        | "Lindy Hop" -> Lindy
        | "Jazz" -> Jazz
        | "Shag" -> Shag
        | x -> failwithf "Can't match value '%s' with 'DanceStyle'" x

and Location = { Country: string; City: string }

module private DanceEventsPrivates = 
    let baseUrl = "http://www.swingplanit.com/"
    
    let getEventsUrls () =
        printf "Loading events list.."
        let rootDocument = Http.RequestString(url = baseUrl) |> HtmlDocument.Parse 
        let events = rootDocument.CssSelect "li.europe"
        
        events 
        |> Seq.map (fun event -> event.CssSelect "a" |> List.head)
        |> Seq.map (fun a -> a.AttributeValue "href")
    
    let parseEventDates (data: string) =
        data.Split('-')
        |> Array.map (fun dateString -> 
            let preparedStr = 
                dateString
                    .Trim()
                    .Replace("st", "")
                    .Replace("nd", "")
                    .Replace("rd", "")
                    .Replace("th", "")
            try
                DateTime.ParseExact(preparedStr, "d MMM yyyy", CultureInfo.GetCultureInfo("en-US"))
            with 
                | :? FormatException as ex -> 
                    raise (new FormatException(sprintf "Can't convert '%s' to DateTime" preparedStr, ex)))
        |> (fun dates -> { Start = dates.[0]; End = dates.[1] })

    let parseDanceStyles (data: string) =
        data.Split([|','|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun value -> DanceStyle.Parse(value.Trim()))
        |> Array.toList

    let getEventDetails url = 
        printfn "Getting event details by url '%s'.." url
        let eventPage = Http.RequestString(url = url) |> HtmlDocument.Parse
        let eventName = eventPage.CssSelect(".cardtitle h2").[0].InnerText()
        let dataListItems = eventPage.CssSelect(".postcardleft ul li")

        let eventData = 
            dataListItems 
            |> Seq.map (fun li -> 
                let dataName = 
                    let nameSpan = li.CssSelect("span").[0]
                    nameSpan.InnerText().Trim('?', ':')
                let dataValue = 
                    if dataName = "Website" then
                        li.CssSelect("a").[0].AttributeValue("href")
                    else
                        li.DirectInnerText().Trim()
                (dataName, dataValue))
            |> dict

        { Name = eventName; 
          Dates = parseEventDates eventData.["When"]; 
          Styles = parseDanceStyles eventData.["Styles"]; 
          Location = { Country = eventData.["Country"]; City = eventData.["Town"] };
          Website = eventData.["Website"]}
        
open DanceEventsPrivates

let getDanceEvents () : DanceEvent list = 
    getEventsUrls () 
    |> Seq.map getEventDetails
    |> Seq.toList
