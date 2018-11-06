namespace Skyscrapper.Services.Skyscanner
open System
open FSharp.Data

type Place = { Name: string; Code: string; }
type FlightTimes = { Departure: DateTime; Arrival: DateTime; }
type Agent = { Id: string; Name: string; }
type Itinerary = 
    { Carrier: Agent option; 
      Agent: Agent; 
      Price: decimal;
      Url: string;
      Origin: Place;
      Destination: Place; 
      Times: FlightTimes; }

module Itineraries = 
    open System.Diagnostics

    let [<Literal>] private servicesBaseUrl = "https://www.skyscanner.net/dataservices"
    let [<Literal>] private pricingSamplePath = @".\Skyscanner\json-samples\pricing.json"
    type private PricingResponse = JsonProvider<pricingSamplePath>

    let private loadRawPrices (origin, destination, date: DateTime) = 
        let apiEndpoint = "/flights/pricing/v3.0/search/?geo_schema=skyscanner&carrier_schema=skyscanner&response_include=query%3Bdeeplink%3Bsegment%3Bstats%3Bfqs%3Bpqs%3B_flights_availability"
        let url = servicesBaseUrl + apiEndpoint
        let body = 
            sprintf """{
                "market":"BY",
                "currency":"USD",
                "locale":"en-US",
                "cabin_class":"economy",
                "prefer_directs":false,
                "trip_type":"one-way",
                "legs":[{"origin":"%s","destination":"%s","date":"%s"}],
                "adults":1,
                "child_ages":[],
                "options": {
                    "include_unpriced_itineraries":true,
                    "include_mixed_booking_options":false
                }
            }""" origin destination (date.ToString("yyyy-MM-dd"))

        let response = 
            Network.requestJsonUntrusted(
                url, 
                ("$.itineraries", 15, 20), 
                ["$.itineraries"; "$.agents"; "$.places"; "$.segments"],
                (100, 200),
                payload = body,
                additionalHeaders = [
                    "x-skyscanner-channelid", "website";
                    "x-skyscanner-devicedetection-ismobile", "false";
                    "x-skyscanner-devicedetection-istablet", "false";
                    "x-skyscanner-trackid", "null"; ])

        PricingResponse.Parse response
    
    let private dictBy getKey items = items |> Seq.map (fun it -> (getKey it, it)) |> dict

    let private getPlacesChain (getPlace: int -> PricingResponse.Placis option) (place: PricingResponse.Placis) =
        let rec getPlacesChainInner places (place: PricingResponse.Placis) = 
            let parent = getPlace place.ParentId   
            match parent with
            | Some(parentPlace) -> getPlacesChainInner (parentPlace :: places) parentPlace
            | None -> places 
        getPlacesChainInner [place] place

    let getItineraries (origin, destination) (date: DateTime) = 
        Trace.TraceInformation (sprintf "Loading itineraries from '%s' to '%s' on '%s'" origin destination (date.ToShortDateString()))
        let rawPricesData = loadRawPrices (origin, destination, date)
        let agents = rawPricesData.Agents |> dictBy (fun agent -> agent.Id)
        let segments = rawPricesData.Segments |> dictBy (fun segment -> segment.Id)
        let places = rawPricesData.Places |> dictBy (fun place -> place.Id)
        
        let tryGetPlace id = 
            let (success, value) = places.TryGetValue id
            if success then Some(value) else None

        rawPricesData.Itineraries
        |> Seq.map (fun itinerary -> 
            let carrier = 
                itinerary.PricingOptions 
                |> Seq.map(fun option -> option.AgentIds) 
                |> Seq.concat
                |> Seq.map (fun id -> agents.[id])
                |> Seq.tryFind (fun agent -> agent.IsCarrier)
                |> Option.map (fun agent -> { Id = agent.Id; Name = agent.Name; })

            itinerary.PricingOptions
            |> Seq.collect (fun pricing -> 
                pricing.Items 
                |> Seq.filter(fun item -> item.Price.Amount.IsSome && item.SegmentIds.Length = 1)
                |> Seq.map (fun item -> 
                    let segment = segments.[item.SegmentIds.[0]]
                    let origin = places.[segment.OriginPlaceId]
                    let destination = places.[segment.DestinationPlaceId]
                    let originPlaces = getPlacesChain tryGetPlace origin
                    let destinationPlaces = getPlacesChain tryGetPlace destination
                
                    ((originPlaces, destinationPlaces), 
                     { Carrier = carrier; 
                       Agent = { Id = item.AgentId; Name = agents.[item.AgentId].Name }
                       Url = item.Url;
                       Price = item.Price.Amount.Value; 
                       Origin = { Name = origin.Name; Code = origin.DisplayCode };
                       Destination = { Name = destination.Name; Code = destination.DisplayCode };
                       Times = { Departure = segment.Departure; Arrival = segment.Arrival }})))
            |> Seq.filter (fun ((originPlaces, destinationPlaces), _) -> 
                originPlaces |> Seq.exists (fun place -> place.DisplayCode = origin || place.AltId = origin) &&
                destinationPlaces |> Seq.exists (fun place -> place.DisplayCode = destination || place.AltId = destination))) 
        |> Seq.concat
        |> Seq.map snd
        |> Seq.distinct
        |> Seq.toList