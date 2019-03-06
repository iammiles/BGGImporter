namespace MilesMcDaniel.BGGImporter

module API =
    open System
    open System.Net
    open System.Net.Http
    open System.Text
    open System.IO
    open System.Threading
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Mvc
    open Microsoft.Azure.WebJobs
    open Microsoft.Azure.WebJobs.Extensions.Http
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Logging
    open Newtonsoft.Json
    open FSharp.Data

    type BoardGame = XmlProvider<"https://www.boardgamegeek.com/xmlapi2/thing?id=230802&type=boardgame">

    type UserProfile = XmlProvider<"""<?xml version="1.0" encoding="utf-8"?>
    <items>
        <item objecttype="thing" objectid="1" subtype="boardgame" collid="12345">
        <name sortindex="1">Test1</name>
        </item>
        <item objecttype="thing" objectid="2" subtype="boardgame" collid="123456">
        <name sortindex="1">Test2</name></item>
        <item objecttype="thing" objectid="3" subtype="boardgame" collid="1234567">
        <name sortindex="1">Test3</name></item>
    </items>
    """>

    type GameTuple = string * int

    type Game = {
        BGGId: int;
        MaxPlayers: int;
        MinPlayers: int;
        Name: string;
    }

    type Async with 
        static member AwaitTaskCorrect(task : Task) : Async<unit> =
            Async.FromContinuations(fun (sc,ec,cc) ->
                task.ContinueWith(fun (task:Task) ->
                    if task.IsFaulted then
                        let e = task.Exception
                        if e.InnerExceptions.Count = 1 then ec e.InnerExceptions.[0]
                        else ec e
                    elif task.IsCanceled then
                        ec(TaskCanceledException())
                    else
                        sc ())
                |> ignore)

        static member AwaitTaskCorrect(task : Task<'T>) : Async<'T> =
            Async.FromContinuations(fun (sc,ec,cc) ->
                task.ContinueWith(fun (task:Task<'T>) ->
                    if task.IsFaulted then
                        let e = task.Exception
                        if e.InnerExceptions.Count = 1 then ec e.InnerExceptions.[0]
                        else ec e
                    elif task.IsCanceled then
                        ec(TaskCanceledException())
                    else
                        sc task.Result)
                |> ignore)

    let client = new HttpClient()
    let createProfileUri username = "https://boardgamegeek.com/xmlapi2/collection?username=" + username + "&own=1&subtype=boardgame"
    let gameUri (id:int) = String.Format("https://www.boardgamegeek.com/xmlapi2/thing?id={0}&type=boardgame,boardgameexpansion", id)

    let getUserGames username: GameTuple[] =
       async {
            try
                let! resp = client.GetAsync(createProfileUri username) |> Async.AwaitTaskCorrect
                if resp.IsSuccessStatusCode then
                    let body = resp.Content.ReadAsByteArrayAsync().Result |> Text.Encoding.UTF8.GetString
                    let profile = body |> UserProfile.Parse
                    return profile.Items
                        |> Array.map(fun item -> (item.Name.Value, item.Objectid))
                else return failwithf "Something went wrong! Could not reach boardgamegeek.com! Response code: %A %s" resp.StatusCode resp.ReasonPhrase
            with
            | :? HttpRequestException as e ->
                return failwithf "Something went wrong! Could not reach boardgamegeek.com because '%s'" e.Message
        } |> Async.RunSynchronously

    // let getUserGames username: GameTuple[] =
    //     let profile = UserProfile.Load (createProfileUri username)
    //     profile.Items
    //         |> Array.map(fun item -> (item.Name.Value, item.Objectid))

    let getGameData (gameTuple: GameTuple): Game =
        let name, id = gameTuple
        let game = BoardGame.Load (gameUri id)
        {BGGId = id; MaxPlayers = game.Item.Maxplayers.Value; MinPlayers = game.Item.Minplayers.Value; Name = name;}

    let isReadiedProfile profileUri =
        let req = Http.Request (profileUri)
        Thread.Sleep(1000)
        req.StatusCode

    let serialize (obj: Game[]) = JsonConvert.SerializeObject obj

    let getUserGamesWithDelay id =
        Thread.Sleep 1000
        getUserGames id
        
    let tryLoadProfile id =
        match isReadiedProfile (createProfileUri id) with
        | 200 -> getUserGames id
        | 202 -> getUserGamesWithDelay id
        | _ -> null

    [<FunctionName("RetrieveGames")>]
    let retrieveGames([<HttpTrigger(AuthorizationLevel.Function, "get", Route = "Retrieve/{id}")>] req: HttpRequest, id: string, log: ILogger) =
        let profileData = tryLoadProfile id
        let listOfGames = profileData |> Array.map getGameData;
        let gamesAsJson = serialize listOfGames
        gamesAsJson