using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Web.Helpers;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using BrightIdeasSoftware;

namespace Wabang
{
    public partial class Form1 : Form
    {
        public Config Config;
        public Dictionary<long, Auction> MyAuctions = new Dictionary<long, Auction>();
        public Dictionary<long, Auction> MyBids = new Dictionary<long, Auction>();
        public Dictionary<long, Auction> AlreadySeen = new Dictionary<long, Auction>();
        public NumberFormatInfo Gold = new NumberFormatInfo { NumberGroupSizes = new[] { 2, 2, 0 }, NumberGroupSeparator = "." };
        public bool DoingIt = true;
        public Guid RunningThreadId;
        private Thread WorkerThread;
        public List<Blah> Notifications = new List<Blah>(); 

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (File.Exists("config.bin"))
            {
                try
                {
                    using (var file = File.OpenRead("config.bin"))
                    {
                        Config = (Config)new BinaryFormatter().Deserialize(file);
                    }
                }
                catch (Exception)
                {
                    CreateNewConfig();
                }
            }
            else
            {
                CreateNewConfig();
            }

            StartDoingIt();
        }

        public void Worker(object runningThreadId)
        {
            var myThreadId = (Guid)runningThreadId;
            var url = "http://us.battle.net/api/wow/auction/data/" + Config.MyRealm;

            while (DoingIt && myThreadId == RunningThreadId)
            {
                try
                {
                    var whichFile = Json.Client.DownloadString(url);
                    var auctionPointers = Json.Deserialiser.Deserialize<AuctionPointers>(whichFile);
                    var latest = auctionPointers.files.OrderBy(ap => ap.lastModified).First();

                    if (latest.lastModified > Config.LastModified)
                    {
                        var newUrl = latest.url;
                        var auctionsString = Json.Client.DownloadString(newUrl);
                        //var auctionsString = File.ReadAllText(@"C:\auctions.json");
                        var auctions = Json.Deserialiser.Deserialize<Auctions>(auctionsString);
                        Config.LastModified = latest.lastModified;

                        AddNotification(string.Format("{0} - A new dump of AH data was downloaded.", DateTime.Now.ToString("s")));

                        var newAuctions = new Dictionary<long, Auction>();
                        var newMyAuctions = new Dictionary<long, Auction>();
                        foreach (var auction in auctions.horde.auctions)
                        {
                            if (Config.MyCharacters.Contains(auction.owner))
                            {
                                newMyAuctions.Add(auction.auc, auction);
                            }

                            newAuctions.Add(auction.auc, null);
                        }

                        foreach (var oldAuctionId in MyAuctions.Keys)
                        {
                            if (!newMyAuctions.ContainsKey(oldAuctionId))
                            {
                                // sold or expired
                                var oldAuction = MyAuctions[oldAuctionId];
                                if (oldAuction.timeLeft == "SHORT")
                                {
                                    // we assume it expired
                                    AddNotification(string.Format("{0} - {1}'s auction of {2} has expired.", DateTime.Now.ToString("s"), oldAuction.owner, Config.GetItemDetails(oldAuction.item).name));
                                }
                                else
                                {
                                    // it must have sold
                                    AddNotification(string.Format("{0} -  A buyer has been found for {1}'s auction of {2}.", DateTime.Now.ToString("s"), oldAuction.owner, Config.GetItemDetails(oldAuction.item).name));
                                }
                            }
                        }

                        foreach (var oldAuctionId in AlreadySeen.Keys.ToList())
                        {
                            if (!newAuctions.ContainsKey(oldAuctionId))
                            {
                                AlreadySeen.Remove(oldAuctionId);
                            }
                        }

                        foreach (var oldAuctionId in MyBids.Keys.ToList())
                        {
                            if (!newAuctions.ContainsKey(oldAuctionId))
                            {
                                MyBids.Remove(oldAuctionId);
                            }
                        }

                        var buyouts = new List<Auction>();
                        var bids = new List<Auction>();
                        foreach (var auction in auctions.horde.auctions)
                        {
                            try
                            {
                                var item = Config.GetItemDetails(auction.item);
                                if (auction.buyout > 0 && item.sellPrice * auction.quantity > auction.buyout)
                                {
                                    buyouts.Add(auction);
                                }
                                else if (auction.timeLeft != "VERY_LONG" && item.sellPrice * auction.quantity > auction.bid)
                                {
                                    bids.Add(auction);
                                }
                            }
                            catch (Exception e)
                            {
                                LogException(e);
                            }
                        }

                        foreach (var auction in buyouts.OrderBy(a => a.item))
                        {
                            if (!AlreadySeen.ContainsKey(auction.auc))
                            {
                                AlreadySeen.Add(auction.auc, auction);
                                var item = Config.GetItemDetails(auction.item);
                                AddNotification(string.Format("{0} -  You can make a profit of {3} by paying {2} for {4}x {1}.", DateTime.Now.ToString("s"), item.name, auction.buyout.ToString("#,#", Gold), (item.sellPrice*auction.quantity - auction.buyout).ToString("#,#", Gold), auction.quantity), "https://us.battle.net/wow/en/vault/character/auction/horde/browse?qual=0&itemId=" + auction.item);
                            }
                        }

                        foreach (var auction in bids.OrderBy(a => a.item))
                        {
                            if (!MyBids.ContainsKey(auction.auc))
                            {
                                MyBids.Add(auction.auc, auction);
                                var item = Config.GetItemDetails(auction.item);
                                AddNotification(string.Format("{0} -  You can make a profit of {3} by bidding {2} for {4}x {1}.", DateTime.Now.ToString("s"), item.name, auction.bid.ToString("#,#", Gold), (item.sellPrice*auction.quantity - auction.bid).ToString("#,#", Gold), auction.quantity), "https://us.battle.net/wow/en/vault/character/auction/horde/browse?qual=0&itemId=" + auction.item);
                            }
                        }

                        MyAuctions = newMyAuctions;
                        AddNotification(string.Format("{0} - Finished parsing the AH data.", DateTime.Now.ToString("s")));
                    }
                }
                catch (Exception e)
                {
                    AddNotification(e.ToString());
                }

                for (int i = 0; i < 10; i++)
                {
                    Thread.Sleep(6000);
                    if (!DoingIt || myThreadId != RunningThreadId)
                    {
                        return;
                    }
                }
            }
        }

        private void LogException(Exception e)
        {
            if (e.Message != "The remote server returned an error: (404) Not Found.")
            {
                try
                {
                    AddNotification(e.ToString());
                }
                catch (Exception)
                {
                    // application is probably shutting down, ignore
                }
            }
        }

        private void AddNotification(string text, string url = null)
        {
            Notifications.Add(new Blah { Text = text, Url = url });
            Invoke((MethodInvoker)(() => fastObjectListView1.SetObjects(Notifications)));            
        }

        private void DoIt(object sender, EventArgs e)
        {
            if (DoingIt)
            {
                DoingIt = false;
                RunningThreadId = Guid.Empty;
                textBox1.Enabled = true;
                textBox2.Enabled = true;
                button3.Text = "DoIt";
            }
            else
            {
                DoingIt = true;
                StartDoingIt();
            }
        }

        private void ClearSelected(object sender, EventArgs e)
        {
            foreach (Blah selected in fastObjectListView1.SelectedObjects)
            {
                Notifications.Remove(selected);
            }

            fastObjectListView1.SetObjects(Notifications);
        }

        private void ClearAll(object sender, EventArgs e)
        {
            Notifications.Clear();
            fastObjectListView1.SetObjects(Notifications);
        }

        private void CreateNewConfig()
        {
            Config = new Config
                     {
                         ItemDatabase = new Dictionary<long, Item>(),
                         MyCharacters = textBox1.Text.Split(','),
                         MyRealm = textBox2.Text,
                     };

            WriteOutConfig();
        }

        private void StartDoingIt()
        {
            button3.Text = "Stop";
            Config.MyCharacters = textBox1.Text.Split(',');
            Config.MyRealm = textBox2.Text;

            textBox1.Enabled = false;
            textBox2.Enabled = false;

            lock (this)
            {
                RunningThreadId = Guid.NewGuid();
                WorkerThread = new Thread(Worker);
                WorkerThread.Start(RunningThreadId);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DoingIt = false;
            WriteOutConfig();
        }

        private void WriteOutConfig()
        {
            using (var file = File.OpenWrite("config.bin"))
            {
                new BinaryFormatter().Serialize(file, Config);
            }
        }

        private void fastObjectListView1_IsHyperlink(object sender, IsHyperlinkEventArgs e)
        {
            e.Url = e.Text;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            Config.LastModified = 0;
        }
    }
}
