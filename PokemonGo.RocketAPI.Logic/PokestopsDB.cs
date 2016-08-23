using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.OleDb;

namespace PokemonGo.RocketAPI.Logic
{
    class PokestopsDB
    {
        OleDbConnection con;

        public Boolean openDB()
        {
            try
            {
                con = new OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=F:/Spegeli/PokemoGoBot-PathFinder/PokemonGo.RocketAPI.Console/bin/Debug/Logs/Pokestops.accdb");
                con.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Boolean insertPokeStop(String name, String coord, Boolean enabled, Boolean lured)
        {
            if (con.State == System.Data.ConnectionState.Open) { 
                try
                {
                    OleDbCommand cmd = new OleDbCommand("insert into Pokestops(Type,Name,Coordinate,Enabled,Lured)values('Pokestop','" + name.Trim() + "','" + coord.Trim() + "', " + enabled + ", " + lured + ")", con);
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch
                {
                    return false;
                }
            } else {
                return false;
            }
        }

        public Boolean closeDB()
        {
            try
            {
                if (con != null)
                    con.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
