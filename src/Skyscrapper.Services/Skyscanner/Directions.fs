namespace Skyscrapper.Services.Skyscanner
open System
open FSharp.Data

type Direction = { Code: string; Name: string; }

module Directions = 
    let [<Literal>] private servicesBaseUrl = "https://www.skyscanner.net"
    type private DestinationsResponse = JsonProvider<""".\Skyscanner\json-samples\destinations.json""">
    type private AutocompleteResponse = JsonProvider<""".\Skyscanner\json-samples\autosuggest-flights.json""">

    let private dateToString (date: DateTime) = date.ToString("yyyy-MM-dd")

    let private loadRawDestinations (origin, destination, date) =
        let apiEndpoint = "dataservices/browse/v3/bvweb/UK/USD/en-GB/destinations"
        let url = String.Join("/", servicesBaseUrl, apiEndpoint, origin, destination, date |> dateToString)
        let destinations = Network.requestJsonSafe (url, (300, 500)) |> DestinationsResponse.Parse
        destinations
    
    let private buildDirections (placeType, buildDirection) (rawDestinations: DestinationsResponse.Root) = 
        let places = 
            rawDestinations.Places 
            |> Seq.map (fun p -> p.PlaceId, p)
            |> dict

        rawDestinations.Routes
        |> Seq.filter (fun route -> route.Direct)
        |> Seq.map (fun route -> places.[route.DestinationId])
        |> Seq.map (fun place -> 
            if place.Type = placeType then
                buildDirection place
            else 
                failwithf "Expected place to be of type '%s', but got '%s', place: '%A'" placeType place.Type place)
        |> Seq.distinct

    let private buildCountryDirections rawDestinations = 
        rawDestinations
        |> buildDirections (
            "Country", 
            fun place -> { Code = place.PlaceId; Name = place.Name })
    
    let private buildCityDirections rawDestinations = 
        rawDestinations
        |> buildDirections (
            "Station", 
            fun place -> { Code = place.CityId.Value; Name = place.CityName.Value } )

    let getCountryByCityCode cityCode =
        let url = sprintf "%s/g/autosuggest-flights/UK/en-GB/%s?isDestination=true&ccy=USD" servicesBaseUrl cityCode
        Network.requestJsonSafe (url, (0, 50)) 
        |> AutocompleteResponse.Parse
        |> Seq.find (fun p -> p.PlaceId = cityCode)
        |> (fun p -> {Name = p.CountryName; Code = p.CountryId })

    let getReachableDirections originCode date = 
        loadRawDestinations (originCode, "anywhere", date)
        |> buildCountryDirections
        |> Seq.collect (fun { Code = countryId; Name = _ } -> 
            loadRawDestinations (originCode, countryId, date)
            |> buildCityDirections )
        |> Seq.toList

    let getDirections originCode destinationCountryCode date = 
        loadRawDestinations (originCode, destinationCountryCode, date)
        |> buildCityDirections
       