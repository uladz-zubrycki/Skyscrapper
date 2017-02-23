[<AutoOpen>]
module Skyscrapper.DataServices.Directions

open Core
open Skyscanner
open System
open FSharp.Data

type Direction = { Origin: string; City: string; CityId: string}

module private DirectionsPrivates = 
    type DestinationsParams = { Origin: string; Destination: string; Date: DateTime; }
    type Destination = { Id: string; Name: string; }
    let [<Literal>] destinationsSamplePath = jsonSamplesPath + "data-services/destinations.json"
    type DestinationsResponse = JsonProvider<destinationsSamplePath>

    let loadRawDestinations (origin, destination, date) =
        let apiEndpoint = "browse/v3/bvweb/UK/USD/en-GB/destinations"
        let url = String.Join("/", servicesBaseUrl, apiEndpoint, origin, destination, date |> dateToString)
        let destinationsContext = { Origin = origin; Destination = destination; Date = date; }
        let destinations = requestJsonSafe (url, None) |> DestinationsResponse.Parse
        (destinationsContext, destinations)
    
    let getPlaces (destinationsResponse: DestinationsResponse.Root) = 
        destinationsResponse.Places 
        |> Seq.map (fun p -> p.PlaceId, p)
        |> dict

    let processDestinations (destinationType: string, getDestinationValue) 
                            (destinationsParams: DestinationsParams, data: DestinationsResponse.Root) = 
        let places = getPlaces data

        data.Routes
        |> Seq.filter (fun route -> route.Direct)
        |> Seq.map (fun route -> places.[route.DestinationId], route)
        |> Seq.map (fun (place, route) -> 
            match place.Type with
            | placeType when placeType = destinationType ->
                getDestinationValue place
            
            | placeType -> 
                failwithf 
                    "Expected place to be of type '%s', but got '%s'.
                     Request context: '%A'; Place: '%A'" 
                     destinationType placeType destinationsParams place )

    let loadAccessibleCountries (origin, date) = 
        loadRawDestinations (origin, "anywhere", date)
        |> processDestinations (
            "Country", 
            fun place -> { Id = place.PlaceId; Name = place.Name })

    let loadAccessibleCities (origin, destinationCountry, date) = 
        loadRawDestinations (origin, destinationCountry, date)
        |> processDestinations (
            "Station", 
            fun place -> { Id = place.CityId.Value; Name = place.CityName.Value } )

open DirectionsPrivates

let getDirections (origin, date) = 
    let countries = loadAccessibleCountries (origin, date)
       
    countries 
    |> Seq.map (fun { Id = countryId; Name = _  } -> 
        loadAccessibleCities (origin, countryId, date)
        |> Seq.map (fun { Id = cityId; Name = cityName } -> 
            { Origin = origin; City = cityName; CityId = cityId }))
    |> Seq.concat
    |> Seq.toList