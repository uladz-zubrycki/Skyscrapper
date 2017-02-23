#r @"..\packages\FSharp.Data\lib\net40\FSharp.Data.dll"
#r @"Skyscrapper.DataServices\bin\Debug\Skyscrapper.DataServices.dll"
#r @"Skyscrapper.Notifier\bin\Debug\Skyscrapper.Notifier.dll"

open System
open FSharp.Data
open System.Net
open System.IO
open System.Diagnostics
open System.Collections.Generic
open Skyscrapper.DataServices
open Skyscrapper.Notifier

//let getData() = 
//    let origin = "VNO"
//    let events = getDanceEvents()

//    events 
//    |> Seq.map (fun event -> 
//        printfn "Loading directions for event '%s' on '%A'..." event.Name event.Dates.Start
//        let directions = getDirections(origin, event.Dates.Start)
//        let matchedDirection = 
//            directions
//            |> List.tryFind (fun direction -> direction.City = event.Location.City)
    
//        match matchedDirection with
//        | Some(direction) -> 
//            printfn "Direction to '%s' is available for event '%s'" direction.City event.Name
//            event, Some(direction)
//        | None -> 
//            printfn "Found no directions to '%s' for event '%s'" event.Location.City event.Name 
//            event, None)
//    |> Seq.filter (snd >> Option.isSome)
//    |> Seq.map (fun (event, direction) ->
//        let direction = direction.Value
//        let rec getItinerariesInner () =
//            let itineraries = getItineraries (origin, direction.CityId, event.Dates.Start)
//            if itineraries.Length = 0 then
//                printfn "Got no itineraries, repeating..."
//                getItinerariesInner ()
//            else
//                itineraries

//        printfn "Loading itineraries to '%s' for event '%s'..." direction.City event.Name
//        let itineraries = getItinerariesInner () |> List.sortBy (fun itinerary -> itinerary.Price)
//        printfn "Found %d itineraries for event '%s'" itineraries.Length event.Name
    
//        itineraries 
//        |> Seq.iter (fun itinerary -> 
//        printfn "\t\t %M by %s" itinerary.Price itinerary.Agent.Name)

//        (event, itineraries))
//    |> Seq.toArray
//
//let notify text =
//    let notifierPath = Path.Combine(currentDirectory, "Skyscrapper.Notifier.VK", "bin", "debug", "Skyscrapper.Notifier.VK.exe")
//    let notifierApp = Process.Start (notifierPath, sprintf "\"%s\"" text)  
//    notifierApp.WaitForExit()

type Direction = 
    | There
    | Back

let stockholmCode = "STOC"
let places = 
    ["Вильнюс", "VNO";
     "Рига", "RIX";
     "Киев", "KIEV";
     "Таллин", "TLL"; 
     "Варшава", "WARS";
     "Минск", "MSQ";]

let getDates (startDate: DateTime) (datesCount: int) =  
    List.init datesCount (fun i -> startDate.AddDays (float i))

let getFlightDates (direction: Direction) =
    match direction with
    | There -> getDates (new DateTime(2017, 07, 05)) 3 
    | Back -> getDates (new DateTime(2017, 07, 15)) 3 

let getOriginDestinationPair place (direction: Direction) = 
    match direction with
    | There -> place, stockholmCode
    | Back -> stockholmCode, place

let getFlights (flightDate: DateTime) (direction: Direction) =
    places
    |> Seq.map (fun (placeName, placeCode) ->
        let origin, destination = getOriginDestinationPair placeCode direction
        let itineraries = 
            getItineraries (origin, destination, flightDate)
            |> Seq.filter (fun it -> it.Price < 100.0M)
            |> Seq.groupBy (fun it -> it.Carrier)
            |> Seq.map (fun (carrier, items) -> items |> Seq.minBy (fun it -> it.Price))
            |> Seq.toList
        
        let top3Itineraries = 
            itineraries
            |> List.take (Math.Min (2, itineraries.Length))
            |> List.map (fun it -> (placeName, it))
        
        top3Itineraries)
    |> List.concat
    |> List.sortBy (fun (place, it) -> it.Price)

let formatItinerary (placeName: string) (direction: Direction) (itinerary: Itinerary) = 
    let directionStr = 
        match direction with
        | There -> sprintf "%s →" placeName
        | Back -> sprintf "%s ←" placeName

    let agentStr = 
        match itinerary.Carrier with
        | Some(agent) -> agent.Name
        | None -> itinerary.Agent.Name
    
    let timesStr = sprintf "%s - %s" (itinerary.Times.Departure.ToShortTimeString()) (itinerary.Times.Arrival.ToShortTimeString())

    sprintf "$%s %s %s %s %s" 
        (itinerary.Price.ToString("#.00")) 
        directionStr 
        timesStr
        agentStr
        (Googl.shortenUrl ("https://www.skyscanner.net" + itinerary.Url))

let getFlightsInfo (direction: Direction) =
    let dates = getFlightDates direction

    let infoByDates = 
        dates 
        |> Seq.map (fun date -> date, getFlights date direction)
        |> Seq.filter (fun (_, flights) -> flights.Length > 0)
        |> Seq.map (fun (date, flights) -> 
            let (_, cheapestItinerary) = flights |> Seq.minBy (fun (place, it) -> it.Price)
            let dateMinPrice = cheapestItinerary.Price
     
            let flightsText = 
                flights
                |> Seq.map (fun (place, it) -> formatItinerary place direction it)
                |> String.concat Environment.NewLine

            let dateInfo = 
                sprintf "%s (%d)\n%s" 
                    (date.ToShortDateString()) 
                    flights.Length 
                    flightsText

            (dateMinPrice, dateInfo))
        |> Seq.toList
    
    if infoByDates.Length > 0 then
        let totalMinPrice = fst (infoByDates |> Seq.minBy fst)
        (totalMinPrice.ToString("#.00"), infoByDates |> List.map snd |> String.concat "\n\n") 
    else
        ("N/A", "N/A")

let notificationMessage = 
    let (priceThere, flightsThereText) = getFlightsInfo Direction.There
    let (priceBack, flightsBackText) = getFlightsInfo Direction.Back

    sprintf """
Skyscanner, билеты без пересадок до 100$ (%s)
Туда: от $%s
Обратно: от $%s
    
─────────────
Рейсы в Стокгольм  

%s 

───────────────
Рейсы из Стокгольма  

%s""" (DateTime.Now.ToShortDateString()) 
       priceThere 
       priceBack 
       flightsThereText 
       flightsBackText

Vk.sendMessage (User 12260137) notificationMessage

//getItineraries ("MSQ", "STOC", new DateTime(2017, 03, 10))
