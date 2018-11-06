namespace Skyscrapper.Notifications
open System
open Skyscrapper.Notifications.Domain

module Formatter = 
    let private toLongDateString (dateTime: DateTime) = dateTime.ToString("dd.MM.yy dddd")
    let private toShortDateString (dateTime: DateTime) = dateTime.ToString("dd.MM.yy")
    let private toTimeString (dateTime: DateTime) = 
        let digits = 
            (1, "\u00B9") ::
            [ for i in [2; 3] -> i, string (char (0x00B0 + i)) ] @
            [ for i in 0 :: [4..9] -> i, string (char (0x2070 + i))]

        let hour = dateTime.ToString("HH")
        let minutes =
            (dateTime.ToString("mm"), digits)
            ||> List.fold (fun str (i, char) -> str.Replace(string i, char)) 
        hour + minutes
    
    let private toPriceLimitString (amount: decimal) = "$" + amount.ToString("F0")
    let private toPriceString (amount: decimal) = "$" + amount.ToString("F1")
    let private messagePartsSeparator = "\n\n"

    let private getCheapestFlight direction maxPrice flights = 
        flights
        |> Seq.filter (fun f -> f.Price <= maxPrice && f.TripDirection = direction)
        |> Seq.sortBy (fun f -> f.Price)
        |> Seq.tryHead
    
    let private formatCheapestFlight directionTitle trip cheapestFlight = 
        match cheapestFlight with
        | Some(flight) ->
            let priceText = flight.Price |> toPriceString
            sprintf "%s: от %s (%s)" directionTitle priceText flight.RequestedPlace.Name 
        | None -> 
            let priceLimitText = trip.FlightPriceLimit |> toPriceLimitString
            sprintf "%s: нет билетов дешевле %s" directionTitle priceLimitText
    
    let private formatHeader trip cheapestFlights startPlacesNames =
        let priceLimitText = trip.FlightPriceLimit |> toPriceLimitString
        let lastUpdatedAtText = trip.LastUpdatedAt.Value |> toLongDateString
        let thereTripDateText = trip.ThereTripDate |> Option.map toShortDateString
        let backTripDateText = trip.BackTripDate |> Option.map toShortDateString

        let plannedDatesBlock = 
            match trip.Directions with
            | [There; Back] -> sprintf "Планируемые даты: туда %s, обратно %s\n" thereTripDateText.Value backTripDateText.Value
            | [There] -> sprintf "Планируемые даты: туда %s\n" thereTripDateText.Value 
            | [Back] -> sprintf "Планируемые даты: обратно %s\n" backTripDateText.Value 
            | _ -> failwithf "not supported trip directions combination %A" trip.Directions

        let cheapestFlightsBlock = 
            cheapestFlights
            |> Seq.map (fun (direction, flight) -> 
                match direction with
                | There -> formatCheapestFlight "Туда" trip flight
                | Back -> formatCheapestFlight "Обратно" trip flight)
            |> String.concat "\n"

        sprintf "Skyscanner, прямые билеты в %s до %s\n" trip.TargetPlace.Name priceLimitText
        + sprintf "Обновлено: %s\n" lastUpdatedAtText
        + plannedDatesBlock
        + cheapestFlightsBlock
        + "\nПерелёт через: " + String.concat ", " startPlacesNames
    
    let private formatFlightPrice price priceChange =
        let priceText = price |> toPriceString
        match priceChange with
        | Some(change) when abs change > 0.1M -> 
            let priceChangeSymbol = if (change > 0M) then "↑" else "↓"
            let priceChangeText = change.ToString("+0.0;-0.0")
            sprintf "%s%s %s" priceText priceChangeSymbol priceChangeText
        | _ -> priceText

    let private formatFlight getFlightShortUrl flight = 
        let priceText = formatFlightPrice flight.Price flight.PriceChange
        let placeText = 
            let directionSymbol = match flight.TripDirection with There -> "→" | Back -> "←" 
            sprintf "%s %s" flight.RequestedPlace.Name directionSymbol

        let timesText = 
            let departureTime = flight.DepartureDate |> toTimeString
            let arrivalTime = flight.ArrivalDate |> toTimeString
            sprintf "%s - %s" departureTime arrivalTime

        let url = getFlightShortUrl flight.Url 
        sprintf "%s %s %s %s %s" priceText placeText timesText flight.AgentName url
    
    let private formatExpensiveRoutes trip cheapFlights expensiveFlights = 
        let cheapRoutes = 
            cheapFlights
            |> List.map (fun f -> f.RequestedPlace.Name)
            |> set

        let routesTexts = 
            expensiveFlights 
            |> List.groupBy (fun f -> f.RequestedPlace.Name)
            |> List.filter (fun (direction, _) -> not (cheapRoutes.Contains direction))
            |> List.sortBy fst
            |> List.map (fun (direction, flights) -> 
                let previousBestPrice = 
                    let previousPrices = 
                        flights
                        |> Seq.filter (fun f -> f.PriceChange.IsSome)
                        |> Seq.map (fun f -> f.Price - f.PriceChange.Value)
                        |> Seq.toList
                    
                    if previousPrices.Length > 0 
                    then Some(previousPrices |> List.min)
                    else None
                    
                let currentBestPrice = 
                    flights 
                    |> Seq.map (fun f -> f.Price)
                    |> Seq.min 

                let priceChange = previousBestPrice |> Option.map (fun price -> currentBestPrice - price) 
                let priceText = formatFlightPrice currentBestPrice priceChange

                if (flights |> List.length) > 1 then
                    sprintf "%s (от %s)" direction priceText
                else 
                    sprintf "%s (%s)" direction priceText )

        let routesCount = routesTexts |> List.length
        let routesTitle = 
            let modulo10 = routesCount % 10
            let modulo100 = routesCount % 100

            if modulo10 = 1 && modulo100 <> 11 then "направление"
            else if modulo10 >= 2 && modulo10 <= 4 && (modulo100 < 12 || modulo100 > 14) then "направления"
            else "направлений"
        let routesBody = routesTexts |> String.concat ", "

        let priceLimit = trip.FlightPriceLimit |> toPriceLimitString
        sprintf "%d %s дороже %s:\n%s" routesCount routesTitle priceLimit routesBody

    let private formatDateBlock trip getFlightShortUrl (dateTime, flights) = 
        let dateBlockHeader = (dateTime |> toLongDateString) + "\n"  
        let (expensiveFlights, cheapFlights) = flights |> List.partition (fun f -> (f.Price - trip.FlightPriceLimit) >= 0.1m)
        let expensiveRoutesText = formatExpensiveRoutes trip cheapFlights expensiveFlights
        let cheapFlightsCount = cheapFlights |> List.length
        
        if cheapFlightsCount = 0 
        then 
            let dateBlock = 
                dateBlockHeader + 
                expensiveRoutesText

            Some dateBlock
        else 
            let cheapFlightsText = 
                cheapFlights
                |> Seq.sortBy (fun f -> f.Price)
                |> Seq.map (formatFlight getFlightShortUrl)
                |> String.concat "\n"
           
            let dateBlock =
                dateBlockHeader + 
                sprintf "%s\n" cheapFlightsText +
                sprintf "Ещё %s" expensiveRoutesText

            Some dateBlock

    let private formatDirectionBlock trip getFlightShortUrl (direction, flights) = 
        let flightsBlocks =
            flights
            |> List.groupBy (fun f -> f.DepartureDate.Date)
            |> Seq.sortBy fst
            |> Seq.map (formatDateBlock trip getFlightShortUrl)
            |> Seq.filter Option.isSome
            |> Seq.map Option.get
            |> Seq.toList
        
        if (flightsBlocks |> List.isEmpty) then None
        else 
            let directionName = match direction with There -> "ТУДА" | Back -> "OБРАТНО"
            let directionBlockHeader = directionName + "\n───────────────\n" 
            let directionBodyBlock = flightsBlocks |> String.concat messagePartsSeparator
            let directionBlock = directionBlockHeader + directionBodyBlock
            Some(directionBlock)
    
    let formatMessage getShortFlightUrl trip flights startPlaces = 
        let cheapestFlights = 
            trip.Directions 
            |> List.map (fun direction -> 
                direction, getCheapestFlight direction trip.FlightPriceLimit flights) 
        let startPlacesNames = startPlaces |> Seq.map (fun p -> p.Name)
        let header = formatHeader trip cheapestFlights startPlacesNames
        let body = 
            flights
            |> List.groupBy (fun f -> f.TripDirection)
            |> Seq.filter (fun (direction, _) -> trip.Directions |> List.contains direction)
            |> Seq.sortBy (fst >> (function There -> 0 | Back -> 1))
            |> Seq.map (formatDirectionBlock trip getShortFlightUrl)
            |> Seq.filter Option.isSome
            |> Seq.map Option.get
            |> String.concat messagePartsSeparator
        header + messagePartsSeparator + body