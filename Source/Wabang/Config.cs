using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Wabang
{
    [Serializable]
    public class Config
    {
        public string[] MyCharacters = new[] { "Deathlance", "Delibird" };
        public string MyRealm = "jubei'thos";
        public long LastModified;
        public Dictionary<long, Item> ItemDatabase = new Dictionary<long, Item>();

        public Item GetItemDetails(long itemId)
        {
            if (!ItemDatabase.ContainsKey(itemId))
            {
                var whichFile = Json.Client.DownloadString("http://us.battle.net/api/wow/item/" + itemId);
                var item = Json.Deserialiser.Deserialize<Item>(whichFile);
                ItemDatabase.Add(itemId, item);
            }

            return ItemDatabase[itemId];
        }
    }

    public static class Json
    {
        public static JavaScriptSerializer Deserialiser = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue };
        public static WebClient Client = new WebClient();
    }

    // {"files":[{"url":"http://us.battle.net/auction-data/78f813cae8eb0f9038e9916841e3f35d/auctions.json","lastModified":1352256167000}]}
    [Serializable]
    public class AuctionPointers
    {
        public AuctionPointer[] files { get; set; }
    }

    [Serializable]
    public class AuctionPointer
    {
        public string url { get; set; }
        public long lastModified { get; set; }
    }

    //{
    //"realm":{"name":"Jubei'Thos","slug":"jubeithos"},
    //"alliance":{"auctions":[
    //    {"auc":1605125097,"item":76662,"owner":"Arizul","bid":1150000,"buyout":1200000,"quantity":1,"timeLeft":"VERY_LONG"},
    [Serializable]
    public class Auctions
    {
        public dynamic realm { get; set; }
        public dynamic alliance { get; set; }
        public Horde horde { get; set; }
    }

    [Serializable]
    public class Horde
    {
        public Auction[] auctions { get; set; }
    }

    [Serializable]
    public class Auction
    {
        public long auc { get; set; }
        public long item { get; set; }
        public string owner { get; set; }
        public long bid { get; set; }
        public long buyout { get; set; }
        public long quantity { get; set; }
        public string timeLeft { get; set; }
    }

    [Serializable]
    public class Item
    {
        //public long buyPrice { get; set; }
        public long sellPrice { get; set; }
        public string name { get; set; }
    }

    public class Blah
    {
        public string Text { get; set; }
        public string Url { get; set; }
    }
}
