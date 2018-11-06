module Skyscrapper.Notifications.Domain
open System
open Skyscrapper.Data.Domain

type Config = 
    { BitlyApiKey: string; 
      VkAccessToken: string; 
      ConnectionString: string; }

type Place = { Name: string; Code: string; }
type TripDirection = There | Back 
with
    override this.ToString() = match this with There -> "There" | Back -> "Back"
    static member Parse value = 
        match value with
        | "There" -> There
        | "Back" -> Back
        | _ -> failwithf "Unknown 'TripDirection' value '%s'" value

type Flight = 
    { Id: int;
      TripName: string;
      TripDirection: TripDirection;
      RequestedPlace: Place; 
      Origin: Place;
      Destination: Place;
      DepartureDate: DateTime;
      ArrivalDate: DateTime; 
      CarrierName: string option;
      AgentName: string;
      Price: decimal;
      PriceChange: decimal option;
      Url: string;
      RetrievalDate: DateTime; }
with
    static member create (dto: FlightReadModel) = 
        { Id = dto.Id;
          TripName = dto.TripName;
          TripDirection = TripDirection.Parse dto.TripDirection;
          RequestedPlace = { Name = dto.RequestedPlaceName; Code = dto.RequestedPlaceCode };
          Origin = { Name = dto.OriginName; Code = dto.OriginCode };
          Destination = { Name = dto.DestinationName; Code = dto.DestinationCode };
          DepartureDate = dto.DepartureDate;
          ArrivalDate = dto.ArrivalDate;
          CarrierName = Option.ofObj dto.CarrierName;
          AgentName = dto.AgentName;
          Price = dto.Price;
          PriceChange = Option.ofNullable dto.PriceChange;
          Url = dto.Url;
          RetrievalDate = dto.RetrievalDate; }

type Trip = 
    { TripName: string;
      Directions: TripDirection list;
      ThereTripDate: DateTime option;
      BackTripDate: DateTime option;
      TargetPlace: Place;
      FlightPriceLimit: decimal;
      DatesSearchInterval: int; 
      LastUpdatedAt: DateTime option; }
with 
    static member create (dto: TripModel) = 
        let directions = 
            match dto.Directions with
            | "There" -> [There]
            | "Back" -> [Back]
            | "Both" -> [There; Back]
            | _ -> failwithf "Not supported TripModel.Directions value '%s'" dto.Directions

        { TripName = dto.TripName;
          Directions = directions;
          ThereTripDate = Option.ofNullable dto.ThereTripDate;
          BackTripDate = Option.ofNullable dto.BackTripDate;
          TargetPlace = { Name = dto.TargetPlaceName; Code = dto.TargetPlaceCode }
          FlightPriceLimit = dto.FlightPriceLimit;
          DatesSearchInterval = dto.DatesSearchInterval;
          LastUpdatedAt = Option.ofNullable dto.LastUpdatedAt; }