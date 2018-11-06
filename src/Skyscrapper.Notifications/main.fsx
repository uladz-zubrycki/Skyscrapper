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
open Skyscrapper.Notifications

let events = 
    getDanceEvents(20) 
    |> Async.RunSynchronously

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



//getItineraries ("MSQ", "STOC", new DateTime(2017, 03, 10))

let response = 
    Vk.sendMessage 
        "73a36a2cb436c22056a4f9c6b592e4b8199fadb804ce4b49476e1cd2aefecd9b40519b2ad0dfd1e494431" 
        (DialogId.User 12260137) 
        "Api test"

