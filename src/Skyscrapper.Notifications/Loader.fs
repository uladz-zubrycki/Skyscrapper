namespace Skyscrapper.Notifications
open System
open System.Diagnostics
open Skyscrapper.Data
open Skyscrapper.Data.Domain
open Skyscrapper.Services.Skyscanner
open Skyscrapper.Services.Skyscanner.Itineraries
open Skyscrapper.Notifications.Domain

module Loader = 
    let startPlaces = 
        ["Вильнюс", "VNO";
         "Рига", "RIX";
         "Киев", "KIEV";
         "Таллин", "TLL"; 
         "Варшава", "WARS";
         "Минск",  "MSQ";
         "Краков", "KRK";
         "Москва", "MOSC";
         "Гданьск", "GDN";
         "Люблин", "LUZ";
         "Каунас", "KUN"]
        |> List.map (fun (name, code) -> {Name = name; Code = code})
    
    let private getDatesRange (startDate: DateTime) (count: int) = 
        let dateRangeStart = startDate.AddDays(-Math.Floor(float count / 2.0))
        List.init count (fun i -> dateRangeStart.AddDays (float i))
    
    let private itineraryToFlight tripName tripDirection retrievalDate requestedPlace itinerary =
        { Id = 0;
          TripName = tripName;
          TripDirection = tripDirection.ToString();
          RetrievalDate = retrievalDate;
          CarrierName = itinerary.Carrier |> Option.map (fun c -> c.Name) |> Option.toObj;
          AgentName = itinerary.Agent.Name;
          Price = itinerary.Price;
          Url = itinerary.Url;
          RequestedPlaceName = requestedPlace.Name;
          RequestedPlaceCode  = requestedPlace.Code;
          OriginName = itinerary.Origin.Name;
          OriginCode = itinerary.Origin.Code;
          DestinationName = itinerary.Destination.Name;
          DestinationCode = itinerary.Destination.Code;
          DepartureDate = itinerary.Times.Departure;
          ArrivalDate = itinerary.Times.Arrival }
    
    let private getOriginDestinationPair startPlace targetPlace tripDirection = 
        match tripDirection with
        | There -> (startPlace.Code, targetPlace.Code)
        | Back -> (targetPlace.Code, startPlace.Code)
    
    let private getFlights tripName targetPlace getStartPlaces retrievalDate datesCount targetDate tripDirection =
        getDatesRange targetDate datesCount 
        |> Seq.collect (fun date -> 
            getStartPlaces date tripDirection 
            |> Seq.collect (fun startPlace -> 
                let origin, destination = getOriginDestinationPair startPlace targetPlace tripDirection
                getItineraries (origin, destination) date 
                |> Seq.filter (fun itinerary -> itinerary.Carrier.IsNone || itinerary.Carrier.Value.Id = itinerary.Agent.Id)
                |> Seq.map (itineraryToFlight tripName tripDirection retrievalDate startPlace)))
        |> Seq.distinctBy (fun f -> 
            (f.TripName, 
             f.TripDirection, 
             f.RetrievalDate, 
             f.CarrierName, 
             f.RequestedPlaceCode, 
             f.OriginCode, 
             f.DestinationCode, 
             f.DepartureDate, 
             f.ArrivalDate))
        |> Seq.toList

    let private printNetworkStats networkStats = 
        let responseTime = networkStats.TotalResponseTime
        let sleepTime = networkStats.TotalSleepTime
        
        Trace.TraceInformation <| sprintf "Requests count: %d" networkStats.RequestsCount
        Trace.TraceInformation <| sprintf "Total response time: %s(%fms)" (responseTime.ToString()) responseTime.TotalMilliseconds
        Trace.TraceInformation <| sprintf "Total sleep time: %s(%fms)" (sleepTime.ToString()) sleepTime.TotalMilliseconds

    let private buildStartPlacesProvider tripName connectionString = 
        let buildPlacesMap (flights: FlightReadModel list) = 
            flights
            |> List.groupBy (fun f -> f.DepartureDate.Date)
            |> List.map (fun (date, flights) -> 
                let requestedPlaces = 
                    flights 
                    |> List.distinctBy (fun f -> f.RequestedPlaceCode)
                    |> List.map (fun f -> {Name = f.RequestedPlaceName; Code = f.RequestedPlaceCode;})
                date, requestedPlaces)
            |> dict

        let flights =  Storage.loadLatestFlights connectionString tripName
        let thereFlights, backFlights = flights |> List.partition (fun f -> f.TripDirection = There.ToString())
        let therePlacesMap = buildPlacesMap thereFlights
        let backPlacesMap = buildPlacesMap backFlights

        let getStartPlaces date direction =
            let placesMap =
                if direction = There then therePlacesMap
                else backPlacesMap

            let succeed, requestedPlaces = placesMap.TryGetValue date
            if succeed then requestedPlaces
            else startPlaces

        getStartPlaces

    let loadTripFlights tripName connectionString =
        let trip = Storage.loadTrip connectionString tripName |> Trip.create
        let retrievalDate = DateTime.Now
        let getStartPlaces = buildStartPlacesProvider tripName connectionString
        let getTripFlights = getFlights trip.TripName trip.TargetPlace getStartPlaces retrievalDate trip.DatesSearchInterval 
        
        startNetworkTracking()

        let tripFlights = 
            trip.Directions
            |> List.collect (function 
                | There -> getTripFlights trip.ThereTripDate.Value TripDirection.There
                | Back -> getTripFlights trip.BackTripDate.Value TripDirection.Back)
        
        stopNetworkTracking() |> printNetworkStats
        Storage.saveTripFlights connectionString tripName retrievalDate tripFlights