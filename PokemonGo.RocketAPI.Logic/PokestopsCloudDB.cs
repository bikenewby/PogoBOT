using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

using Google.Apis.Services;
using Google.Apis.Fusiontables.v2;
using Google.Apis.Auth.OAuth2;
using Logger = PokemonGo.RocketAPI.Logic.Logging.Logger;
using LogLevel = PokemonGo.RocketAPI.Logic.Logging.LogLevel;

namespace PokemonGo.RocketAPI.Logic
{
    class PokestopsCloudDB
    {

        private FusiontablesService service;
        public Boolean isActive = false;

        public async Task openDB()
        {
            try
            {
                UserCredential credential;
                using (var stream = new FileStream("client_id.json", FileMode.Open, FileAccess.Read))
                {
                    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { FusiontablesService.Scope.Fusiontables },
                        "sudduenk@gmail.com", CancellationToken.None);
                }

                // Create the service.
                service = new FusiontablesService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "TestFusionTables1",
                });

                if (service != null)
                {
                    isActive = true;
                    Logger.Write("CloudDB is Active!!!", LogLevel.Warning);
                }
                else
                {
                    Logger.Write("CloudDB is NOT Active!!!", LogLevel.Error);
                }
            } catch (Exception ex)
            {
                Logger.Write("Exception on connecting to CloudDB:" + ex.ToString(), LogLevel.Error);
            }
        }

        

        public int insertPokeStop(String name, String coord, Boolean enabled, Boolean lured)
        {
            if (isActive)
            {
                try
                {
                    // Pokestops2
                    //Google.Apis.Fusiontables.v2.Data.Sqlresponse result = service.Query.Sql("INSERT INTO 1mXaXNyUzU-xcEi_MDhRIgAPs4EbQ3449M_LXPKDx (Type, Name, Coordinate, Enabled, Lured) VALUES ('Pokestop', '" + name + "', '" + coord + "','" + enabled + "','" + lured + "')").Execute();
                    // Pokestops
                    Google.Apis.Fusiontables.v2.Data.Sqlresponse result = service.Query.Sql("INSERT INTO 1FqIk481AcBYfVekoP0aBUnV66jTesS4uYIoN1x8B (Type, Name, Coordinate, Enabled, Lured) VALUES ('Pokestop', '" + name + "', '" + coord + "','" + enabled + "','" + lured + "')").Execute();
                    if (result.Rows.Count == 1)
                        return 0; // Success
                    else
                    {
                        Logger.Write("Exception on CloudDB. Row count:" + result.Rows.Count, LogLevel.Error);
                        return 1; // Failure
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception on CloudDB:" + ex.ToString());
                    if (ex.ToString().ToUpper().Contains("RATE LIMIT EXCEEDED"))
                        return 2;  // Fail due to exceed limit (need retry
                    else
                        return 1; // Fail
                }
            }
            else
            {
                Logger.Write("CloudDB is not active...",LogLevel.Error);
                return 1; // Fail
            }
        }

    }
}
