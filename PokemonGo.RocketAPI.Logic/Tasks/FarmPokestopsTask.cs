using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Logic.Utils;
using PokemonGo.RocketAPI.Logic;
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Responses;
using Logger = PokemonGo.RocketAPI.Logic.Logging.Logger;
using LogLevel = PokemonGo.RocketAPI.Logic.Logging.LogLevel;

namespace PokemonGo.RocketAPI.Logic.Tasks
{
    public class FarmPokestopsTask
    {
        public static async Task Execute()
        {
            var distanceFromStart = LocationUtils.CalculateDistanceInMeters(
                Logic._client.Settings.DefaultLatitude, Logic._client.Settings.DefaultLongitude,
                Logic._client.CurrentLatitude, Logic._client.CurrentLongitude);

            // Edge case for when the client somehow ends up outside the defined radius
            if (Logic._client.Settings.MaxTravelDistanceInMeters != 0 &&
                distanceFromStart > Logic._client.Settings.MaxTravelDistanceInMeters)
            {
                Logger.Write(
                    $"You're outside of your defined radius! Walking to Default Coords ({distanceFromStart:0.##}m away). Is your LastCoords.ini file correct?",
                    LogLevel.Warning);
                await Navigation.HumanLikeWalking(
                    new GeoUtils(Logic._client.Settings.DefaultLatitude, Logic._client.Settings.DefaultLongitude),
                    async () =>
                    {
                        // Catch normal map Pokemon
                        await CatchMapPokemonsTask.Execute();
                        //Catch Incense Pokemon
                        await CatchIncensePokemonsTask.Execute();
                        return true;
                    });
            }

            var pokestops = await Inventory.GetPokestops();
            var pokegyms = await Inventory.GetPokeGyms();
            Boolean isDBAvailable = false;
            PokestopsDB pokestopDB = null;
            PokestopsCloudDB pokestopCDB = null;
            int count = 0;
            int count2 = 0;
            int countCDB = 0;

            // KS
            if (Logic._clientSettings.UpdateDB)
            {
                isDBAvailable = false;
                pokestopDB = new PokestopsDB();
                isDBAvailable = pokestopDB.openDB();

                pokestopCDB = new PokestopsCloudDB();
                pokestopCDB.openDB().Wait();

                count = 0;
                count2 = 0;
                countCDB = 0;
                if (isDBAvailable)
                    Logger.Write("PokestopsDB Opened.");
                else
                {
                    Logger.Write("Unable to open PokestopsDB.");
                    return;
                }
            }
            // --------------

            if (pokestops == null || !pokestops.Any())
                Logger.Write("No usable PokeStops found in your area. Reasons: Softbanned - Server Issues - MaxTravelDistanceInMeters too small",
                    LogLevel.Warning);
            else
            {
                Logger.Write($"Found {pokestops.Count()} {(pokestops.Count() == 1 ? "Pokestop" : "Pokestops")}", LogLevel.Info);
                Logger.Write($"Found {pokegyms.Count()} {(pokegyms.Count() == 1 ? "Pokegym" : "Pokegyms")}", LogLevel.Info);
                // KS ---- Log all PokeStops 

                if (pokestops.Any())
                {
                    foreach (FortData pokeStopItem in pokestops)
                    {
                        var fortInfo = await Logic._client.Fort.GetFort(pokeStopItem.Id, pokeStopItem.Latitude, pokeStopItem.Longitude);
                        String msg = "";
                        Boolean pokeStopLured = false;
                        pokeStopLured = (pokeStopItem.LureInfo != null);
                        String coord = pokeStopItem.Latitude + "," + pokeStopItem.Longitude;
                        fortInfo.Name = fortInfo.Name.Replace("\n", "");
                        fortInfo.Name = fortInfo.Name.Replace("\r", "");
                        fortInfo.Name = fortInfo.Name.Replace("\t", " ");
                        fortInfo.Name = fortInfo.Name.Replace("'", " ");
                        fortInfo.Name = fortInfo.Name.Replace("\"", " ");
                        msg = "Pokestop, " + fortInfo.Name.Trim() + ", " + coord.Trim() + ", Enable (" + pokeStopItem.Enabled + "), Lured (" + pokeStopLured + ")";
                        Logger.Write(msg);
                        if (Logic._clientSettings.UpdateDB)
                        {
                            if (isDBAvailable)
                            {
                                if (pokestopDB.insertPokeStop(fortInfo.Name.Trim(), coord.Trim(), pokeStopItem.Enabled, pokeStopLured))
                                {
                                    count++;
                                    Logger.Write(fortInfo.Name.Trim() + " Inserted. (" + count + ")");
                                    int result = pokestopCDB.insertPokeStop(fortInfo.Name.Trim(), coord.Trim(), pokeStopItem.Enabled, pokeStopLured);
                                    if (result == 0)
                                    {
                                        countCDB++;
                                        Logger.Write(fortInfo.Name.Trim() + " Inserted to Cloud DB. (" + count + ")");
                                    }
                                    else
                                    {
                                        while (result == 2)
                                        {
                                            Console.WriteLine("Retry inserting CloudDB");
                                            Thread.Sleep(2000);
                                            result = pokestopCDB.insertPokeStop(fortInfo.Name.Trim(), coord.Trim(), pokeStopItem.Enabled, pokeStopLured);
                                            if (result == 0)
                                            {
                                                countCDB++;
                                                Logger.Write(fortInfo.Name.Trim() + " Inserted to Cloud DB. (" + count + ")");
                                            }
                                        }
                                        if (result == 1)
                                        {
                                            Logger.Write("Unable to insert to Cloud DB: " + fortInfo.Name.Trim() + " (" + count + ")");
                                        }
                                    }
                                }
                                else
                                {
                                    count2++;
                                    Logger.Write("Unable to insert: " + fortInfo.Name.Trim() + " (" + count2 + ")");
                                }
                            }
                        }
                    }
                    if (Logic._clientSettings.UpdateDB)
                    {
                        Logger.Write("Inserted: " + count + " , Unable to inserted: " + count2 + ", Total " + pokestops.Count());
                        Logger.Write("Inserted to Cloud DB: " + countCDB);
                    }
                }

                // KS ---- Log all PokeGymss
                if (pokegyms.Any())
                {
                    foreach (FortData pokeGymItem in pokegyms)
                    {
                        var fortInfo = await Logic._client.Fort.GetFort(pokeGymItem.Id, pokeGymItem.Latitude, pokeGymItem.Longitude);
                        String msg2 = "";

                        String teamName = "";
                        if (pokeGymItem.OwnedByTeam == POGOProtos.Enums.TeamColor.Neutral)
                            teamName = "None";
                        else if (pokeGymItem.OwnedByTeam == POGOProtos.Enums.TeamColor.Blue)
                            teamName = "Blue";
                        else if (pokeGymItem.OwnedByTeam == POGOProtos.Enums.TeamColor.Red)
                            teamName = "Red";
                        else if (pokeGymItem.OwnedByTeam == POGOProtos.Enums.TeamColor.Yellow)
                            teamName = "Yello";

                        msg2 = "Pokegym, " + fortInfo.Name + ", " + pokeGymItem.Latitude + "," + pokeGymItem.Longitude + ", Enable (" + pokeGymItem.Enabled + "), Prestigate (" + pokeGymItem.GymPoints + "), " + teamName;
                        Logger.Write(msg2);
                        //if (Logic._clientSettings.UpdateDB)
                        //{
                        //    if (isDBAvailable)
                        //    {
                        //        if (pokestopDB.insertPokeStop(fortInfo.Name.Trim(), coord.Trim(), pokeStopItem.Enabled, pokeStopLured))
                        //        {
                        //            count++;
                        //            Logger.Write(fortInfo.Name.Trim() + " Inserted. (" + count + ")");
                        //            int result = pokestopCDB.insertPokeStop(fortInfo.Name.Trim(), coord.Trim(), pokeStopItem.Enabled, pokeStopLured);
                        //            if (result == 0)
                        //            {
                        //                countCDB++;
                        //                Logger.Write(fortInfo.Name.Trim() + " Inserted to Cloud DB. (" + count + ")");
                        //            }
                        //            else
                        //            {
                        //                while (result == 2)
                        //                {
                        //                    Console.WriteLine("Retry inserting CloudDB");
                        //                    Thread.Sleep(2000);
                        //                    result = pokestopCDB.insertPokeStop(fortInfo.Name.Trim(), coord.Trim(), pokeStopItem.Enabled, pokeStopLured);
                        //                    if (result == 0)
                        //                    {
                        //                        countCDB++;
                        //                        Logger.Write(fortInfo.Name.Trim() + " Inserted to Cloud DB. (" + count + ")");
                        //                    }
                        //                }
                        //                if (result == 1)
                        //                {
                        //                    Logger.Write("Unable to insert to Cloud DB: " + fortInfo.Name.Trim() + " (" + count + ")");
                        //                }
                        //            }
                        //        }
                        //        else
                        //        {
                        //            count2++;
                        //            Logger.Write("Unable to insert: " + fortInfo.Name.Trim() + " (" + count2 + ")");
                        //        }
                        //    }
                        //}
                    }
                }
                else
                {
                    Logger.Write("Pokegym...None.");
                }
                Logger.Write("End...");
            }

            if (Logic._clientSettings.UpdateDB)
            {
                if (isDBAvailable)
                {
                    pokestopDB.closeDB();
                    Logger.Write("PokestopsDB Closed.");
                }
            }
        }
    }
}
