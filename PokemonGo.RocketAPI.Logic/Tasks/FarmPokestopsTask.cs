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

            // KS

            Boolean isDBAvailable = false;
            PokestopsDB pokestopDB = new PokestopsDB();
            isDBAvailable = pokestopDB.openDB();

            PokestopsCloudDB pokestopCDB = new PokestopsCloudDB();
            pokestopCDB.openDB().Wait();

            int count = 0;
            int count2 = 0;
            int countCDB = 0;
            if (isDBAvailable)
                Logger.Write("PokestopsDB Opened.");
            else { 
                Logger.Write("Unable to open PokestopsDB.");
                return;
            }

            var pokegyms = await Inventory.GetPokeGyms();
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
                        if (isDBAvailable) { 
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
                Logger.Write("Inserted: " + count + " , Unable to inserted: " + count2 + ", Total " + pokestops.Count());
                Logger.Write("Inserted to Cloud DB: " + countCDB);
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
                    }
                } else
                {
                    Logger.Write("Pokegym...None.");
                }
                Logger.Write("End...");
            }

            if (isDBAvailable)
            {
                pokestopDB.closeDB();
                Logger.Write("PokestopsDB Closed.");
            }

            // KS
            /*
                        while (pokestops.Any())
                        {
                            if (Logic._client.Settings.ExportPokemonToCsvEveryMinutes > 0 && ExportPokemonToCsv._lastExportTime.AddMinutes(Logic._client.Settings.ExportPokemonToCsvEveryMinutes).Ticks < DateTime.Now.Ticks)
                            {
                                var _playerProfile = await Logic._client.Player.GetPlayer();
                                await ExportPokemonToCsv.Execute(_playerProfile.PlayerData);
                            }
                            if (Logic._client.Settings.UseLuckyEggs)
                                await UseLuckyEggTask.Execute();
                            if (Logic._client.Settings.CatchIncensePokemon)
                                await UseIncenseTask.Execute();

                            var pokestopwithcooldown = pokestops.Where(p => p.CooldownCompleteTimestampMs > DateTime.UtcNow.ToUnixTime()).FirstOrDefault();
                            if (pokestopwithcooldown != null)
                                pokestops.Remove(pokestopwithcooldown);

                            var pokestop =
                                pokestops.Where(p => p.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime())
                                    .OrderBy(
                                        i =>
                                            LocationUtils.CalculateDistanceInMeters(Logic._client.CurrentLatitude,
                                                Logic._client.CurrentLongitude, i.Latitude, i.Longitude)).First();

                            var lured = string.Empty;
                            var distance = LocationUtils.CalculateDistanceInMeters(Logic._client.CurrentLatitude, Logic._client.CurrentLongitude, pokestop.Latitude, pokestop.Longitude);
                            if (distance > 100)
                            {
                                var lurePokestop = pokestops.FirstOrDefault(x => x.LureInfo != null);
                                if (lurePokestop != null)
                                {
                                    distance = LocationUtils.CalculateDistanceInMeters(Logic._client.CurrentLatitude, Logic._client.CurrentLongitude, pokestop.Latitude, pokestop.Longitude);
                                    if (distance < 200)
                                    {
                                        lured = " is Lured";
                                        pokestop = lurePokestop;
                                    }
                                    else
                                        pokestops.Remove(pokestop);
                                }
                            } else
                                pokestops.Remove(pokestop);

                            var fortInfo = await Logic._client.Fort.GetFort(pokestop.Id, pokestop.Latitude, pokestop.Longitude);
                            var latlngDebug = string.Empty;
                            if (Logic._client.Settings.DebugMode)
                                latlngDebug = $" | Latitude: {pokestop.Latitude} - Longitude: {pokestop.Longitude}";
            //KS            Logger.Write($"Name: {fortInfo.Name} in {distance:0.##} m distance{lured}{latlngDebug}", LogLevel.Pokestop);
                            Logger.Write($"Name: {fortInfo.Name}, {latlngDebug}, in {distance:0.##} m distance{lured}", LogLevel.Pokestop);

                            if (Logic._client.Settings.UseTeleportInsteadOfWalking)
                            {
                                await
                                    Logic._client.Player.UpdatePlayerLocation(pokestop.Latitude, pokestop.Longitude,
                                        Logic._client.Settings.DefaultAltitude);
                                await RandomHelper.RandomDelay(500);
                                Logger.Write($"Using Teleport instead of Walking!", LogLevel.Navigation);
                            }
                            else
                            {
                                await
                                    Navigation.HumanLikeWalking(new GeoUtils(pokestop.Latitude, pokestop.Longitude),
                                        async () =>
                                        {
                                            // Catch normal map Pokemon
                                            await CatchMapPokemonsTask.Execute();
                                            //Catch Incense Pokemon
                                            await CatchIncensePokemonsTask.Execute();
                                            return true;
                                        });
                            }

                            //Catch Lure Pokemon
                            if (pokestop.LureInfo != null && Logic._client.Settings.CatchLuredPokemon)
                            {
                                await CatchLurePokemonsTask.Execute(pokestop);
                            }

                            var timesZeroXPawarded = 0;
                            var fortTry = 0;      //Current check
                            const int retryNumber = 45; //How many times it needs to check to clear softban
                            const int zeroCheck = 5; //How many times it checks fort before it thinks it's softban
                            do
                            {
                                var fortSearch = await Logic._client.Fort.SearchFort(pokestop.Id, pokestop.Latitude, pokestop.Longitude);

                                if (fortSearch.ExperienceAwarded > 0 && timesZeroXPawarded > 0) timesZeroXPawarded = 0;
                                if (fortSearch.ExperienceAwarded == 0)
                                {
                                    if (fortSearch.Result == FortSearchResponse.Types.Result.InCooldownPeriod)
                                    {
                                        Logger.Write("Pokestop is on Cooldown", LogLevel.Debug);
                                        break;
                                    }

                                    timesZeroXPawarded++;
                                    if (timesZeroXPawarded > zeroCheck)
                                    {
                                        fortTry += 1;

                                        if (Logic._client.Settings.DebugMode)
                                            Logger.Write(
                                                $"Seems your Soft-Banned. Trying to Unban via Pokestop Spins. Retry {fortTry} of {retryNumber - zeroCheck}",
                                                LogLevel.Warning);

                                        await RandomHelper.RandomDelay(450);
                                    }
                                }
                                else if (fortSearch.ExperienceAwarded != 0)
                                {
                                    BotStats.ExperienceThisSession += fortSearch.ExperienceAwarded;
                                    BotStats.UpdateConsoleTitle();
                                    Logger.Write($"XP: {fortSearch.ExperienceAwarded}, Gems: {fortSearch.GemsAwarded}, Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded)}", LogLevel.Pokestop);
                                    RecycleItemsTask._recycleCounter++;
                                    HatchEggsTask._hatchUpdateDelay++;
                                    break; //Continue with program as loot was succesfull.
                                }
                            } while (fortTry < retryNumber - zeroCheck); //Stop trying if softban is cleaned earlier or if 40 times fort looting failed.

                            if (RecycleItemsTask._recycleCounter >= 5)
                                await RecycleItemsTask.Execute();
                            if (HatchEggsTask._hatchUpdateDelay >= 15)
                                await HatchEggsTask.Execute();
                        }
            */


        }
    }
}
