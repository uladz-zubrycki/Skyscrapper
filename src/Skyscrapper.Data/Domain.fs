module Skyscrapper.Data.Domain
open System
open PetaPoco

[<CLIMutable>]
[<TableName("Trips")>]
[<PrimaryKey("TripName", AutoIncrement = false)>]
type TripModel = 
    { TripName: string;
      Directions: string;
      ThereTripDate: Nullable<DateTime>; 
      BackTripDate: Nullable<DateTime>; 
      TargetPlaceName: string; 
      TargetPlaceCode: string; 
      FlightPriceLimit: decimal; 
      DatesSearchInterval: int;
      LastUpdatedAt: Nullable<DateTime>; }

[<CLIMutable>]
[<TableName("Flights")>]
[<PrimaryKey("Id", AutoIncrement = true)>]
type FlightWriteModel = 
    { Id: int;
      TripName: string;
      TripDirection: string;
      RetrievalDate: DateTime;
      CarrierName: string;
      AgentName: string;
      Price: decimal;
      Url: string;
      RequestedPlaceName: string;
      RequestedPlaceCode: string;
      OriginName: string;
      OriginCode: string;
      DestinationName: string;
      DestinationCode: string;
      DepartureDate: DateTime;
      ArrivalDate: DateTime; } 

[<CLIMutable>]
[<TableName("Flights")>]
[<PrimaryKey("Id", AutoIncrement = true)>]
type FlightReadModel = 
    { Id: int;
      TripName: string;
      TripDirection: string;
      RetrievalDate: DateTime;
      CarrierName: string;
      AgentName: string;
      Price: decimal;
      PriceChange: Nullable<decimal>;
      Url: string;
      RequestedPlaceName: string;
      RequestedPlaceCode: string;
      OriginName: string;
      OriginCode: string;
      DestinationName: string;
      DestinationCode: string;
      DepartureDate: DateTime;
      ArrivalDate: DateTime; } 
