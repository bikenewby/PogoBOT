using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGo.RocketAPI
{
    public class BOTSessionItem
    {
        private int seq;
        private String uid;
        private String pwd;
        private double lat;
        private double lng;

        public BOTSessionItem(int seq, string uid, string pwd, double lat, double lng)
        {
            this.seq = seq;
            this.uid = uid;
            this.pwd = pwd;
            this.lat = lat;
            this.lng = lng;
        }

        public int Seq
        {
            get
            {
                return seq;
            }

            set
            {
                seq = value;
            }
        }

        public string Uid
        {
            get
            {
                return uid;
            }

            set
            {
                uid = value;
            }
        }

        public string Pwd
        {
            get
            {
                return pwd;
            }

            set
            {
                pwd = value;
            }
        }

        public double Lat
        {
            get
            {
                return lat;
            }

            set
            {
                lat = value;
            }
        }

        public double Lng
        {
            get
            {
                return lng;
            }

            set
            {
                lng = value;
            }
        }
    }
}
