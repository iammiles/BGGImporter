namespace MilesMcDaniel.BGGImporter
open System.Net

module API =
    open System
    open System.IO
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

    let profileUri username = "https://boardgamegeek.com/xmlapi2/collection?username=" + username + "&own=1&subtype=boardgame"
    let gameUri (id:int) = String.Format("https://www.boardgamegeek.com/xmlapi2/thing?id={0}&type=boardgame,boardgameexpansion", id)

    let getUserGames username: GameTuple[] =
        let profile = UserProfile.Load (profileUri username)
        profile.Items
            |> Array.map(fun item -> (item.Name.Value, item.Objectid))

    let getGameData (gameTuple: GameTuple): Game =
        let name, id = gameTuple
        let game = BoardGame.Load (gameUri id)
        {BGGId = id; MaxPlayers = game.Item.Maxplayers.Value; MinPlayers = game.Item.Minplayers.Value; Name = name;}

    let serialize (obj: Game[]) = JsonConvert.SerializeObject obj 



    [<FunctionName("RetrieveGames")>]
    let retrieveGames([<HttpTrigger(AuthorizationLevel.Function, "get", Route = "Retrieve/{id}")>] req: HttpRequest, id: string, log: ILogger) =
        let profileData = getUserGames id
        let listOfGames = profileData |> Array.map getGameData;
        let gamesAsJson = serialize listOfGames
        gamesAsJson