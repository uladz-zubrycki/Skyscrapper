module Skyscrapper.Data.Storage
open System
open System.Runtime.ExceptionServices
open System.Threading
open System.Data.SqlClient
open PetaPoco
open Skyscrapper.Data.Domain

[<AutoOpen>]
module private Privates =
    let verifyDbAccessibility (db: IDatabase) =
        let maxAttempts = 5
        let sleep i = System.Threading.Thread.Sleep (TimeSpan.FromSeconds (float (i + 1)))
        let rec verifyInner i = 
            try
                db.ExecuteScalar<int> ("SELECT 1") |> ignore
            with
               | :? SqlException as ex -> 
               if ex.Number = 0x80131904 && i < maxAttempts then
                  sleep i
                  verifyInner (i + 1) 
               else
                  let exDispatch = ExceptionDispatchInfo.Capture ex
                  exDispatch.Throw()

        verifyInner 0

    let createDbClient connectionString = 
        let db = 
            DatabaseConfiguration
                .Build()
                .UsingConnectionString(connectionString)
                .UsingProvider<Providers.SqlServerDatabaseProvider>()
                .Create()

        do verifyDbAccessibility db
        db

let saveTrip connectionString (trip: TripModel) = 
    use db = createDbClient connectionString 
    db.Insert(trip) |> ignore

let loadTrip connectionString (tripName: string) = 
    use db = createDbClient connectionString
    let trip = 
        let queryResult = db.Fetch<TripModel>("WHERE TripName = @0", tripName)
        if queryResult.Count = 0 then
            failwithf "Can't load trip by tripName '%s'." tripName
        queryResult.[0]
    trip

let saveTripFlights connectionString tripName retrievalDate (flights: FlightWriteModel list) = 
    use db = createDbClient connectionString
    use transaction = db.GetTransaction()
    try
        flights |> Seq.iter (fun flight -> 
            try
                db.Insert(flight) |> ignore
            with
                ex ->
                    let msg = sprintf "Can't save flight '%A'" flight
                    raise (new InvalidOperationException(msg, ex)))

        db.Update<TripModel>("SET LastUpdatedAt = @0 WHERE TripName = @1", retrievalDate, tripName)
        |> ignore

        db.CompleteTransaction()
    with
        ex -> 
            db.AbortTransaction()
            failwithf "Error on flights save. '%s'" (ex.ToString())

let loadLatestFlights connectionString (tripName: string) = 
    use db = createDbClient connectionString
    db.Fetch<FlightReadModel>(
        "SELECT 
               [Id]
              ,[TripName]
              ,[TripDirection]
              ,[RetrievalDate]
              ,[CarrierName]
              ,[AgentName]
              ,[Price]
              ,[Url]
              ,[RequestedPlaceName]
              ,[RequestedPlaceCode]
              ,[OriginName]
              ,[OriginCode]
              ,[DestinationName]
              ,[DestinationCode]
              ,[DepartureDate]
              ,[ArrivalDate]
              , (SELECT TOP(1) 
                    Cur.Price - Prev.Price
                 FROM Flights AS Prev
                 WHERE Cur.TripName = Prev.TripName AND
                       Cur.TripDirection = Prev.TripDirection AND
                       Cur.RequestedPlaceCode = Prev.RequestedPlaceCode AND
                       Cur.AgentName = Prev.AgentName AND
                       Cur.OriginCode = Prev.OriginCode AND
                       Cur.DestinationCode = Prev.DestinationCode AND
                       Cur.DepartureDate = Prev.DepartureDate AND
                       Cur.ArrivalDate = Prev.ArrivalDate AND
                       Prev.RetrievalDate = (
                           SELECT MAX(RetrievalDate) 
                           FROM Flights 
                           WHERE RetrievalDate < Cur.RetrievalDate AND TripName = @0)) 
                 AS [PriceChange]
        FROM Flights AS Cur
        WHERE TripName = @0 AND 
              RetrievalDate = (SELECT LastUpdatedAt FROM Trips Where TripName = @0)", 
        tripName) 
    |> Seq.toList

