using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;

namespace PokemonGo.RocketAPI
{
    public class BOTSessions
    {
        private List<BOTSessionItem> sessionList;
        private readonly string sessionConfigFileName = Path.Combine(Directory.GetCurrentDirectory(), "Settings") + "/SessionsConfig.json";

        public BOTSessions(String defaultUID, String defaultPWD, double defaultLat, double defaultLng)
        {
            sessionList = new List<BOTSessionItem>();
            sessionList.Add(new BOTSessionItem(1, defaultUID, defaultPWD, defaultLat, defaultLng));

            if (System.IO.File.Exists(sessionConfigFileName))
            {
                // If config file exists, load and override default value
                LoadConfigFile();
            }
            else
            {
                // If config file does not exists, create new one based on default value
                CreateConfigFile();
            }
        }

        public List<BOTSessionItem> SessionList
        {
            get
            {
                return sessionList;
            }
        }

        private void LoadConfigFile()
        {
            String inStr = "";
            List<BOTSessionItem> loadedList;

            inStr = System.IO.File.ReadAllText(sessionConfigFileName);
            loadedList = JsonConvert.DeserializeObject<List<BOTSessionItem>>(inStr);
            if (loadedList.Count > 0)
                sessionList = loadedList;
        }

        private void CreateConfigFile()
        {
            String json = JsonConvert.SerializeObject(sessionList);
            byte[] dataArray = Encoding.UTF8.GetBytes(json);
            // Create directory if not existed
            System.IO.FileInfo file = new System.IO.FileInfo(sessionConfigFileName);
            file.Directory.Create(); // If the directory already exists, this method does nothing.
            // Create/write file
            System.IO.File.WriteAllText(sessionConfigFileName, json);
        }
    }
}
